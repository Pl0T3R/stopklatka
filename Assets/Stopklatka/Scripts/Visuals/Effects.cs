using UnityEngine;

namespace Stopklatka
{
    /// <summary>Short-lived flashes. They live in game time, so a muzzle flash lingers in slow motion.</summary>
    public class Flash : MonoBehaviour
    {
        /// <summary>From the glow pool (yellow kill flash) instead of the muzzle-flash pool.</summary>
        public bool Glow;
        float _until, _size;

        public void Show(float size, float seconds)
        {
            _size = size;
            _until = Time.time + seconds;
            transform.localScale = Vector3.one * size;
        }

        void Update()
        {
            float left = _until - Time.time;
            if (left <= 0f) { Effects.Release(this); return; }
            transform.localScale = Vector3.one * (_size * (0.6f + 0.4f * Random.value));
        }
    }

    /// <summary>Red ring that spreads over the floor where an enemy is forming. Game time.</summary>
    public class RingFx : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static MaterialPropertyBlock _block;

        MeshRenderer _renderer;
        Color _color;
        float _age, _seconds, _radius;

        public void Setup(MeshRenderer r)
        {
            _renderer = r;
            _color = r.sharedMaterial.GetColor(BaseColorId);
        }

        public void Show(float radius, float seconds)
        {
            _radius = radius;
            _seconds = seconds;
            _age = 0f;
            Apply(0f);
        }

        void Update()
        {
            _age += Time.deltaTime;
            float t = _age / _seconds;
            if (t >= 1f) { Effects.Release(this); return; }
            Apply(t);
        }

