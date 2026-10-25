using System;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// Every gameplay number in one asset (Assets/Stopklatka/Resources/GameConfig.asset, created by
    /// Stopklatka > Build Game). Edit it in the Inspector, also while playing. Code reads it through
    /// <see cref="Tuning"/> and <see cref="GameMode"/>. If the asset is missing, these defaults are used.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Stopklatka/Game Config")]
    public class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "GameConfig";

        public TimeSettings time = new();
        public HandSettings player = new();
        public CombatSettings combat = new();
        public EnemySettings enemies = new();
        [Tooltip("Numbers for playing at school (big rooms and corridors).")]
        public ModeProfile school = ModeProfile.CreateSchool();
        [Tooltip("Numbers for playing at home (a whole house, smaller rooms).")]
        public ModeProfile home = ModeProfile.CreateHome();
        public PerformanceSettings performance = new();
        public DebugSettings debug = new();

        static GameConfig _instance;

        public static GameConfig Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = Resources.Load<GameConfig>(ResourcePath);
                if (_instance == null)
                {
                    Debug.LogWarning("[Stopklatka] GameConfig asset missing, using defaults. Run Stopklatka > Build Game.");
                    _instance = CreateInstance<GameConfig>();
                }
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;
    }

    [Serializable]
    public class TimeSettings
    {
        [Tooltip("Time scale when you stand completely still.")]
        [Range(0.005f, 0.3f)] public float minTimeScale = 0.03f;
        [Range(0.3f, 1f)] public float maxTimeScale = 1f;

        [Tooltip("Head movement (m/s) that runs time at full speed.")]
        public float headSpeedForFullTime = 1.2f;
        [Tooltip("Hand movement (m/s) that runs time at full speed.")]
        public float handSpeedForFullTime = 2.6f;
        [Tooltip("Head turning (rad/s) that would run time at full speed, before the weight below.")]
        public float headTurnForFullTime = 2.8f;
        [Tooltip("Turning the head counts less than moving: 0.3 means a fast turn alone gives about a third.")]
        [Range(0f, 1f)] public float headTurnWeight = 0.3f;
        [Tooltip("Movement below this (m/s) is tracking jitter and ignored.")]
        public float motionDeadzone = 0.06f;
        [Tooltip("Turning below this (rad/s) is ignored.")]
        public float turnDeadzone = 0.15f;

        [Tooltip("Maps movement (0..1) to time (0 = minimum, 1 = maximum).")]
        public AnimationCurve response = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Seconds to catch up when speeding up (short = time reacts at once).")]
        public float speedUpSmoothing = 0.05f;
        [Tooltip("Seconds to settle when slowing down (a bit longer, no flicker).")]
        public float slowDownSmoothing = 0.18f;

        [Tooltip("Shooting, throwing and grabbing push time to this scale for a moment.")]
        [Range(0f, 1f)] public float actionPulseScale = 1f;
        [Tooltip("How long the push lasts (real seconds).")]
        public float actionPulseSeconds = 0.2f;
        [Tooltip("A throw counts as an action above this hand speed (m/s).")]
        public float throwPulseMinSpeed = 1.5f;
    }

    [Serializable]
    public class HandSettings
    {
        [Tooltip("How close (m) the controller must be to grab something.")]
        public float grabRadius = 0.13f;
        [Tooltip("Thrown objects fly this much faster than the hand moved.")]
        public float throwBoost = 1.1f;
        public float maxThrowSpeed = 18f;
    }

    [Serializable]
    public class CombatSettings
    {
        [Tooltip("Your bullets, game-time m/s.")]
        public float playerBulletSpeed = 26f;
        [Tooltip("Bullets disappear after this many game seconds.")]
        public float bulletLifetime = 8f;
    }

    [Serializable]
    public class EnemySettings
    {
        public float sightRange = 10f;
        public float punchRange = 0.85f;
        [Tooltip("Seconds an armed enemy needs to aim after seeing you: at wave 1, and from wave 9 on.")]
        public float reactionFirstWave = 1.3f, reactionLateWave = 0.55f;
        [Tooltip("Seconds between shots: at wave 1, and from wave 9 on.")]
        public float fireIntervalFirstWave = 3.0f, fireIntervalLateWave = 1.6f;
        [Tooltip("Share of enemies with a gun: at wave 1, and from wave 7 on.")]
        [Range(0f, 1f)] public float armedFirstWave = 0.55f, armedLateWave = 0.85f;
        [Tooltip("Share of armed enemies with a shotgun, from wave 3.")]
        [Range(0f, 1f)] public float shotgunShare = 0.25f;
    }

    [Serializable]
    public class PerformanceSettings
    {
        [Tooltip("Display refresh rate to ask for. 90 is the default; 120 if the game holds it on the headset.")]
        public float targetRefreshRate = 90f;
        public OVRManager.FoveatedRenderingLevel foveation = OVRManager.FoveatedRenderingLevel.High;
        public bool dynamicFoveation = true;
    }

    [Serializable]
    public class DebugSettings
    {
        [Tooltip("Time scale gauge on the left wrist. Off in normal play.")]
        public bool wristTimeIndicator;
        [Tooltip("FPS readout at start (left thumbstick click toggles it anyway).")]
        public bool performanceReadout;
    }
}
