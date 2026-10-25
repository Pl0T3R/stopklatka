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
        public WeaponSettings weapons = new();
        public HapticSettings haptics = new();
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

        [Header("Catching")]
        [Tooltip("While the grip is held with an empty hand, items flying faster than this (m/s) are caught when they reach the hand.")]
        public float catchMinSpeed = 0.5f;
        [Tooltip("Extra grab radius (m) per m/s of the item's speed, so fast items are easier to catch.")]
        public float catchRadiusPerSpeed = 0.012f;
        [Tooltip("Largest extra grab radius (m) for fast items.")]
        public float catchRadiusBonusMax = 0.08f;

        [Header("Fist")]
        [Tooltip("Show a black low-poly fist on an empty controller and let it punch.")]
        public bool fistEnabled = true;
        [Tooltip("Real hand speed (m/s) at which a punch kills an enemy.")]
        public float punchKillSpeed = 2f;
        [Tooltip("Real hand speed (m/s) at which a weaker punch stuns (knocks the weapon out). 0 = slower punches do nothing.")]
        public float punchStunSpeed = 1f;
        [Tooltip("Radius (m) of the fist's hit sphere around the knuckles.")]
        public float punchRadius = 0.07f;
        [Tooltip("Real seconds before the same hand can hit the same enemy again.")]
        public float punchRepeatSeconds = 0.5f;
    }

    [Serializable]
    public class CombatSettings
    {
        [Tooltip("Your bullets, game-time m/s.")]
        public float playerBulletSpeed = 26f;
        [Tooltip("Bullets disappear after this many game seconds.")]
        public float bulletLifetime = 8f;
        [Tooltip("Your bullet destroys an enemy bullet that passes this close (m).")]
        public float bulletInterceptRadius = 0.055f;
        [Tooltip("An enemy bullet this close (m) to a moving katana blade is cut.")]
        public float katanaCutRadius = 0.035f;

        [Header("Thrown objects")]
        [Tooltip("Off: a fast throw stuns the enemy and knocks its weapon out. On: above the kill speed it kills (bottles, mugs and ashtrays always kill).")]
        public bool killOnThrow;
        [Tooltip("Impact speed (game m/s) at which a thrown object stuns.")]
        public float throwStunSpeed = 2f;
        [Tooltip("Impact speed (game m/s) at which a thrown object kills, if killOnThrow is on.")]
        public float throwKillSpeed = 4.5f;
        [Tooltip("A thrown katana kills above its own kill speed even with killOnThrow off (it's a blade).")]
        public bool thrownBladeKills = true;
        public float thrownBladeKillSpeed = 2.5f;
        [Tooltip("Bottles, mugs and ashtrays break above this impact speed (game m/s).")]
        public float breakSpeed = 2.5f;
    }

    [Serializable]
    public class WeaponSettings
    {
        [Header("Pistol")]
        public int pistolRounds = 4;
        [Tooltip("Game seconds between shots.")]
        public float pistolCooldown = 0.3f;
        [Tooltip("Spread (degrees).")]
        public float pistolSpread = 0.25f;

        [Header("Shotgun")]
        public int shotgunShells = 2;
        public int shotgunPellets = 6;
        public float shotgunCooldown = 0.9f;
        public float shotgunSpread = 5.5f;

        [Header("SMG (short bursts)")]
        [Tooltip("Rounds in a full magazine (no reload).")]
        public int smgRounds = 15;
        [Tooltip("Rounds per trigger pull.")]
        public int smgRoundsPerBurst = 3;
        [Tooltip("Game seconds between rounds in a burst.")]
        public float smgRoundInterval = 0.07f;
        [Tooltip("Game seconds after a burst before the next one.")]
        public float smgBurstCooldown = 0.35f;
        public float smgSpread = 1.2f;
    }

    /// <summary>One controller vibration: strength, length (real seconds) and frequency (0..1).</summary>
    [Serializable]
    public struct HapticPulse
    {
        [Range(0f, 1f)] public float amplitude;
        public float seconds;
        [Range(0f, 1f)] public float frequency;

        public HapticPulse(float amplitude, float seconds, float frequency = 1f)
        {
            this.amplitude = amplitude;
            this.seconds = seconds;
            this.frequency = frequency;
        }
    }

    [Serializable]
    public class HapticSettings
    {
        public HapticPulse pistolShot = new(0.75f, 0.1f);
        public HapticPulse shotgunShot = new(1f, 0.18f);
        [Tooltip("Each round of an SMG burst.")]
        public HapticPulse smgRound = new(0.6f, 0.045f);
        [Tooltip("Empty gun: played as a few short sharp ticks, so it never feels like a shot.")]
        public HapticPulse emptyClick = new(0.55f, 0.018f, 0.5f);
        public int emptyClickTicks = 2;
        [Tooltip("Real seconds between the empty-gun ticks.")]
        public float emptyClickGap = 0.05f;
        public HapticPulse grab = new(0.35f, 0.05f);
        [Tooltip("Catching something that was flying.")]
        public HapticPulse catchItem = new(0.6f, 0.08f);
        public HapticPulse punch = new(0.9f, 0.12f);
        [Tooltip("A slower punch that only stuns.")]
        public HapticPulse punchStun = new(0.5f, 0.06f);
        public HapticPulse katanaCut = new(0.8f, 0.12f);
        [Tooltip("A bullet cut by the katana or stopped by something you hold.")]
        public HapticPulse block = new(0.6f, 0.08f);
        [Tooltip("You got hit (both hands).")]
        public HapticPulse playerHit = new(1f, 0.4f);
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
