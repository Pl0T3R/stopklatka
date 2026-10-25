using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// One Touch controller: grip grabs (also catches things in mid-air), trigger fires the held
    /// weapon, letting go throws. Throw speed comes from the fastest recent hand motion, measured
    /// in real time. Held items follow the controller in the rig's anchor update, so they never lag.
    /// </summary>
    public class ControllerHand : MonoBehaviour
    {
        public bool IsLeft;

        public OVRInput.Controller Controller => IsLeft ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        public Grabbable Held { get; private set; }

        Transform Anchor => IsLeft ? PlayerRig.Instance.LeftController : PlayerRig.Instance.RightController;
        Vector3 GrabPoint => Anchor.TransformPoint(new Vector3(0f, -0.01f, 0.02f));

        const int HistoryLength = 16;
        readonly float[] _t = new float[HistoryLength];
        readonly Vector3[] _p = new Vector3[HistoryLength];
        readonly Quaternion[] _r = new Quaternion[HistoryLength];
        int _count, _head;
        bool _grip, _trigger;
        float _hapticUntil;
        OVRCameraRig _subscribedRig;

        void OnEnable() => Subscribe();

        void OnDisable()
        {
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
            if (!gripNow && _grip && Held != null) Release();
            _grip = gripNow;

            if (Held != null)
            {
                Held.FollowHand(Anchor);
                if (triggerNow && !_trigger) Held.OnTriggerDown(this);
                if (!triggerNow && _trigger) Held.OnTriggerUp(this);
            }
            _trigger = triggerNow;

            if (_hapticUntil > 0f && Time.unscaledTime > _hapticUntil)
            {
                OVRInput.SetControllerVibration(0f, 0f, Controller);
                _hapticUntil = 0f;
            }
        }

        void Record()
        {
            _t[_head] = Time.unscaledTime;
            _p[_head] = GrabPoint;
            _r[_head] = Anchor.rotation;
            _head = (_head + 1) % HistoryLength;
            _count = Mathf.Min(_count + 1, HistoryLength);
        }

        void TryGrab()
        {
            var point = GrabPoint;
            Grabbable best = null;
            float bestDist = Tuning.GrabRadius;
            foreach (var g in Grabbable.All)
            {
                if (g.EnemyOwner != null || g == Held) continue;
                if ((g.transform.position - point).sqrMagnitude > 1f) continue;
                foreach (var col in g.GetComponentsInChildren<Collider>())
                {
                    float d = Vector3.Distance(col.ClosestPoint(point), point);
                    if (d < bestDist) { bestDist = d; best = g; }
                }
            }
            if (best == null) return;
            if (best.Holder != null) best.Holder.ForceDrop();   // pass between hands
            Held = best;
            best.OnGrab(this);
            best.FollowHand(Anchor);
            Haptic(0.35f, 0.05f);
            SoundBank.Instance?.Play(Sfx.Grab, point, 0.5f, 1f, false);
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

        public void Haptic(float amplitude, float seconds)
        {
            OVRInput.SetControllerVibration(1f, Mathf.Clamp01(amplitude), Controller);
            _hapticUntil = Time.unscaledTime + seconds;
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
