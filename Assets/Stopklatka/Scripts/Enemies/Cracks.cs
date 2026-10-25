using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// One glowing crack strip (or the bright spot at the impact). Follows the body part it was
    /// drawn on and grows along its length in game time, so in slow motion the cracks creep out.
    /// Goes back to the pool by itself when the part is gone (the enemy shattered) or time is up.
    /// </summary>
    public class CrackFx : MonoBehaviour
    {
        Transform _target;
        Vector3 _localPos;
        Quaternion _localRot;
        float _age, _delay, _grow, _length, _width, _until;
        bool _spot;

        public void Show(Transform target, Vector3 worldPos, Quaternion worldRot, float length, float width, float delay, float grow, float lifetime, bool spot)
        {
            _target = target;
            _localPos = target.InverseTransformPoint(worldPos);
            _localRot = Quaternion.Inverse(target.rotation) * worldRot;
            _length = length;
            _width = width;
            _delay = delay;
            _grow = Mathf.Max(0.01f, grow);
            _age = 0f;
            _until = lifetime;
            _spot = spot;
            Apply();
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_target == null || !_target.gameObject.activeInHierarchy || _age > _until)
            {
                Cracks.Release(this);
                return;
            }
            Apply();
        }

        void Apply()
        {
            if (_target != null) transform.SetPositionAndRotation(_target.TransformPoint(_localPos), _target.rotation * _localRot);
            float t = Mathf.Clamp01((_age - _delay) / _grow);
            float ease = 1f - (1f - t) * (1f - t);
            transform.localScale = _spot
                ? Vector3.one * (_length * Mathf.Lerp(0.3f, 1f, ease))
                : new Vector3(Mathf.Max(1e-4f, _length * ease), 1f, _width);
        }
    }

    /// <summary>
    /// Glowing cracks spreading from the fist's impact over the hit part (STYLE_GUIDE 8.5): a bright
    /// spot plus a few jagged strips (some branching), drawn just above the surface, facing the puncher.
    /// Pooled; a few strips per punch.
    /// </summary>
    public static class Cracks
    {
        const int MaxStrips = 40;
        static Pool<CrackFx> _pool;

        static Pool<CrackFx> PoolInstance => _pool ??= new Pool<CrackFx>("Cracks", Create, 14);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _pool = null; }

        /// <param name="punchDir">Direction of the punch (into the body).</param>
        /// <param name="seconds">Game seconds until the enemy shatters.</param>
        public static void Spread(EnemyBody body, Vector3 point, Vector3 punchDir, float seconds)
        {
            var cfg = Tuning.Shatter;
            if (punchDir.sqrMagnitude < 1e-6f) punchDir = body.transform.forward;
            punchDir.Normalize();
            int index = body.NearestPart(point, out var surface);
            var part = body.Pieces[index].Renderer.transform;
            var facing = -punchDir;                       // toward the puncher
            surface += facing * 0.012f;                   // just above the surface

            // two axes across the surface
            var u = Vector3.Cross(facing, Vector3.up);
            if (u.sqrMagnitude < 1e-4f) u = Vector3.Cross(facing, Vector3.right);
            u.Normalize();
            var v = Vector3.Cross(facing, u);

            float life = seconds + 0.5f;
            Spawn(part, surface, Quaternion.LookRotation(u, facing), 0.035f, 0f, 0f, seconds * 0.4f, life, true);

            int count = Mathf.Max(1, cfg.crackCount);
            for (int i = 0; i < count; i++)
            {
                float ang = (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / count;
                var d = u * Mathf.Cos(ang) + v * Mathf.Sin(ang);
                float length = cfg.crackLength * Random.Range(0.6f, 1.2f);
                float width = Random.Range(0.012f, 0.02f);
                var rot = StripRotation(d, facing);
                Spawn(part, surface, rot, length, width, 0f, seconds * 0.7f, life, false);

                // every other crack branches off near its end, a moment later
                if (i % 2 != 0) continue;
                var branchDir = Quaternion.AngleAxis(Random.Range(25f, 45f) * (Random.value < 0.5f ? -1f : 1f), facing) * d;
                var start = surface + d * (length * 0.6f);
                Spawn(part, start, StripRotation(branchDir, facing), length * 0.55f, width * 0.7f, seconds * 0.35f, seconds * 0.55f, life, false);
            }
        }

        /// <summary>The strip mesh runs along +X and faces +Y.</summary>
        static Quaternion StripRotation(Vector3 along, Vector3 facing) =>
            Quaternion.LookRotation(Vector3.Cross(along, facing), facing);

        static void Spawn(Transform target, Vector3 pos, Quaternion rot, float length, float width, float delay, float grow, float life, bool spot)
        {
            var pool = PoolInstance;
            if (pool.Active.Count >= MaxStrips) Release(pool.Active[0]);
            var fx = pool.Get(pos, rot);
            var r = fx.GetComponent<MeshFilter>();
            r.sharedMesh = spot ? GlowKit.Cap : GlowKit.Crack;
            fx.Show(target, pos, rot, length, width, delay, grow, life, spot);
        }

        public static void Release(CrackFx fx) => PoolInstance.Release(fx);

        /// <summary>Creates the pool now (at load time) instead of at the first punch kill.</summary>
        public static void Prewarm() => _ = PoolInstance;

        public static void ClearAll() => _pool?.ReleaseAll();

        static CrackFx Create()
        {
            var go = MeshKit.Part("Crack", null, GlowKit.Crack, Mats.Get(Mats.Glow));
            return go.AddComponent<CrackFx>();
        }
    }
}
