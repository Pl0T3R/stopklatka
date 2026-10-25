using System.Collections.Generic;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// Kills enemies when swung fast and cuts bullets out of the air. A bullet is cut when the
    /// blade's path since the last frame passes it (not only when the bullet flies into a resting
    /// blade), so a quick swing at a nearly frozen bullet works in slow motion.
    /// </summary>
    public class Katana : Grabbable
    {
        public Transform BladeBase;
        public Transform BladeTip;

        const float KillTipSpeed = 1.8f;    // real-world m/s at the tip
        const int MaxSweepSteps = 8;
        static readonly Collider[] Hits = new Collider[8];
        readonly HashSet<Enemy> _cutThisSwing = new();
        Vector3 _lastTip, _lastBase;
        bool _hasLast;
        float _swingSoundAt;

        public override void OnGrab(ControllerHand hand)
        {
            base.OnGrab(hand);
            _hasLast = false;
        }

        /// <summary>A thrown blade kills (combat.thrownBladeKills), otherwise the usual throw rules.</summary>
        protected override bool KillsOnImpact(float speed) =>
            (Tuning.ThrownBladeKills && speed >= Tuning.ThrownBladeKillSpeed) || base.KillsOnImpact(speed);

        protected override void Update()
        {
            base.Update();
            if (Holder == null) { _hasLast = false; return; }

            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            var tip = BladeTip.position;
            var bladeBase = BladeBase.position;
            float tipSpeed = _hasLast ? (tip - _lastTip).magnitude / dt : 0f;
            var swingDir = _hasLast ? (tip - _lastTip).normalized : BladeTip.forward;
            if (_hasLast) CutBullets(_lastBase, _lastTip, bladeBase, tip);
            _lastTip = tip;
            _lastBase = bladeBase;
            _hasLast = true;

            if (tipSpeed < KillTipSpeed) { _cutThisSwing.Clear(); return; }

            if (tipSpeed > 3f && Time.unscaledTime > _swingSoundAt)
            {
                _swingSoundAt = Time.unscaledTime + 0.35f;
                SoundBank.Instance?.Play(Sfx.Swing, tip, 0.6f, Random.Range(0.9f, 1.15f), false);
            }

            int n = Physics.OverlapCapsuleNonAlloc(bladeBase, tip, 0.05f, Hits, Layers.EnemyMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var enemy = Hits[i].GetComponentInParent<Enemy>();
                if (enemy != null && _cutThisSwing.Add(enemy))
                {
                    enemy.Kill(Hits[i].ClosestPoint(tip), swingDir);
                    SoundBank.Instance?.Play(Sfx.Slice, tip, 0.9f);
                    Holder.Haptic(GameConfig.Instance.haptics.katanaCut);
                }
                var target = Hits[i].GetComponentInParent<MenuTarget>();
                if (target != null) target.Activate(swingDir);
            }
        }

        /// <summary>
        /// Enemy bullets near the area the blade swept this frame are cut. The sweep is sampled as a
        /// few blade positions between last frame and now (about one every 2 cm of tip travel).
        /// </summary>
        void CutBullets(Vector3 base0, Vector3 tip0, Vector3 base1, Vector3 tip1)
        {
            var bullets = BulletSystem.Active;
            if (bullets.Count == 0) return;
            float r = Tuning.KatanaCutRadius;
            float r2 = r * r;
            int steps = Mathf.Clamp(Mathf.CeilToInt((tip1 - tip0).magnitude / 0.02f), 1, MaxSweepSteps);
            var mid = (base1 + tip1) * 0.5f;
            float reach = (tip1 - base1).magnitude * 0.5f + (tip1 - tip0).magnitude + r;

            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var b = bullets[i];
                if (b.Owner != BulletOwner.Enemy) continue;
                var p = b.transform.position;
                if ((p - mid).sqrMagnitude > reach * reach) continue;
                for (int s = 0; s <= steps; s++)
                {
                    float u = s / (float)steps;
                    var a = Vector3.Lerp(base0, base1, u);
                    var c = Vector3.Lerp(tip0, tip1, u);
                    if ((ClosestOnSegment(p, a, c) - p).sqrMagnitude > r2) continue;
                    Deflect(p);
                    BulletSystem.Release(b);
                    break;
                }
            }
        }

        static Vector3 ClosestOnSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-10f) return a;
            return a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
        }

        public void Deflect(Vector3 point)
        {
            SoundBank.Instance?.Play(Sfx.Deflect, point, 1f, Random.Range(0.9f, 1.1f));
            ShardSystem.Burst(point, Mats.Get(Mats.Blade), 5, Vector3.zero, 1.5f, 0.01f, 0.02f);
            if (Holder != null) Holder.Haptic(GameConfig.Instance.haptics.block);
        }
    }
}
