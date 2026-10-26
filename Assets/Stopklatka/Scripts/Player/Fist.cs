using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// The black faceted fist on an empty controller (hidden while the hand holds something).
    /// A punch is measured in real time: above GameConfig player.punchKillSpeed (about 2 m/s) the
    /// enemy it touches shatters; above punchStunSpeed it only staggers and drops its weapon; slower
    /// contact does nothing. One cheap sphere overlap per frame, only while the hand moves fast.
    /// Driven by <see cref="ControllerHand"/> after it has read the controller, so the speed is current.
    /// </summary>
    [DisallowMultipleComponent]
    public class Fist : MonoBehaviour
    {
        static readonly Collider[] Hits = new Collider[8];

        Transform _visual, _parent;
        MeshRenderer _renderer;
        Enemy _lastEnemy;
        float _ignoreUntil;

        public void Tick(ControllerHand hand)
        {
            using var perfScope = PerfMarkers.Fist.Auto();
            var cfg = GameConfig.Instance.player;
            var anchor = hand.Anchor;
            EnsureVisual(hand, anchor);

            bool show = cfg.fistEnabled && hand.Held == null;
            if (_renderer.enabled != show) _renderer.enabled = show;
            if (!show) return;

            float speed = hand.Velocity.magnitude;
            float minSpeed = cfg.punchStunSpeed > 0f ? Mathf.Min(cfg.punchStunSpeed, cfg.punchKillSpeed) : cfg.punchKillSpeed;
            if (speed < minSpeed) return;

            var center = _visual.TransformPoint(Factory.FistHitCenter);
            var dir = hand.Velocity / speed;
            int n = Physics.OverlapSphereNonAlloc(center, cfg.punchRadius, Hits, Layers.EnemyMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var col = Hits[i];
                var enemy = col.GetComponentInParent<Enemy>();
                if (enemy == null)
                {
                    // the menu crystals are on the enemy layer too
                    var target = col.GetComponentInParent<MenuTarget>();
                    if (target != null && speed >= cfg.punchKillSpeed) target.Activate(dir);
                    continue;
                }
                if (enemy == _lastEnemy && Time.unscaledTime < _ignoreUntil) continue;
                _lastEnemy = enemy;
                _ignoreUntil = Time.unscaledTime + cfg.punchRepeatSeconds;

                var point = col.ClosestPoint(center);
                var h = GameConfig.Instance.haptics;
                if (speed >= cfg.punchKillSpeed)
                {
                    SoundBank.Instance?.Play(Sfx.Punch, point, 1f, Random.Range(0.9f, 1.1f));
                    enemy.Kill(point, dir, Enemy.KillKind.Punch);   // glowing cracks, then the shatter
                    hand.Haptic(h.punch);
                    TimeController.Instance?.ActionPulse();
                }
                else
                {
                    SoundBank.Instance?.Play(Sfx.Punch, point, 0.6f, 1.2f);
                    enemy.Stun(point);
                    hand.Haptic(h.punchStun);
                }
                break;   // one enemy per punch
            }
        }

        void EnsureVisual(ControllerHand hand, Transform anchor)
        {
            if (_visual == null)
            {
                var go = MeshKit.Part(hand.IsLeft ? "Left Fist" : "Right Fist", anchor, Factory.FistMesh(hand.IsLeft), Mats.Get(Mats.Weapon));
                _visual = go.transform;
                _renderer = go.GetComponent<MeshRenderer>();
                _parent = null;
            }
            if (_parent == anchor) return;
            // the rig's controller anchor: the fist moves with tracking, never with game time
            _parent = anchor;
            _visual.SetParent(anchor, false);
            Factory.FistPose(hand.IsLeft, out var pos, out var rot);
            _visual.localPosition = pos;
            _visual.localRotation = rot;
        }

        void OnDestroy()
        {
            if (_visual != null) Destroy(_visual.gameObject);
        }
    }
}
