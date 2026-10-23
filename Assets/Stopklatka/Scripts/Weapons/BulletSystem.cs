using UnityEngine;

namespace Stopklatka
{
    /// <summary>Pooled bullets: a small dark slug plus a streak that grows behind it and fades toward its tail.</summary>
    public static class BulletSystem
    {
        static Pool<Bullet> _pool;
        static Mesh _slugMesh, _streakMesh;

        static Pool<Bullet> PoolInstance => _pool ??= new Pool<Bullet>("Bullets", Create, 48);

        public static void Fire(Vector3 position, Vector3 velocity, BulletOwner owner, Enemy shooter)
        {
            var b = PoolInstance.Get(position, Quaternion.LookRotation(velocity));
            b.Launch(velocity, owner, shooter);
        }

        public static void Release(Bullet b) => PoolInstance.Release(b);

        public static void ClearAll() => _pool?.ReleaseAll();

        /// <summary>Resets static state between play sessions (domain reload may be off).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _pool = null; }

        static Bullet Create()
        {
            if (_slugMesh == null)
            {
                var b = new MeshKit.Builder();
                b.Prism(new Vector3(0f, 0f, -0.025f), new Vector3(0f, 0f, 0.02f), 0.007f, 0.004f, 6);
                _slugMesh = b.ToMesh("Bullet Slug");
                var s = new MeshKit.Builder();
                // unit length along -Z, scaled by the bullet as it travels; opaque at the bullet, clear at the tail
                s.Prism(new Vector3(0f, 0f, -1f), Vector3.zero, 0.0012f, 0.0055f, 5);
                _streakMesh = s.ToMesh("Bullet Streak", p => new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f + p.z), 1.6f)));
            }
            var go = new GameObject("Bullet");
            MeshKit.Part("Slug", go.transform, _slugMesh, Mats.Get(Mats.Weapon));
            var streak = MeshKit.Part("Streak", go.transform, _streakMesh, Mats.Get(Mats.EnemyTrail));
            var bullet = go.AddComponent<Bullet>();
            bullet.Setup(streak.transform, streak.GetComponent<MeshRenderer>());
            return bullet;
        }
    }
}