        void Apply(float t)
        {
            float ease = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.one * Mathf.Lerp(0.15f, _radius, ease);
            _block ??= new MaterialPropertyBlock();
            var c = _color;
            c.a *= Mathf.Clamp01(t * 6f) * (1f - t);
            _block.SetColor(BaseColorId, c);
            _renderer.SetPropertyBlock(_block);
        }
    }

    public static class Effects
    {
        static Pool<Flash> _flashes, _glowFlashes;
        static Pool<RingFx> _rings;
        static Mesh _flashMesh, _ringMesh;

        static Pool<Flash> Flashes => _flashes ??= new Pool<Flash>("Flashes", CreateFlash, 8);
        static Pool<Flash> GlowFlashes => _glowFlashes ??= new Pool<Flash>("Glow Flashes", CreateGlowFlash, 4);
        static Pool<RingFx> Rings => _rings ??= new Pool<RingFx>("Spawn Rings", CreateRing, 4);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _flashes = null; _glowFlashes = null; _rings = null; }

        public static void MuzzleFlash(Vector3 position, Quaternion rotation)
        {
            var f = Flashes.Get(position + rotation * Vector3.forward * 0.03f, rotation * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            f.Show(0.07f, 0.05f);
        }

        public static void Impact(Vector3 point, Vector3 normal)
        {
            var f = Flashes.Get(point + normal * 0.01f, Quaternion.LookRotation(normal));
            f.Show(0.04f, 0.04f);
            ShardSystem.Burst(point + normal * 0.02f, Mats.Get(Mats.Weapon), 3, normal * 1.2f, 0.8f, 0.008f, 0.016f);
        }

        /// <summary>Two bullets met in the air: a small bright pop and a few dark and light specks.</summary>
        public static void BulletClash(Vector3 point, Vector3 direction)
        {
            var f = Flashes.Get(point, Quaternion.LookRotation(direction.sqrMagnitude > 1e-6f ? direction : Vector3.forward));
            f.Show(0.07f, 0.06f);
            ShardSystem.Burst(point, Mats.Get(Mats.Weapon), 3, Vector3.zero, 1.0f, 0.006f, 0.012f);
            ShardSystem.Burst(point, Mats.Get(Mats.Glass), 2, Vector3.zero, 1.2f, 0.006f, 0.01f);
        }

        /// <summary>Short bright #FFD84D pop where an enemy was hit, with a few glowing sparks (STYLE_GUIDE 8.1).</summary>
        public static void KillFlash(Vector3 point, Vector3 direction)
        {
            var cfg = Tuning.Shatter;
            var f = GlowFlashes.Get(point, Quaternion.LookRotation(direction.sqrMagnitude > 1e-6f ? direction : Vector3.forward));
            f.Show(cfg.flashSize, cfg.flashSeconds);
            ShardSystem.Burst(point, Mats.Get(Mats.GlowShard), 5, direction * 1.5f, 1.4f, 0.008f, 0.018f);
        }

        /// <summary>The tell before an enemy shot: a small glint at the muzzle (game time, so it lingers in slow motion).</summary>
        public static void AimGlint(Vector3 position, Vector3 direction)
        {
            // white, not yellow: the yellow-orange accent is only for enemies breaking (STYLE_GUIDE 1)
            var f = Flashes.Get(position, Quaternion.LookRotation(direction.sqrMagnitude > 1e-6f ? direction : Vector3.forward));
            f.Show(0.045f, Mathf.Max(0.08f, Tuning.AimTellSeconds * 0.6f));
        }

        /// <summary>Ring on the floor under a spawning enemy.</summary>
        public static void SpawnRing(Vector3 floorPoint, float seconds)
        {
            Rings.Get(floorPoint + Vector3.up * 0.01f, Quaternion.identity).Show(0.9f, seconds);
        }

        /// <summary>Creates the pools now (at load time) instead of at the first shot or kill.</summary>
        public static void Prewarm()
        {
            _ = Flashes;
            _ = GlowFlashes;
            _ = Rings;
        }

        public static void Release(Flash f)
        {
            if (f.Glow) GlowFlashes.Release(f);
            else Flashes.Release(f);
        }
        public static void Release(RingFx r) => Rings.Release(r);

        /// <summary>Clears every pooled effect, including enemy debris, shards and cracks.</summary>
        public static void ClearAll()
        {
            _flashes?.ReleaseAll();
            _glowFlashes?.ReleaseAll();
            _rings?.ReleaseAll();
            ShardSystem.ClearAll();
            DebrisSystem.ClearAll();
            Cracks.ClearAll();
        }

        static Flash CreateFlash()
        {
            if (_flashMesh == null)
            {
                var b = new MeshKit.Builder();
                b.Ico(Vector3.zero, new Vector3(0.5f, 0.5f, 0.9f), 0.25f, new System.Random(5));
                _flashMesh = b.ToMesh("Flash");
            }
            var go = MeshKit.Part("Flash", null, _flashMesh, Mats.Get(Mats.Flash));
            InstancedPart.Attach(go, InstanceGroup.Effects);
            return go.AddComponent<Flash>();
        }

        static Flash CreateGlowFlash()
        {
            var go = MeshKit.Part("Glow Flash", null, GlowKit.Flash, Mats.Get(Mats.Glow));
            InstancedPart.Attach(go, InstanceGroup.Effects);
            var f = go.AddComponent<Flash>();
            f.Glow = true;
            return f;
        }

        static RingFx CreateRing()
        {
            if (_ringMesh == null)
            {
                // unit radius: a sharp bright outer edge that fades inward over a faint fill
                var b = new MeshKit.Builder();
                b.Ring(Vector3.zero, Quaternion.identity, 0f, 0.75f, 40);
                b.Ring(Vector3.zero, Quaternion.identity, 0.75f, 0.92f, 40);
                b.Ring(Vector3.zero, Quaternion.identity, 0.92f, 1f, 40);
                _ringMesh = b.ToMesh("Spawn Ring", p =>
                {
                    float r = new Vector2(p.x, p.z).magnitude;
                    float a = r < 0.01f ? 0f : r < 0.8f ? 0.08f : r < 0.95f ? 0.3f : 1f;
                    return new Color(1f, 1f, 1f, a);
                });
            }
            var go = MeshKit.Part("Spawn Ring", null, _ringMesh, Mats.Get(Mats.RingFx));
            var ring = go.AddComponent<RingFx>();
            ring.Setup(go.GetComponent<MeshRenderer>());
            return ring;
        }
    }
}
