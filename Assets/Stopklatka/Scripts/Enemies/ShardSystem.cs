using UnityEngine;

namespace Stopklatka
{
    /// <summary>A single piece of debris. Lives in game time, so shards hang in the air in slow motion.</summary>
    public class Shard : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        public MeshRenderer Renderer { get; private set; }
        float _dieAt, _size;

        public void Setup(Rigidbody body, MeshRenderer renderer)
        {
            Body = body;
            Renderer = renderer;
        }

        public void Launch(Material mat, float size, Vector3 velocity, float lifetime)
        {
            Renderer.sharedMaterial = mat;
            _size = size;
            transform.localScale = Vector3.one * size;
            Body.linearVelocity = velocity;
            Body.angularVelocity = Random.insideUnitSphere * 12f;
            _dieAt = Time.time + lifetime;
        }

        void Update()
        {
            float left = _dieAt - Time.time;
            if (left <= 0f) { ShardSystem.Release(this); return; }
            if (left < 0.6f) transform.localScale = Vector3.one * (_size * left / 0.6f);
        }
    }

    /// <summary>Pooled shards for shattering enemies, bottles and bullet impacts.</summary>
    public static class ShardSystem
    {
        const int MaxShards = 320;
        static Pool<Shard> _pool;
        static Mesh[] _meshes;

        static Pool<Shard> PoolInstance => _pool ??= new Pool<Shard>("Shards", Create, 160);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _pool = null; }

        /// <summary>Burst of shards from one point.</summary>
        public static void Burst(Vector3 center, Material mat, int count, Vector3 baseVelocity, float spread, float minSize, float maxSize)
        {
            for (int i = 0; i < count; i++)
            {
                var pos = center + Random.insideUnitSphere * maxSize * 2f;
                Spawn(mat, pos, baseVelocity + Random.insideUnitSphere * spread, Random.Range(minSize, maxSize));
            }
        }

        /// <summary>
        /// Breaks a figure apart: shards fill each body part's bounds and fly along the hit.
        /// Mixes two materials (a light and a dark red) so the debris reads as broken crystal.
        /// </summary>
        public static void Shatter(Renderer[] parts, Material mat, Material darkMat, Vector3 hitPoint, Vector3 hitDir, int perPart)
        {
            int n = 0;
            foreach (var r in parts)
            {
                if (r == null) continue;
                var b = r.bounds;
                for (int i = 0; i < perPart; i++)
                {
                    var p = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y), Random.Range(b.min.z, b.max.z));
                    float near = 1f / (1f + (p - hitPoint).sqrMagnitude * 6f);
                    var v = hitDir * Random.Range(0.6f, 2.4f) * (0.4f + near) + (p - b.center).normalized * Random.Range(0.4f, 1.4f) + Vector3.up * 0.6f;
                    // pieces near the hit are smaller and faster
                    float size = Mathf.Lerp(Random.Range(0.04f, 0.08f), Random.Range(0.02f, 0.04f), near);
                    Spawn(n++ % 3 == 0 ? darkMat : mat, p, v, size);
                }
            }
        }

        static void Spawn(Material mat, Vector3 pos, Vector3 velocity, float size)
        {
            var pool = PoolInstance;
            if (pool.Active.Count >= MaxShards) pool.Release(pool.Active[0]);
            var s = pool.Get(pos, Random.rotation);
            s.Launch(mat, size, velocity, Random.Range(3.5f, 5.5f));
        }

        public static void Release(Shard s) => PoolInstance.Release(s);

        public static void ClearAll() => _pool?.ReleaseAll();

        static Shard Create()
        {
            if (_meshes == null)
            {
                var rng = new System.Random(77);
                _meshes = new Mesh[6];
                for (int i = 0; i < _meshes.Length; i++)
                {
                    var b = new MeshKit.Builder();
                    var radii = new Vector3(0.5f + (float)rng.NextDouble() * 0.5f, 0.3f + (float)rng.NextDouble() * 0.7f, 0.4f + (float)rng.NextDouble() * 0.6f);
                    b.Ico(Vector3.zero, radii * 0.5f, 0.35f, rng);
                    _meshes[i] = b.ToMesh($"Shard {i}");
                }
            }
            var go = new GameObject("Shard") { layer = Layers.Shard };
            var mesh = _meshes[Random.Range(0, _meshes.Length)];
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var col = go.AddComponent<BoxCollider>();
            col.size = mesh.bounds.size * 0.8f;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.2f;
            var shard = go.AddComponent<Shard>();
            shard.Setup(rb, r);
            return shard;
        }
    }
}
