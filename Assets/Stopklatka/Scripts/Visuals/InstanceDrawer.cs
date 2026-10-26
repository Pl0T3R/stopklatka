using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stopklatka
{
    /// <summary>
    /// Draws crowds of identical meshes with GPU instancing (stage 7): every frame, after all game
    /// logic has moved things (LateUpdate, run last), it collects the world matrix of every active
    /// <see cref="InstancedPart"/>, groups them by mesh and material and issues one
    /// Graphics.RenderMeshInstanced per group. 320 shards then cost one draw call per shard shape and
    /// material instead of 320. The game's shaders already support it (multi_compile_instancing with
    /// the stereo macros, so multiview and single pass instanced both work, and the Depth API
    /// occlusion still runs per pixel). GameConfig performance.instancedRendering off (or a group
    /// switched off) gives every part back its own renderer at once.
    /// Created on demand; one per scene.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class InstanceDrawer : MonoBehaviour
    {
        /// <summary>Instanced draw calls issued last frame, and the objects they drew.</summary>
        public static int LastBatches { get; private set; }
        public static int LastInstances { get; private set; }

        sealed class Batch
        {
            public Mesh Mesh;
            public Material Material;
            public int Layer;
            public Matrix4x4[] Matrices = new Matrix4x4[32];
            public int Count;
            public float MeshRadius;
            public Vector3 Min, Max;
            public float MaxScale2;
            public int LastUsedFrame;
            /// <summary>The material allows instancing (checked once; the builder turns it on for every game material).</summary>
            public bool Instancing;

            public void Add(in Matrix4x4 m)
            {
                if (Count == Matrices.Length) System.Array.Resize(ref Matrices, Matrices.Length * 2);
                Matrices[Count] = m;
                var p = new Vector3(m.m03, m.m13, m.m23);
                // largest axis scale (squared) of this copy
                float sx = m.m00 * m.m00 + m.m10 * m.m10 + m.m20 * m.m20;
                float sy = m.m01 * m.m01 + m.m11 * m.m11 + m.m21 * m.m21;
                float sz = m.m02 * m.m02 + m.m12 * m.m12 + m.m22 * m.m22;
                float s2 = Mathf.Max(sx, Mathf.Max(sy, sz));
                if (Count == 0)
                {
                    Min = Max = p;
                    MaxScale2 = s2;
                }
                else
                {
                    Min = Vector3.Min(Min, p);
                    Max = Vector3.Max(Max, p);
                    if (s2 > MaxScale2) MaxScale2 = s2;
                }
                Count++;
            }
        }

        static readonly List<InstancedPart> Parts = new();
        static readonly Dictionary<long, Batch> Batches = new();
        static readonly List<Batch> BatchList = new();
        static InstanceDrawer _instance;
        static bool _quitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Parts.Clear();
            Batches.Clear();
            BatchList.Clear();
            _instance = null;
            _quitting = false;
            LastBatches = LastInstances = 0;
        }

        internal static void Add(InstancedPart part)
        {
            if (part.Index >= 0) return;
            part.Index = Parts.Count;
            Parts.Add(part);
            if (_instance == null && !_quitting)
            {
                var go = new GameObject("Instanced Draws");
                _instance = go.AddComponent<InstanceDrawer>();
            }
        }

        internal static void Remove(InstancedPart part)
        {
            Release(part);
            int i = part.Index;
            if (i < 0) return;
            // swap with the last one: O(1), the order doesn't matter
            int last = Parts.Count - 1;
            if (i != last)
            {
                var moved = Parts[last];
                Parts[i] = moved;
                moved.Index = i;
            }
            Parts.RemoveAt(last);
            part.Index = -1;
        }

        /// <summary>Hands the part back to its own renderer.</summary>
        internal static void Release(InstancedPart part)
        {
            if (!part.Forced) return;
            part.Forced = false;
            if (part.Renderer != null) part.Renderer.forceRenderingOff = false;
        }

        static bool GroupOn(PerformanceSettings p, InstanceGroup g) => g switch
        {
            InstanceGroup.Effects => p.instanceEffects,
            InstanceGroup.Enemies => p.instanceEnemies,
            InstanceGroup.Items => p.instanceItems,
            _ => p.instanceProps,
        };

        void LateUpdate()
        {
            using (PerfMarkers.Draw.Auto())
            {
                var perf = GameConfig.Instance.performance;
                bool master = perf.instancedRendering;
                int frame = Time.frameCount;
                for (int b = 0; b < BatchList.Count; b++) BatchList[b].Count = 0;

                int instances = 0;
                for (int i = 0; i < Parts.Count; i++)
                {
                    var part = Parts[i];
                    var r = part.Renderer;
                    bool want = master && !part.Suspended && GroupOn(perf, part.Group);
                    Batch batch = null;
                    if (want)
                    {
                        var mat = r.sharedMaterial;
                        var mesh = part.Filter.sharedMesh;
                        // nothing to draw, or a material without instancing: the part keeps its renderer
                        want = !ReferenceEquals(mat, null) && !ReferenceEquals(mesh, null) &&
                               (batch = GetBatch(mesh, mat, part.Layer, frame)).Instancing;
                    }
                    if (!want)
                    {
                        Release(part);
                        continue;
                    }
                    if (!part.Forced)
                    {
                        part.Forced = true;
                        r.forceRenderingOff = true;
                    }
                    if (!r.enabled) continue;   // hidden on purpose (an enemy's gun before it appears)
                    batch.Add(part.Cached.localToWorldMatrix);
                    instances++;
                }

                int batches = 0;
                for (int b = BatchList.Count - 1; b >= 0; b--)
                {
                    var batch = BatchList[b];
                    if (batch.Count == 0 || !batch.Instancing)
                    {
                        // forget groups that haven't been used for a while (destroyed meshes, old materials)
                        if (frame - batch.LastUsedFrame > 600) Forget(b);
                        continue;
                    }
                    Draw(batch);
                    batches++;
                }
                LastBatches = batches;
                LastInstances = instances;
            }
        }

        static Batch GetBatch(Mesh mesh, Material mat, int layer, int frame)
        {
            long key = ((long)mesh.GetInstanceID() << 32) ^ (uint)mat.GetInstanceID();
            if (!Batches.TryGetValue(key, out var batch))
            {
                batch = new Batch
                {
                    Mesh = mesh, Material = mat, Layer = layer,
                    MeshRadius = mesh.bounds.extents.magnitude,
                    Instancing = mat.enableInstancing && SystemInfo.supportsInstancing,
                };
                if (!batch.Instancing)
                    Debug.LogWarning($"[Stopklatka] Instanced draws: material {mat.name} has GPU instancing off, its {mesh.name} copies use their own renderers.");
                Batches.Add(key, batch);
                BatchList.Add(batch);
            }
            batch.LastUsedFrame = frame;
            return batch;
        }

        void Forget(int index)
        {
            var batch = BatchList[index];
            BatchList.RemoveAt(index);
            long remove = 0;
            bool found = false;
            foreach (var pair in Batches)
            {
                if (pair.Value != batch) continue;
                remove = pair.Key;
                found = true;
                break;
            }
            if (found) Batches.Remove(remove);
        }

        static void Draw(Batch batch)
        {
            if (ReferenceEquals(batch.Mesh, null) || batch.Mesh == null || batch.Material == null) return;
            // bounds of all copies, for culling and transparent sorting: their positions grown by the
            // mesh radius at the largest scale
            float pad = batch.MeshRadius * Mathf.Sqrt(batch.MaxScale2);
            var center = (batch.Min + batch.Max) * 0.5f;
            var size = batch.Max - batch.Min + Vector3.one * (2f * pad);
            var rp = new RenderParams(batch.Material)
            {
                layer = batch.Layer,
                worldBounds = new Bounds(center, size),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
            };
            Graphics.RenderMeshInstanced(rp, batch.Mesh, 0, batch.Matrices, batch.Count);
        }

        void OnApplicationQuit() => _quitting = true;

        void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;
            // give every part its renderer back (scene change, quitting)
            for (int i = 0; i < Parts.Count; i++) Release(Parts[i]);
        }
    }
}
