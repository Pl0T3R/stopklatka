using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stopklatka
{
    /// <summary>
    /// Avoids the hitch of the first kill, the first reveal, the first glowing cap or the first title
    /// (stage 7): on Vulkan a shader variant's pipeline is built the first time it is drawn. While the
    /// scan loads, every game material is drawn for a few frames, plain and instanced, on a mesh with
    /// the same vertex layout as all game meshes (position, normal, color), shrunk to nothing just in
    /// front of you, so it never shows. That covers the variants that matter:
    ///   Stopklatka/Crystal with and without _REVEAL, plain and INSTANCING_ON,
    ///   Stopklatka/Transparent plain and INSTANCING_ON, Stopklatka/Text,
    /// each with the Depth API occlusion keyword the frame has (none until the depth manager starts,
    /// HARD_OCCLUSION after: the warm-up runs again when that changes).
    /// Texts: one hidden text with every Polish letter, so the TMP dynamic font atlas (or the legacy
    /// font texture) already has them.
    /// GameConfig performance.warmUpShaders off skips it.
    /// </summary>
    public static class PipelineWarmup
    {
        // every material the game draws (Hidden is the invisible EffectMesh material: its renderers are off)
        static readonly string[] Materials =
        {
            Mats.Enemy, Mats.EnemyDark, Mats.EnemyReveal, Mats.Weapon, Mats.Blade, Mats.Glass, Mats.DarkGlass,
            Mats.Statue, Mats.PropWhite, Mats.WindowLight, Mats.FloorLight, Mats.Target, Mats.EnemyTrail,
            Mats.PlayerTrail, Mats.Flash, Mats.RedFlash, Mats.FloorShadow, Mats.RingFx, Mats.Glow, Mats.GlowShard,
            Mats.Text,
        };

        const string Characters =
            "AĄBCĆDEĘFGHIJKLŁMNŃOÓPQRSŚTUVWXYZŹŻ aąbcćdeęfghijklłmnńoópqrsśtuvwxyzźż 0123456789 .,:;!?()/+-x…";

        static readonly Matrix4x4[] One = new Matrix4x4[1];
        static Mesh _mesh;

        /// <summary>Run as a coroutine of a scene object at load time (GameManager, the time test scene).</summary>
        public static IEnumerator Run()
        {
            var perf = GameConfig.Instance.performance;
            if (!perf.warmUpShaders) yield break;
            WarmTexts();
            bool occlusion = OcclusionOn();
            yield return Draw(Mathf.Max(1, perf.warmUpFrames));
            Debug.Log($"[Stopklatka] Warm-up: {Materials.Length} materials drawn plain and instanced (Depth API occlusion {(occlusion ? "on" : "off")}).");

            // the depth manager turns occlusion on once depth arrives: warm those variants too
            float until = Time.realtimeSinceStartup + 20f;
            while (OcclusionOn() == occlusion && Time.realtimeSinceStartup < until) yield return null;
            if (OcclusionOn() == occlusion) yield break;
            yield return Draw(Mathf.Max(1, perf.warmUpFrames));
            Debug.Log($"[Stopklatka] Warm-up: again with Depth API occlusion {(OcclusionOn() ? "on" : "off")}.");
        }

        static bool OcclusionOn() => Shader.IsKeywordEnabled("HARD_OCCLUSION") || Shader.IsKeywordEnabled("SOFT_OCCLUSION");

        static IEnumerator Draw(int frames)
        {
            for (int f = 0; f < frames; f++)
            {
                DrawAll();
                yield return null;
            }
        }

        static void DrawAll()
        {
            var rig = PlayerRig.Instance;
            if (rig == null) return;
            var head = rig.Head;
            // inside the view, so the draw isn't culled, but a millionth of a metre wide: no pixel ever covers it
            var at = head.position + head.forward * 0.5f;
            var matrix = Matrix4x4.TRS(at, head.rotation, Vector3.one * 1e-6f);
            One[0] = matrix;
            var mesh = WarmMesh();
            foreach (var name in Materials)
            {
                var mat = Mats.Get(name);
                if (mat == null) continue;
                var rp = new RenderParams(mat)
                {
                    worldBounds = new Bounds(at, Vector3.one * 0.01f),
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                };
                Graphics.RenderMesh(rp, mesh, 0, matrix);
                if (mat.enableInstancing && SystemInfo.supportsInstancing) Graphics.RenderMeshInstanced(rp, mesh, 0, One, 1);
            }
        }

        /// <summary>A tiny tetrahedron with the vertex layout of every MeshKit mesh.</summary>
        static Mesh WarmMesh()
        {
            if (_mesh != null) return _mesh;
            var b = new MeshKit.Builder();
            b.Tetra(Vector3.zero, 1f);
            _mesh = b.ToMesh("Warm-up");
            return _mesh;
        }

        /// <summary>Lays out every character the game's texts use, once, in a text nobody sees.</summary>
        static void WarmTexts()
        {
            var rig = PlayerRig.Instance;
            if (rig == null) return;
            var root = new GameObject("Text Warm-up").transform;
            root.SetPositionAndRotation(rig.Head.position + rig.Head.forward * 0.5f, rig.Head.rotation);
            var text = WorldText.Create("Warm-up", root, 1e-5f, Color.white);
            text.Text = Characters;
            // the legacy font (fallback texts and the wrist gauge) too
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf").RequestCharactersInTexture(Characters, 120, FontStyle.Bold);
            Object.Destroy(root.gameObject, 0.5f);
        }
    }
}
