using System.Collections.Generic;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// One Touch controller: grip grabs, trigger fires the held weapon, letting go throws. Throw speed
    /// comes from the fastest recent hand motion, measured in real time. Held items follow the
    /// controller in the rig's anchor update, so they never lag.
    ///
    /// Catching: keep the grip held with an empty hand and anything flying into the hand is caught
    /// (fast items get a slightly larger radius, and the check covers the item's path since the
    /// last frame, so it can't skip past the hand). An empty hand shows a fist that can punch (<see cref="Fist"/>).
    /// </summary>
    public class ControllerHand : MonoBehaviour
    {
        public static readonly List<ControllerHand> All = new();

        public bool IsLeft;

        public OVRInput.Controller Controller => IsLeft ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        public Grabbable Held { get; private set; }

        /// <summary>Hand velocity (m/s, real time) over the last few frames.</summary>
        public Vector3 Velocity { get; private set; }

        public Transform Anchor => IsLeft ? PlayerRig.Instance.LeftController : PlayerRig.Instance.RightController;
        Vector3 GrabPoint => Anchor.TransformPoint(new Vector3(0f, -0.01f, 0.02f));

        const int HistoryLength = 16;
        readonly float[] _t = new float[HistoryLength];
        readonly Vector3[] _p = new Vector3[HistoryLength];
        readonly Quaternion[] _r = new Quaternion[HistoryLength];
        int _count, _head;
        bool _grip, _trigger;
        float _hapticUntil;
        OVRCameraRig _subscribedRig;
        Fist _fist;

        // a short pattern of haptic ticks (the empty-gun click)
        HapticPulse _tick;
        int _ticksLeft;
        float _nextTickAt, _tickGap;

        void Awake()
        {
            // scenes built before the fist existed get it here
            if (!TryGetComponent(out _fist)) _fist = gameObject.AddComponent<Fist>();
        }

        void OnEnable()
        {
            All.Add(this);
            Subscribe();
        }

        void OnDisable()
        {
            All.Remove(this);
            if (_subscribedRig != null) _subscribedRig.UpdatedAnchors -= OnAnchorsUpdated;
            _subscribedRig = null;
            OVRInput.SetControllerVibration(0f, 0f, Controller);
        }

        void Subscribe()
        {
            var rig = PlayerRig.Instance != null ? PlayerRig.Instance.CameraRig : null;
            if (rig == null || rig == _subscribedRig) return;
            rig.UpdatedAnchors += OnAnchorsUpdated;
            _subscribedRig = rig;
        }

        void OnAnchorsUpdated(OVRCameraRig rig)
        {
            if (Held != null) Held.FollowHand(Anchor);
        }

        void Update()
        {
            if (PlayerRig.Instance == null) return;
            Subscribe();

            float grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, Controller);
            float trigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, Controller);
            bool gripNow = _grip ? grip > 0.35f : grip > 0.6f;
            bool triggerNow = _trigger ? trigger > 0.35f : trigger > 0.65f;

            Record();

            if (gripNow && !_grip) TryGrab();
            else if (gripNow && Held == null) TryCatch();
            if (!gripNow && _grip && Held != null) Release();
            _grip = gripNow;

            if (Held != null)
            {
                Held.FollowHand(Anchor);
                if (triggerNow && !_trigger) Held.OnTriggerDown(this);
                if (!triggerNow && _trigger) Held.OnTriggerUp(this);
            }
            _trigger = triggerNow;

            _fist.Tick(this);
            UpdateHaptics();
        }

        void Record()
        {
            _t[_head] = Time.unscaledTime;
            _p[_head] = GrabPoint;
            _r[_head] = Anchor.rotation;
            _head = (_head + 1) % HistoryLength;
            _count = Mathf.Min(_count + 1, HistoryLength);

            // short window (2 frames) so a punch reads its speed at once
            if (_count >= 3)
            {
                int a = Index(2), b = Index(0);
                float dt = _t[b] - _t[a];
                Velocity = dt > 1e-4f ? (_p[b] - _p[a]) / dt : Vector3.zero;
            }
        }

        void TryGrab()
        {
            var point = GrabPoint;
            Grabbable best = null;
            float bestScore = float.MaxValue;
            foreach (var g in Grabbable.All)
            {
                if (g.EnemyOwner != null || g == Held) continue;
                if ((g.transform.position - point).sqrMagnitude > 1f) continue;
                float radius = Tuning.GrabRadius + CatchBonus(g);
                float d = Vector3.Distance(g.ClosestPoint(point), point);
                // pick the closest relative to its own radius, so a fast item isn't beaten by a resting one
                if (d < radius && d / radius < bestScore) { bestScore = d / radius; best = g; }
            }
            if (best != null) Take(best, caught: false);
        }

        /// <summary>Grip held with an empty hand: catch anything that flies into the hand.</summary>
        void TryCatch()
        {
            var point = GrabPoint;
            float minSpeed = Tuning.CatchMinSpeed;
            float dt = Time.deltaTime;
            float now = Time.unscaledTime;
            foreach (var g in Grabbable.All)
            {
                if (g.EnemyOwner != null || g.Holder != null || g.Body.isKinematic) continue;
                if (now - g.ReleasedAt < 0.15f) continue;   // just thrown by the other hand
                var v = g.Body.linearVelocity;
                if (v.sqrMagnitude < minSpeed * minSpeed) continue;
                var travel = v * dt;
                float radius = Tuning.GrabRadius + CatchBonus(g);
                float reach = radius + travel.magnitude + 0.5f;   // 0.5: long items have their center far from their surface
                if ((g.transform.position - point).sqrMagnitude > reach * reach) continue;

                // the item's nearest surface point, swept back along this frame's motion,
                // so an item that passed the hand between two frames still counts
                var surface = g.ClosestPoint(point);
                var swept = ClosestOnSegment(point, surface - travel, surface);
                if (Vector3.Distance(swept, point) < radius)
                {
                    Take(g, caught: true);
                    return;
                }
            }
        }

        float CatchBonus(Grabbable g)
        {
            if (g.Body.isKinematic) return 0f;
            return Mathf.Min(g.Body.linearVelocity.magnitude * Tuning.CatchRadiusPerSpeed, Tuning.CatchRadiusBonusMax);
        }

        static Vector3 ClosestOnSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-10f) return a;
            return a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
        }

        void Take(Grabbable g, bool caught)
        {
            if (g.Holder != null) g.Holder.ForceDrop();   // pass between hands
            Held = g;
            g.OnGrab(this);
            g.FollowHand(Anchor);
            var h = GameConfig.Instance.haptics;
            Haptic(caught ? h.catchItem : h.grab);
            SoundBank.Instance?.Play(Sfx.Grab, GrabPoint, caught ? 0.8f : 0.5f, caught ? 0.8f : 1f, false);
            TimeController.Instance?.ActionPulse();
        }

        void Release()
        {
            EstimateThrow(out var velocity, out var angular);
            var g = Held;
            Held = null;
            g.OnRelease(velocity, angular);
            if (velocity.magnitude > GameConfig.Instance.time.throwPulseMinSpeed) TimeController.Instance?.ActionPulse();
        }

        /// <summary>Drops without throwing (another hand took it, or it broke).</summary>
        public void ForceDrop()
        {
            if (Held == null) return;
            var g = Held;
            Held = null;
            g.OnRelease(Vector3.zero, Vector3.zero);
        }

        // --- haptics (real time) ------------------------------------------------------

        public void Haptic(float amplitude, float seconds, float frequency = 1f)
        {
            OVRInput.SetControllerVibration(Mathf.Clamp01(frequency), Mathf.Clamp01(amplitude), Controller);
            _hapticUntil = Time.unscaledTime + seconds;
        }

        public void Haptic(HapticPulse pulse) => Haptic(pulse.amplitude, pulse.seconds, pulse.frequency);

        /// <summary>A few short separate ticks, e.g. the empty-gun click. Distinct from a shot's single buzz.</summary>
        public void HapticTicks(HapticPulse pulse, int count, float gap)
        {
            _tick = pulse;
            _tickGap = Mathf.Max(0.01f, gap);
            _ticksLeft = Mathf.Max(0, count - 1);
            _nextTickAt = Time.unscaledTime + pulse.seconds + _tickGap;
            Haptic(pulse);
        }

        /// <summary>Both controllers at once (getting hit).</summary>
        public static void HapticBoth(HapticPulse pulse)
        {
            foreach (var hand in All) hand.Haptic(pulse);
        }

        void UpdateHaptics()
        {
            float now = Time.unscaledTime;
            if (_hapticUntil > 0f && now > _hapticUntil)
            {
                OVRInput.SetControllerVibration(0f, 0f, Controller);
                _hapticUntil = 0f;
            }
            if (_ticksLeft > 0 && now >= _nextTickAt)
            {
                _ticksLeft--;
                _nextTickAt = now + _tick.seconds + _tickGap;
                Haptic(_tick);
            }
        }

        // Peak velocity over windows of 3 samples in the last ~100 ms. The grip is often released
        // after the hand has already started slowing down, so the peak matches the intent better.
        void EstimateThrow(out Vector3 velocity, out Vector3 angular)
        {
            velocity = Vector3.zero;
            angular = Vector3.zero;
            if (_count < 4) return;
            float now = Time.unscaledTime;
            float best = -1f;
            for (int k = 0; k < _count - 3; k++)
            {
                int i = Index(k), j = Index(k + 3);
                if (now - _t[i] > 0.1f) break;
                float dt = _t[i] - _t[j];
                if (dt < 1e-4f) continue;
                var v = (_p[i] - _p[j]) / dt;
                if (v.sqrMagnitude > best) { best = v.sqrMagnitude; velocity = v; }
            }
            velocity *= Tuning.ThrowBoost;
            if (velocity.magnitude > Tuning.MaxThrowSpeed) velocity = velocity.normalized * Tuning.MaxThrowSpeed;

            int a = Index(2), b = Index(0);
            float rdt = _t[b] - _t[a];
            if (rdt > 1e-4f)
            {
                var dq = _r[b] * Quaternion.Inverse(_r[a]);
                dq.ToAngleAxis(out float angle, out var axis);
                if (angle > 180f) angle -= 360f;
                if (!float.IsNaN(axis.x)) angular = Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad / rdt), 30f);
            }
        }

        /// <summary>k = 0 is the newest sample.</summary>
        int Index(int k) => (_head - 1 - k + HistoryLength * 2) % HistoryLength;
    }
}
