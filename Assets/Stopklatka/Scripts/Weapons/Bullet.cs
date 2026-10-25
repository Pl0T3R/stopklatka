using UnityEngine;

namespace Stopklatka
{
    public enum BulletOwner { Player, Enemy }

    /// <summary>
    /// A visible, slow-moving projectile. Moves in game time and sweeps a ray each frame,
    /// so it can't skip through thin walls. Real walls from the scan stop it.
    /// Your bullets also destroy enemy bullets they pass close to (closest approach of the two
    /// moving points during the frame, so they can't skip past each other).
    /// </summary>
    public class Bullet : MonoBehaviour
    {
        public Vector3 Velocity { get; private set; }
        public BulletOwner Owner { get; private set; }
        public Enemy Shooter { get; private set; }

        static readonly RaycastHit[] Hits = new RaycastHit[8];

        Transform _streak;
        MeshRenderer _streakRenderer;
        float _age, _traveled, _streakMax, _streakWidth;
        int _movedFrame = -1;

        public void Setup(Transform streak, MeshRenderer streakRenderer)
        {
            _streak = streak;
            _streakRenderer = streakRenderer;
        }

        public void Launch(Vector3 velocity, BulletOwner owner, Enemy shooter)
        {
            Velocity = velocity;
            Owner = owner;
            Shooter = shooter;
            _age = 0f;
            _traveled = 0f;
            _movedFrame = -1;
            // enemy shots get a long red trail so you can read where they go in slow motion (STYLE_GUIDE: 3-6 m)
            _streakMax = owner == BulletOwner.Enemy ? Tuning.EnemyStreakLength : Tuning.PlayerStreakLength;
            _streakWidth = 1f;
            _streakRenderer.sharedMaterial = Mats.Get(owner == BulletOwner.Enemy ? Mats.EnemyTrail : Mats.PlayerTrail);
            transform.rotation = Quaternion.LookRotation(velocity);
            _streak.localScale = new Vector3(_streakWidth, _streakWidth, 0.001f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _age += dt;
            if (_age > Tuning.BulletLifetime) { BulletSystem.Release(this); return; }

            var pos = transform.position;
            var step = Velocity * dt;
            float dist = step.magnitude;
            var dir = step / dist;
            if (Owner == BulletOwner.Player && TryIntercept(pos, step, dt)) return;
            int mask = Layers.RoomMask | Layers.EnemyMask | Layers.GrabbableMask | (Owner == BulletOwner.Enemy ? Layers.PlayerMask : 0);
            int n = Physics.RaycastNonAlloc(pos, dir, Hits, dist, mask, QueryTriggerInteraction.Collide);
            if (n > 0)
            {
                System.Array.Sort(Hits, 0, n, HitComparer.Instance);
                for (int i = 0; i < n; i++)
                    if (TryHandleHit(Hits[i])) return;
            }

            transform.position = pos + step;
            if (Owner == BulletOwner.Player) Enemy.NotifyBulletPass(pos, pos + step);   // near misses make enemies guard
            _movedFrame = Time.frameCount;
            _traveled += dist;
            _streak.localScale = new Vector3(_streakWidth, _streakWidth, Mathf.Min(_traveled, _streakMax));
        }

        /// <summary>
        /// Shooting bullets down. Both bullets move in straight lines during this frame; find the
        /// closest the two get and destroy both if that's within the intercept radius.
        /// At most 48 bullets exist, so the loop stays cheap.
        /// </summary>
        bool TryIntercept(Vector3 pos, Vector3 step, float dt)
        {
            var bullets = BulletSystem.Active;
            float r = Tuning.BulletInterceptRadius;
            float r2 = r * r;
            int frame = Time.frameCount;
            for (int i = 0; i < bullets.Count; i++)
            {
                var e = bullets[i];
                if (e.Owner != BulletOwner.Enemy) continue;
                var eStep = e.Velocity * dt;
                // where it was at the start of this frame (it may already have moved this frame)
                var ePos = e.transform.position;
                if (e._movedFrame == frame) ePos -= eStep;
                var rel = pos - ePos;
                var relStep = step - eStep;
                float far = r + relStep.magnitude;
                if (rel.sqrMagnitude > far * far) continue;
                float len2 = relStep.sqrMagnitude;
                float t = len2 > 1e-10f ? Mathf.Clamp01(-Vector3.Dot(rel, relStep) / len2) : 0f;
                if ((rel + relStep * t).sqrMagnitude > r2) continue;

                var point = ((pos + step * t) + (ePos + eStep * t)) * 0.5f;
                Effects.BulletClash(point, Velocity.normalized);
                SoundBank.Instance?.Play(Sfx.Clash, point, 0.8f, Random.Range(0.9f, 1.15f));
                BulletSystem.Release(e);
                BulletSystem.Release(this);
                return true;
            }
            return false;
        }

        /// <returns>true if the bullet stopped.</returns>
        bool TryHandleHit(RaycastHit hit)
        {
            var c = hit.collider;
            int layer = c.gameObject.layer;

            if (layer == Layers.Room)
            {
                Effects.Impact(hit.point, hit.normal);
                SoundBank.Instance?.Play(Sfx.Ricochet, hit.point, 0.35f, Random.Range(0.8f, 1.2f));
                BulletSystem.Release(this);
                return true;
            }

            if (layer == Layers.Player)
            {
                if (Owner != BulletOwner.Enemy) return false;
                PlayerHealth.Instance?.TakeHit(hit.point);
                BulletSystem.Release(this);
                return true;
            }

            if (layer == Layers.Enemy)
            {
                var enemy = c.GetComponentInParent<Enemy>();
                if (enemy != null)
                {
                    if (enemy == Shooter && _age < 0.5f) return false;   // leaving the shooter's own body
                    enemy.Kill(hit.point, Velocity.normalized);
                    BulletSystem.Release(this);
                    return true;
                }
                var target = c.GetComponentInParent<MenuTarget>();
                if (target != null && Owner == BulletOwner.Player)
                {
                    target.Activate(Velocity.normalized);
                    BulletSystem.Release(this);
                    return true;
                }
                return false;
            }

            if (layer == Layers.Grabbable)
            {
                var g = c.GetComponentInParent<Grabbable>();
                if (g == null) return false;
                if (g.EnemyOwner != null && g.EnemyOwner == Shooter) return false;   // own gun at the muzzle
                if (g.Holder != null && Owner == BulletOwner.Player) return false;   // player's own weapons
                if (g is Katana k && k.Holder != null)
                {
                    k.Deflect(hit.point);
                }
                else if (g.Holder != null)
                {
                    // anything you hold blocks bullets
                    Effects.Impact(hit.point, hit.normal);
                    SoundBank.Instance?.Play(Sfx.Ricochet, hit.point, 0.6f);
                    g.Holder.Haptic(GameConfig.Instance.haptics.block);
                }
                else if (!g.Body.isKinematic)
                {
                    g.Body.AddForceAtPosition(Velocity * 0.02f, hit.point, ForceMode.Impulse);
                    Effects.Impact(hit.point, hit.normal);
                }
                else return false;
                BulletSystem.Release(this);
                return true;
            }
            return false;
        }

        class HitComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitComparer Instance = new();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
