using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// "Time moves only when you move." Measures how fast the head and both controllers
    /// move and drives Time.timeScale from that. Tracking itself is never slowed: the rig,
    /// held weapons and UI use unscaled time.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class TimeController : MonoBehaviour
    {
        public enum Mode { Normal, MotionDriven, Fixed, Paused }

        public static TimeController Instance { get; private set; }

        public Mode CurrentMode { get; private set; } = Mode.Normal;

        /// <summary>The time scale applied this frame (0 when paused).</summary>
        public float Scale { get; private set; } = 1f;

        /// <summary>0..1, how much the player is moving right now (for audio and effects).</summary>
        public float Motion { get; private set; }

        float _fixedScale = 1f;
        float _boost, _boostUntil;
        Vector3 _lastHeadPos;
        Quaternion _lastHeadRot;
        bool _hasLast;
        float _smoothed = 1f;

        void Awake()
        {
            Instance = this;
            Time.fixedDeltaTime = Tuning.BasePhysicsStep;
        }

        public void SetMode(Mode mode, float fixedScale = 1f)
        {
            CurrentMode = mode;
            _fixedScale = fixedScale;
            if (mode == Mode.Normal) _smoothed = 1f;
        }

        /// <summary>Actions (shooting, throwing) push time forward for a moment, like in Superhot.</summary>
        public void Kick(float level, float seconds)
        {
            _boost = Mathf.Max(_boost, level);
            _boostUntil = Mathf.Max(_boostUntil, Time.unscaledTime + seconds);
        }

        void Update()
        {
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            var rig = PlayerRig.Instance;
            float motion = 0f;
            if (rig != null)
            {
                var head = rig.Head;
                if (_hasLast)
                {
                    float lin = Mathf.Max(0f, (head.position - _lastHeadPos).magnitude / dt - Tuning.MotionDeadzone);
                    float ang = Quaternion.Angle(_lastHeadRot, head.rotation) * Mathf.Deg2Rad / dt;
                    motion = Mathf.Max(lin / Tuning.HeadSpeedForFullTime, Mathf.Max(0f, ang - 0.15f) / Tuning.HeadTurnForFullTime);
                }
                _lastHeadPos = head.position;
                _lastHeadRot = head.rotation;
                _hasLast = true;

                float hands = Mathf.Max(
                    OVRInput.GetLocalControllerVelocity(OVRInput.Controller.LTouch).magnitude,
                    OVRInput.GetLocalControllerVelocity(OVRInput.Controller.RTouch).magnitude);
                motion = Mathf.Max(motion, Mathf.Max(0f, hands - Tuning.MotionDeadzone) / Tuning.HandSpeedForFullTime);
            }
            Motion = Mathf.Clamp01(motion);

            float target;
            switch (CurrentMode)
            {
                case Mode.MotionDriven:
                    target = Mathf.Lerp(Tuning.MinTimeScale, 1f, Mathf.SmoothStep(0f, 1f, Motion));
                    if (Time.unscaledTime < _boostUntil) target = Mathf.Max(target, _boost);
                    else _boost = 0f;
                    // speed up quickly, slow down a little more gently
                    float rate = target > _smoothed ? 14f : 6f;
                    _smoothed = Mathf.Lerp(_smoothed, target, 1f - Mathf.Exp(-rate * dt));
                    Scale = _smoothed;
                    break;
                case Mode.Fixed:
                    Scale = _fixedScale;
                    break;
                case Mode.Paused:
                    Scale = 0f;
                    break;
                default:
                    Scale = 1f;
                    break;
            }

            Time.timeScale = Scale;
            if (Scale > 0f) Time.fixedDeltaTime = Tuning.BasePhysicsStep * Mathf.Max(Scale, 0.01f);
        }

        void OnDisable()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = Tuning.BasePhysicsStep;
        }
    }
}
