using System.Collections.Generic;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>Kills enemies when swung fast and cuts bullets out of the air.</summary>
    public class Katana : Grabbable
    {
        public Transform BladeBase;
        public Transform BladeTip;

        const float KillTipSpeed = 1.8f;    // real-world m/s at the tip
        static readonly Collider[] Hits = new Collider[8];
        readonly HashSet<Enemy> _cutThisSwing = new();
        Vector3 _lastTip;
        bool _hasLast;
        float _swingSoundAt;

        protected override void Awake()
        {
            base.Awake();
            KillSpeed = 2.5f;   // a thrown katana is deadly
        }

        public override void OnGrab(ControllerHand hand)
        {
            base.OnGrab(hand);
            _hasLast = false;
        }

        protected override void Update()
        {
            base.Update();
            if (Holder == null) { _hasLast = false; return; }

            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            var tip = BladeTip.position;
            float tipSpeed = _hasLast ? (tip - _lastTip).magnitude / dt : 0f;
            var swingDir = _hasLast ? (tip - _lastTip).normalized : BladeTip.forward;
            _lastTip = tip;
            _hasLast = true;

            if (tipSpeed < KillTipSpeed) { _cutThisSwing.Clear(); return; }

            if (tipSpeed > 3f && Time.unscaledTime > _swingSoundAt)
            {
                _swingSoundAt = Time.unscaledTime + 0.35f;
                SoundBank.Instance?.Play(Sfx.Swing, tip, 0.6f, Random.Range(0.9f, 1.15f), false);
            }

            int n = Physics.OverlapCapsuleNonAlloc(BladeBase.position, tip, 0.05f, Hits, Layers.EnemyMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var enemy = Hits[i].GetComponentInParent<Enemy>();
                if (enemy != null && _cutThisSwing.Add(enemy))
                {
                    enemy.Kill(Hits[i].ClosestPoint(tip), swingDir);
                    SoundBank.Instance?.Play(Sfx.Slice, tip, 0.9f);
                    Holder.Haptic(0.8f, 0.12f);
                }
                var target = Hits[i].GetComponentInParent<MenuTarget>();
                if (target != null) target.Activate(swingDir);
            }
        }

        public void Deflect(Vector3 point)
        {
            SoundBank.Instance?.Play(Sfx.Deflect, point, 1f, Random.Range(0.9f, 1.1f));
            ShardSystem.Burst(point, Mats.Get(Mats.Blade), 5, Vector3.zero, 1.5f, 0.01f, 0.02f);
            if (Holder != null) Holder.Haptic(0.5f, 0.06f);
        }
    }
}
