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
        [Tooltip("Enemy death: shards, big chunks, the katana cut in half and the fist's glowing cracks.")]
        public ShatterSettings shatter = new();
        [Tooltip("The 3 levels (waves of enemies), death and restart. Off = the old endless waves.")]
        public LevelSettings levels = new();
        [Tooltip("White virtual props of the levels (GALERIA, ULICA, HOL), placed only where they can't hide a real obstacle.")]
        public PropSettings props = new();
        [Tooltip("The menu of black cubes to grab (START, level select, STYL, TRYB, WYJŚCIE).")]
        public MenuSettings menu = new();
        [Tooltip("World-space texts: TextMeshPro or the legacy fallback, where titles appear and how big.")]
        public UiSettings ui = new();
        [Tooltip("Numbers for playing at school (big rooms and corridors).")]
        public ModeProfile school = ModeProfile.CreateSchool();
        [Tooltip("Numbers for playing at home (a whole house, smaller rooms).")]
        public ModeProfile home = ModeProfile.CreateHome();
        public VisualSettings visuals = new();
        public SoundSettings sound = new();
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
        [Tooltip("Seconds an armed enemy needs to aim after seeing you (its arm rises during this time): at wave 1, and from wave 9 on. Levels use their wave difficulty instead of the wave number.")]
        public float reactionFirstWave = 1.3f, reactionLateWave = 0.55f;
        [Tooltip("Seconds between shots: at wave 1, and from wave 9 on.")]
        public float fireIntervalFirstWave = 3.0f, fireIntervalLateWave = 1.6f;
        [Tooltip("Endless waves: share of enemies with a gun at wave 1, and from wave 7 on.")]
        [Range(0f, 1f)] public float armedFirstWave = 0.55f, armedLateWave = 0.85f;
        [Tooltip("Endless waves: share of armed enemies with a shotgun, from shotgunFromWave on.")]
        [Range(0f, 1f)] public float shotgunShare = 0.25f;
        public int shotgunFromWave = 3;
        [Tooltip("Endless waves: share of armed enemies with an SMG, from smgFromWave on.")]
        [Range(0f, 1f)] public float smgShare = 0.25f;
        public int smgFromWave = 4;

        [Header("Aiming (dodgeable)")]
        [Tooltip("Game seconds of a visible tell before every shot: the enemy stops, the arm locks on and the muzzle glints.")]
        public float aimTellSeconds = 0.35f;
        [Tooltip("The arm must point within this angle (degrees) of your head before the tell starts.")]
        public float aimLockAngle = 12f;
        [Tooltip("Extra aim error (degrees) on every enemy shot.")]
        public float aimInaccuracy = 1.6f;

        [Header("Enemy SMG bursts")]
        [Tooltip("Rounds in one enemy SMG burst.")]
        public int smgBurstRounds = 3;
        [Tooltip("Game seconds between rounds of an enemy burst (slower than yours, so the burst can be dodged).")]
        public float smgBurstInterval = 0.13f;
        [Tooltip("Spread (degrees) of each round of an enemy burst. All rounds aim where your head was when the burst started.")]
        public float smgBurstSpread = 2.5f;

        [Header("Punching (unarmed enemies)")]
        [Tooltip("Game seconds the arm is pulled back before the punch (the visible wind-up).")]
        public float punchWindupSeconds = 0.5f;
        [Tooltip("Game seconds of the strike; the hit lands at its end.")]
        public float punchStrikeSeconds = 0.12f;
        [Tooltip("Game seconds to recover before the next move.")]
        public float punchRecoverSeconds = 0.45f;

        [Header("Defensive pose")]
        [Tooltip("Enemies raise their arms for a moment when your bullet passes close or a neighbour dies. Armed ones lose their aim.")]
        public bool defensivePose = true;
        [Tooltip("Game seconds the guard lasts.")]
        public float guardSeconds = 0.7f;
        [Tooltip("Your bullet passing this close (m) to an enemy's chest makes it flinch.")]
        public float guardBulletDistance = 0.6f;
        [Tooltip("An enemy dying this close (m) makes its neighbours flinch.")]
        public float guardNeighbourDistance = 3f;

        [Header("Spawn points")]
        [Tooltip("How often an enemy appears at a scanned door frame on your floor (\"walks in through the door\"). Distance limits always apply.")]
        [Range(0f, 1f)] public float doorSpawnChance = 0.6f;
        [Tooltip("How far (m) into the room from the door frame an enemy appears.")]
        public float doorSpawnOffset = 0.6f;
        [Tooltip("How often a non-door spawn prefers a spot outside your view.")]
        [Range(0f, 1f)] public float outOfViewChance = 0.4f;
        [Tooltip("Out of view = at least this many degrees from where you look.")]
        public float outOfViewAngle = 65f;
    }

    [Serializable]
    public class ShatterSettings
    {
        [Header("Small shards (any kill)")]
        [Tooltip("Small red shards per kill: a random count between these.")]
        public int shardsMin = 30, shardsMax = 60;
        [Tooltip("Shard size range (m).")]
        public float shardSizeMin = 0.02f, shardSizeMax = 0.08f;
        [Tooltip("Share of shards that glow yellow-orange (broken crystal core).")]
        [Range(0f, 1f)] public float glowShardShare = 0.15f;
        [Tooltip("Share of the red shards in the darker red.")]
        [Range(0f, 1f)] public float darkShardShare = 0.33f;
        [Tooltip("Game seconds before a shard shrinks away: a random time between these.")]
        public float shardLifetimeMin = 3f, shardLifetimeMax = 5f;
        [Tooltip("Speed (game m/s) of the shards bursting from the hit point along the hit.")]
        public float shardSpeed = 2.2f;
        [Tooltip("Random extra speed (game m/s) in every direction.")]
        public float shardSpread = 1.4f;
        [Tooltip("Body parts this close (m) to the hit turn into shards; the rest break off as big chunks.")]
        public float pulverizeRadius = 0.2f;

        [Header("Big chunks")]
        [Tooltip("Game seconds the big chunks (limbs, torso) lie on the floor before shrinking away.")]
        public float chunkLifetime = 4f;
        [Tooltip("How hard (game m/s) the hit pushes the chunks.")]
        public float chunkPush = 1f;
        [Tooltip("Most chunk and cap pieces alive at once; the oldest debris goes first.")]
        public int maxPieces = 72;

        [Header("Flash")]
        [Tooltip("Size (m) and game seconds of the yellow flash at the hit.")]
        public float flashSize = 0.18f;
        public float flashSeconds = 0.1f;

        [Header("Katana: cut in half")]
        [Tooltip("The katana cuts enemies in half with a glowing cross-section. Off: a katana kill shatters like any other.")]
        public bool katanaCutsInHalf = true;
        [Tooltip("Game seconds before the two halves break into shards.")]
        public float cutHalfSeconds = 1.3f;
        [Tooltip("Speed (game m/s) at which the halves move apart, across the cut.")]
        public float cutSeparationSpeed = 0.7f;
        [Tooltip("Speed (game m/s) the upper half takes from the swing.")]
        public float cutSwingSpeed = 1.1f;
        [Tooltip("Small shards sprayed from the cut line.")]
        public int cutSprayShards = 26;
        [Tooltip("Shards each half breaks into at the end.")]
        public int cutShardsPerHalf = 18;

        [Header("Fist: glowing cracks")]
        [Tooltip("A punch kill shows glowing cracks spreading from the fist before the enemy shatters.")]
        public bool punchCracks = true;
        [Tooltip("Game seconds the cracks spread before the shatter.")]
        public float crackSeconds = 0.25f;
        [Tooltip("Cracks per punch (each may branch once).")]
        public int crackCount = 6;
        [Tooltip("Length (m) of a crack.")]
        public float crackLength = 0.15f;
    }

    /// <summary>One wave of a level.</summary>
    [Serializable]
    public class WaveDefinition
    {
        [Tooltip("Enemies in this wave.")]
        public int enemies = 4;
        [Tooltip("At most this many alive at once (also capped by the mode: school 6, home 4, and never above 6).")]
        public int maxAlive = 3;
        [Tooltip("Share of enemies with a gun.")]
        [Range(0f, 1f)] public float armedShare = 0.5f;
        [Tooltip("Of the armed ones: share with a shotgun.")]
        [Range(0f, 1f)] public float shotgunShare;
        [Tooltip("Of the armed ones: share with an SMG.")]
        [Range(0f, 1f)] public float smgShare;
        [Tooltip("1..9: how fast enemies aim and how often they shoot (like the endless wave number).")]
        [Range(1, 9)] public int difficulty = 1;

        public WaveDefinition() { }

        public WaveDefinition(int enemies, int maxAlive, float armedShare, float shotgunShare, float smgShare, int difficulty)
        {
            this.enemies = enemies;
            this.maxAlive = maxAlive;
            this.armedShare = armedShare;
            this.shotgunShare = shotgunShare;
            this.smgShare = smgShare;
            this.difficulty = difficulty;
        }
    }

    /// <summary>Which white props a level puts into the scanned rooms (STYLE_GUIDE 9).</summary>
    public enum PropTheme
    {
        [Tooltip("By level number: 1 GALERIA, 2 ULICA, 3 HOL, then again.")]
        Auto,
        None,
        Galeria,
        Ulica,
        Hol,
    }

    /// <summary>A level: a name, its props and its waves.</summary>
    [Serializable]
    public class LevelDefinition
    {
        [Tooltip("Shown when the level starts (Polish, plain letters).")]
        public string name = "POZIOM";
        [Tooltip("The white props of this level (GameConfig props). Auto: by level number.")]
        public PropTheme theme = PropTheme.Auto;
        [Tooltip("How often enemies walk in through a scanned door frame in this level (above 0 overrides). 0 or less: the theme's default (HOL: props.elevatorDoorSpawnChance, others: enemies.doorSpawnChance).")]
        [Range(-1f, 1f)] public float doorSpawnChance = -1f;
        public WaveDefinition[] waves = { new() };

        public LevelDefinition() { }

        public LevelDefinition(string name, PropTheme theme, params WaveDefinition[] waves)
        {
            this.name = name;
            this.theme = theme;
            this.waves = waves;
        }

        /// <summary>The theme, with Auto resolved by the 1-based level number.</summary>
        public PropTheme ThemeFor(int level)
        {
            if (theme != PropTheme.Auto) return theme;
            int i = (Mathf.Max(1, level) - 1) % 3;
            return i == 0 ? PropTheme.Galeria : i == 1 ? PropTheme.Ulica : PropTheme.Hol;
        }
    }

    [Serializable]
    public class LevelSettings
    {
        [Tooltip("On: START plays the levels below in order. Off: the old endless waves (FALA 1, 2, 3...).")]
        public bool useLevels = true;
        [Tooltip("Real seconds from your death to the level restart.")]
        public float restartDelay = 1.5f;
        [Tooltip("Real seconds the level name stays before enemies come.")]
        public float introSeconds = 2f;
        [Tooltip("Real seconds between two waves of a level.")]
        public float waveGap = 1.2f;
        [Tooltip("Game seconds between two enemies appearing: a random time between these.")]
        public float spawnIntervalMin = 1f, spawnIntervalMax = 2.2f;
        [Tooltip("Times the \"STOP. / KLATKA.\" chant repeats when a level is cleared.")]
        public int chantRepeats = 2;
        [Tooltip("Pickups (guns, katana, throwables) placed around you at a level start, besides a pistol if your hands are empty.")]
        public int pickupsPerLevel = 2;

        public LevelDefinition[] list = DefaultLevels();

        /// <summary>Three short levels of about 30 to 90 seconds, getting harder.</summary>
        public static LevelDefinition[] DefaultLevels() => new[]
        {
            new LevelDefinition("GALERIA", PropTheme.Galeria,
                new WaveDefinition(3, 2, 0.35f, 0f, 0f, 1),
                new WaveDefinition(4, 3, 0.5f, 0f, 0f, 2),
                new WaveDefinition(5, 3, 0.6f, 0.25f, 0f, 3)),
            new LevelDefinition("ULICA", PropTheme.Ulica,
                new WaveDefinition(4, 3, 0.6f, 0.2f, 0.15f, 3),
                new WaveDefinition(5, 4, 0.65f, 0.2f, 0.25f, 4),
                new WaveDefinition(6, 4, 0.7f, 0.25f, 0.25f, 5)),
            new LevelDefinition("HOL", PropTheme.Hol,
                new WaveDefinition(5, 4, 0.7f, 0.25f, 0.25f, 5),
                new WaveDefinition(6, 5, 0.75f, 0.25f, 0.3f, 6),
                new WaveDefinition(7, 6, 0.8f, 0.25f, 0.35f, 8)),
        };
    }

    /// <summary>
    /// The white props of the levels (stage 6). MR safety first: free-standing props only cover
    /// scanned furniture (never larger than its footprint), wall props stay flat on free wall areas,
    /// nothing in open floor space or near where you stand when a level starts.
    /// </summary>
    [Serializable]
    public class PropSettings
    {
        [Tooltip("Off: levels have no props at all (the scanned room only).")]
        public bool enabled = true;
        [Tooltip("Most props per level (each is 1 or 2 draw calls).")]
        public int maxProps = 14;
        [Tooltip("Most props made from scanned furniture (plinths, statues, cars, the counter).")]
        public int maxFurnitureProps = 6;
        [Tooltip("Most props on walls (window light, half columns, lamp posts, rails, trims, elevator frames).")]
        public int maxWallProps = 8;

        [Header("Safety")]
        [Tooltip("No prop closer than this (m, horizontally) to where you stand when the level starts.")]
        public float minPlayerDistance = 1.5f;
        [Tooltip("Free space (m) kept on both sides of door frames, windows and other things on a wall.")]
        public float wallMargin = 0.2f;
        [Tooltip("Deepest a wall prop may stick out of the wall (m).")]
        [Range(0.02f, 0.2f)] public float maxWallDepth = 0.18f;
        [Tooltip("Furniture counts only if its bottom is this close (m) to your floor; walls and doors likewise.")]
        public float floorTolerance = 0.3f;
        [Tooltip("Highest point (m above your floor) any prop may reach.")]
        public float maxHeight = 2.3f;

        [Header("Shapes")]
        [Tooltip("Height (m) of a statue on a plinth (smaller on small furniture).")]
        public float statueHeight = 0.75f;
        [Tooltip("Width (m) of a window light panel on a wall.")]
        public float windowWidth = 0.7f;
        [Tooltip("How far (m) the pale light rectangle under a window reaches onto the floor.")]
        public float floorLightDepth = 0.9f;
        [Tooltip("Space (m) between two wall props on the same wall.")]
        public float wallSpacing = 0.9f;

        [Header("Transitions (real seconds)")]
        [Tooltip("Props rise from the floor when a level starts.")]
        public float revealSeconds = 1.2f;
        [Tooltip("Props sink away when a level is cleared.")]
        public float fadeSeconds = 0.9f;

        [Header("HOL")]
        [Tooltip("Door spawn chance in HOL levels (enemies walk out of the elevator frames).")]
        [Range(0f, 1f)] public float elevatorDoorSpawnChance = 0.9f;

        [Header("Haze (STYLE_GUIDE 4)")]
        [Tooltip("Far props fade a little toward a light cyan. Only the props: the real room can't have fog.")]
        public bool haze = true;
        public Color hazeColor = new(0.867f, 0.937f, 0.957f, 1f);   // #DDEFF4
        [Tooltip("Haze starts (m from your eyes) ...")]
        public float hazeStart = 3f;
        [Tooltip("... and is strongest here (m).")]
        public float hazeEnd = 15f;
        [Tooltip("How much of the haze color the farthest props take.")]
        [Range(0f, 1f)] public float hazeStrength = 0.55f;
    }

    /// <summary>The menu (GAME_SPEC 8): black cubes in an arc in front of you, grabbed with the grip.</summary>
    [Serializable]
    public class MenuSettings
    {
        [Tooltip("On: black cubes to grab. Off: the old floating crystals you shoot (START, STYL, TRYB).")]
        public bool useCubes = true;
        [Tooltip("Edge length (m) of a menu cube, and of the START cube.")]
        public float cubeSize = 0.13f, startCubeSize = 0.16f;
        [Tooltip("Horizontal distance (m) from your head to the cubes. Keep it within arm's reach.")]
        public float radius = 0.55f;
        [Tooltip("How far (m) below your eyes the upper row (levels) and the lower row (START, STYL, TRYB, WYJŚCIE) float.")]
        public float upperRowDrop = 0.3f, lowerRowDrop = 0.55f;
        [Tooltip("Degrees between the level cubes, and between START and STYL / TRYB.")]
        public float levelSpacing = 24f, toggleSpacing = 38f;
        [Tooltip("Degrees to the right where WYJŚCIE floats (away from START).")]
        public float exitAngle = 76f;
        [Tooltip("Extra grab radius (m) around a cube.")]
        public float grabRadius = 0.06f;
        [Tooltip("Real seconds to grab WYJŚCIE a second time to really quit.")]
        public float quitConfirmSeconds = 4f;
    }

    /// <summary>World-space texts (GAME_SPEC 6, STYLE_GUIDE 10).</summary>
    [Serializable]
    public class UiSettings
    {
        [Tooltip("TextMeshPro texts (needs TMP Essential Resources, imported by Build Game). Off, or the font missing: the legacy TextMesh texts.")]
        public bool useTextMeshPro = true;
        [Tooltip("Resources path of the TMP font (TMP Essentials). Its dynamic fallback adds Polish letters.")]
        public string tmpFont = "Fonts & Materials/LiberationSans SDF";
        [Tooltip("Resources path of the TMP material with underlay (drop shadow) and outline turned on.")]
        public string tmpMaterial = "Fonts & Materials/LiberationSans SDF - Drop Shadow";
        public Color shadowColor = new(0.04f, 0.04f, 0.06f, 0.6f);

        [Header("Size (font height in m per m of distance)")]
        public float titleSize = 0.07f;
        public float subtitleSize = 0.03f;
        [Tooltip("Font height (m) of the labels over the menu cubes.")]
        public float labelSize = 0.022f;

        [Header("Where titles appear (world-locked, never glued to your face)")]
        [Tooltip("On the real wall in front of you when it is between these distances (m) ...")]
        public float wallMinDistance = 1.5f, wallMaxDistance = 6f;
        [Tooltip("... otherwise this far (m) ahead.")]
        public float defaultDistance = 2.5f;
        [Tooltip("Height (m) above your eyes.")]
        public float aboveEyes = 0.15f;
        [Tooltip("A new title stays at the old spot if that spot is within this angle (degrees) of your view.")]
        public float keepAngle = 25f;
        [Tooltip("A text that stays outside this angle (degrees) of your view for reanchorDelay real seconds moves in front of you again.")]
        public float reanchorAngle = 70f;
        public float reanchorDelay = 1.2f;
    }

    [Serializable]
    public class VisualSettings
    {
        [Tooltip("Longest red streak behind an enemy bullet (m). STYLE_GUIDE: 3 to 6 m, so you can read where it goes in slow motion.")]
        public float enemyStreakLength = 3.5f;
        [Tooltip("Longest dark streak behind your bullets (m).")]
        public float playerStreakLength = 1f;
    }

    /// <summary>Sound (GAME_SPEC 7). Not "AudioSettings": that name belongs to UnityEngine.</summary>
    [Serializable]
    public class SoundSettings
    {
        [Tooltip("Route sounds through Resources/Audio/StopklatkaMixer (Stopklatka > 5. Create Audio Mixer). Off, or no mixer: the pitch is set on each sound instead.")]
        public bool useMixer = true;
        [Tooltip("Pitch of world sounds when time nearly stops.")]
        [Range(0.1f, 1f)] public float slowPitch = 0.35f;
        [Tooltip("Pitch of world sounds at full speed.")]
        [Range(0.5f, 1.5f)] public float fullPitch = 1f;
        [Tooltip("The low hum while time stands still (fades out as time speeds up).")]
        [Range(0f, 1f)] public float stillHumVolume = 0.2f;
        [Tooltip("The quiet ambient at full movement (fades in as you move).")]
        [Range(0f, 1f)] public float motionAmbientVolume = 0.1f;
        [Tooltip("Seconds the ambient takes to follow your movement.")]
        public float ambientResponse = 0.35f;
        [Tooltip("Overall ambient level in the mixer (dB, 0 = as is, -80 = silent).")]
        [Range(-80f, 10f)] public float ambientDb = 0f;
    }

    /// <summary>When PerformanceManager scales the resolution itself (stage 7).</summary>
    public enum ResolutionFallbackMode
    {
        /// <summary>Never: only OVRManager's dynamic resolution.</summary>
        Off,
        /// <summary>Only when OVRManager's dynamic resolution is unavailable on this device or build.</summary>
        Auto,
        /// <summary>Always (turns OVRManager's dynamic resolution off). For testing.</summary>
        Force,
    }

    [Serializable]
    public class PerformanceSettings
    {
        [Tooltip("Display refresh rate to ask for. 90 is the default; 120 if the game holds it on the headset.")]
        public float targetRefreshRate = 90f;
        public OVRManager.FoveatedRenderingLevel foveation = OVRManager.FoveatedRenderingLevel.High;
        public bool dynamicFoveation = true;

        [Header("Instanced rendering (stage 7)")]
        [Tooltip("Draw crowds of identical meshes (shards, bullets, debris, enemies, loose items, props) with one instanced draw call per mesh and material. Off: one renderer per object, as before.")]
        public bool instancedRendering = true;
        [Tooltip("Shards, bullets and their streaks, debris chunks, glowing caps, cracks and flashes.")]
        public bool instanceEffects = true;
        [Tooltip("Enemy bodies and floor shadows (not while they form: the reveal uses its own material per enemy).")]
        public bool instanceEnemies = true;
        [Tooltip("Weapons and throwables that are not in your hands (lying around, flying, held by enemies).")]
        public bool instanceItems = true;
        [Tooltip("The white level props (plinths, statues, cars, columns, light panels).")]
        public bool instanceProps = true;

        [Header("Pooling (stage 7)")]
        [Tooltip("Reuse enemies and guns instead of creating and destroying them (a spawn built about 25 objects and a NavMeshAgent).")]
        public bool poolEnemies = true;
        [Tooltip("Enemies built while the scan loads, so the first wave creates nothing.")]
        public int prewarmEnemies = 6;
        [Tooltip("Guns of each kind (pistol, shotgun, SMG) built while the scan loads.")]
        public int prewarmGunsPerKind = 2;

        [Header("Warm-up (stage 7)")]
        [Tooltip("While the scan loads, draw every game material once, invisibly small in front of you (plain and instanced), and lay out all text characters, so the first kill, reveal or title doesn't stall on shader or font setup.")]
        public bool warmUpShaders = true;
        [Tooltip("Frames to keep drawing the warm-up meshes.")]
        public int warmUpFrames = 3;

        [Header("Frame time")]
        [Tooltip("Longest game step per frame (s, Time.maximumDeltaTime). After a long hitch the game catches up at most this much at once, so physics doesn't run many steps in one frame. Unity's default is 0.333.")]
        public float maxDeltaTime = 0.1f;

        [Header("Resolution fallback (frame rate wins over resolution)")]
        [Tooltip("Auto: only if OVRManager's dynamic resolution can't work here (it then lowers the render scale itself when frames are missed and raises it back slowly). Off: never. Force: always, for testing.")]
        public ResolutionFallbackMode resolutionFallback = ResolutionFallbackMode.Auto;
        [Tooltip("Render scale range of the fallback (1.0 = 1680 px wide per eye, 1.205 = about 2024 px).")]
        public float fallbackMinScale = 1f, fallbackMaxScale = 1.205f;
        [Tooltip("Render scale change per step. Every change reallocates the eye textures, so steps are rare.")]
        public float fallbackStep = 0.05f;
        [Tooltip("Real seconds with missed frames before the scale goes down one step.")]
        public float fallbackDownAfter = 1.5f;
        [Tooltip("Real seconds without a missed frame before the scale goes up one step (doubles after a step up that had to be undone).")]
        public float fallbackUpAfter = 8f;
        [Tooltip("A half-second window counts as missing frames when its average frame time is above the display budget times this.")]
        public float overBudgetFactor = 1.08f;

        [Header("Refresh rate step-down (off by default)")]
        [Tooltip("If frames are still missed for stepDownAfter seconds at the lowest resolution, switch the display to stepDownRate (GAME_SPEC: 72 Hz minimum). Restored when you return to the menu. Every change is logged.")]
        public bool allowRefreshStepDown;
        public float stepDownAfter = 5f;
        public float stepDownRate = 72f;
    }

    [Serializable]
    public class DebugSettings
    {
        [Tooltip("Time scale gauge on the left wrist. Off in normal play.")]
        public bool wristTimeIndicator;
        [Tooltip("FPS readout at start (left thumbstick click toggles it anyway).")]
        public bool performanceReadout;
        [Tooltip("Real seconds between updates of the readout text (it is rebuilt, not every frame).")]
        public float readoutInterval = 0.5f;
        [Tooltip("The readout also lists the game's own profiler markers (development builds only).")]
        public bool readoutMarkers = true;
        [Tooltip("Editor only (never in a build): when the device gives no scan, load this MRUK sample room so levels and props can be checked in Play mode. Empty = off.")]
        public string editorSampleRoom = "Packages/com.meta.xr.mrutilitykit/Core/Rooms/Prefabs/LivingRoom/LivingRoom00.prefab";
    }
}
