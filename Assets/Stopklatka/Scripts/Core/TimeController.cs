using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// "Time moves only when you move" (the TimeManager of GAME_SPEC.md).
    ///
    /// Each frame it measures, from the rig's transforms and real time:
    ///   - head movement (m/s), weight 1,
    ///   - movement of either controller (m/s), weight 1,
    ///   - head turning (rad/s), weight 0.3 by default, so looking around speeds time up less than moving.
    /// The strongest of the three, passed through the response curve, sets the target time scale
    /// (0.03..1). SmoothDamp follows it: fast when speeding up, a bit slower when slowing down.
    /// Shooting, throwing and grabbing push time to full speed for 0.2 s (<see cref="ActionPulse"/>).
    ///
    /// Time.fixedDeltaTime = (1 / display refresh rate) * time scale, so physics keeps one step per
    /// displayed frame and slows down with everything else. The player is never slowed: tracking,
    /// held items and UI run on real time.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class TimeController : MonoBehaviour
    {
        public enum Mode { Normal, MotionDriven, Fixed, Paused }

        public static TimeController Instance { get; private set; }

        public Mode CurrentMode { get; private set; } = Mode.Normal;

        /// <summary>The time scale applied this frame (0 when paused).</summary>
        public float Scale { get; private set; } = 1f;

        /// <summary>0..1, how much the player is moving right now (for audio, effects and the wrist gauge).</summary>
        public float Motion { get; private set; }

        /// <summary>Where the time scale is heading before smoothing.</summary>
        public float Target { get; private set; } = 1f;

        float _fixedScale = 1f;
        float _pulse, _pulseUntil;
        float _smoothed = 1f, _velocity;
        readonly Tracker _head = new(), _left = new(), _right = new();

        /// <summary>Position and rotation of one tracked transform in the previous frame.</summary>
        sealed class Tracker
        {
            Vector3 _pos;
            Quaternion _rot;
            bool _has;

            public void Measure(Transform t, float dt, out float speed, out float turn)
            {
                speed = 0f;
                turn = 0f;
                if (_has)
                {
                    speed = (t.position - _pos).magnitude / dt;
                    turn = Quaternion.Angle(_rot, t.rotation) * Mathf.Deg2Rad / dt;
                }
                _pos = t.position;
                _rot = t.rotation;
                _has = true;
            }

            public void Reset() => _has = false;
        }

        void Awake()
        {
            Instance = this;
            Time.fixedDeltaTime = Tuning.BasePhysicsStep;
        }

        public void SetMode(Mode mode, float fixedScale = 1f)
        {
            CurrentMode = mode;
            _fixedScale = fixedScale;
            if (mode == Mode.Normal) { _smoothed = 1f; _velocity = 0f; }
        }

        /// <summary>Pushes time to at least [level] for [seconds] of real time.</summary>
        public void Kick(float level, float seconds)
        {
            _pulse = Mathf.Max(_pulse, level);
            _pulseUntil = Mathf.Max(_pulseUntil, Time.unscaledTime + seconds);
        }

        /// <summary>The short push for a player action (shot, throw, grab), as set in GameConfig.</summary>
        public void ActionPulse()
        {
            var c = GameConfig.Instance.time;
            Kick(c.actionPulseScale, c.actionPulseSeconds);
        }

        void Update()
        {
            var c = GameConfig.Instance.time;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            Motion = MeasureMotion(c, dt);

            switch (CurrentMode)
            {
                case Mode.MotionDriven:
                    float min = Mathf.Min(c.minTimeScale, c.maxTimeScale);
                    float shaped = Mathf.Clamp01(c.response != null && c.response.length > 0 ? c.response.Evaluate(Motion) : Motion);
                    Target = Mathf.Lerp(min, c.maxTimeScale, shaped);
                    if (Time.unscaledTime < _pulseUntil) Target = Mathf.Max(Target, _pulse);
                    else _pulse = 0f;
                    // speed up quickly, settle a bit more slowly; real time so it never stalls itself
                    float smoothing = Target > _smoothed ? c.speedUpSmoothing : c.slowDownSmoothing;
                    _smoothed = Mathf.SmoothDamp(_smoothed, Target, ref _velocity, Mathf.Max(0.001f, smoothing), Mathf.Infinity, dt);
                    Scale = Mathf.Clamp(_smoothed, min, c.maxTimeScale);
                    break;
                case Mode.Fixed:
                    Scale = Target = _fixedScale;
                    break;
                case Mode.Paused:
                    Scale = Target = 0f;
                    break;
                default:
                    Scale = Target = 1f;
                    break;
            }

            Time.timeScale = Scale;
            if (Scale > 0f) Time.fixedDeltaTime = Tuning.BasePhysicsStep * Mathf.Max(Scale, 0.01f);
        }

        float MeasureMotion(TimeSettings c, float dt)
        {
            var rig = PlayerRig.Instance;
            if (rig == null) return 0f;

            _head.Measure(rig.Head, dt, out float headSpeed, out float headTurn);
            _left.Measure(rig.LeftController, dt, out float leftSpeed, out _);
            _right.Measure(rig.RightController, dt, out float rightSpeed, out _);

            float move = Mathf.Max(0f, headSpeed - c.motionDeadzone) / Mathf.Max(0.01f, c.headSpeedForFullTime);
            float hands = Mathf.Max(0f, Mathf.Max(leftSpeed, rightSpeed) - c.motionDeadzone) / Mathf.Max(0.01f, c.handSpeedForFullTime);
            float turn = c.headTurnWeight * Mathf.Max(0f, headTurn - c.turnDeadzone) / Mathf.Max(0.01f, c.headTurnForFullTime);

            // tracking jumps (recentering, a lost controller) would read as huge speeds: ignore them
            if (headSpeed > 8f || leftSpeed > 25f || rightSpeed > 25f) return Motion;
            return Mathf.Clamp01(Mathf.Max(move, Mathf.Max(hands, turn)));
        }

        /// <summary>After a recenter or a scene change, so the jump doesn't count as movement.</summary>
        public void ResetTracking()
        {
            _head.Reset();
            _left.Reset();
            _right.Reset();
        }

        void OnDisable()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = Tuning.BasePhysicsStep;
        }
    }
}
