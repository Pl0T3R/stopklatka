using System.Collections;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// The playing loop (GAME_SPEC 8): levels made of waves, defined in GameConfig levels.
    ///   start: level name, pickups around you, then the waves one after another,
    ///   end: the last wave cleared -> "STOP." / "KLATKA." -> the next level; after the last level,
    ///        back to the menu with a message,
    ///   death: one hit -> red flash, "TRAFIONY", the world nearly stops (no camera movement),
    ///          and the level restarts from its first wave after levels.restartDelay (1.5 s).
    /// A level here is only its waves; props and rooms per level come in stage 6. GameManager keeps
    /// boot, the scan, the menu and the pause (walking out of the scan pauses this loop too).
    /// With levels.useLevels off, GameManager runs the old endless waves instead.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class LevelManager : MonoBehaviour
    {
        public enum Phase { Idle, Intro, Fighting, WaveBreak, Cleared, Dead }

        public static LevelManager Instance { get; private set; }

        public Phase Current { get; private set; } = Phase.Idle;
        /// <summary>1-based level and wave numbers.</summary>
        public int Level { get; private set; }
        public int Wave { get; private set; }
        public bool Running => Current != Phase.Idle;

        static readonly Color Red = new(0.95f, 0.12f, 0.08f);

        int _toSpawn;
        float _nextSpawnAt;
        bool _subscribed;
        Coroutine _flow;

        static LevelSettings Settings => Tuning.Levels;
        public static int LevelCount => Settings.list != null ? Settings.list.Length : 0;

        LevelDefinition Definition => Settings.list[Mathf.Clamp(Level - 1, 0, LevelCount - 1)];

        WaveDefinition CurrentWave
        {
            get
            {
                var waves = Definition.waves;
                return waves[Mathf.Clamp(Wave - 1, 0, waves.Length - 1)];
            }
        }

        int WaveCount => Definition.waves != null ? Definition.waves.Length : 0;

        void Awake() => Instance = this;

        void Start()
        {
            if (PlayerHealth.Instance == null) return;
            PlayerHealth.Instance.Hit += PlayerHit;
            _subscribed = true;
        }

        void OnDestroy()
        {
            if (_subscribed && PlayerHealth.Instance != null) PlayerHealth.Instance.Hit -= PlayerHit;
        }

        /// <summary>Starts a level (1-based) from its first wave.</summary>
        public void StartLevel(int level, bool restart = false)
        {
            StopFlow();
            if (LevelCount == 0)
            {
                Debug.LogWarning("[Stopklatka] GameConfig levels.list is empty: nothing to play.");
                return;
            }
            if (GameManager.Instance != null) GameManager.Instance.BeginLevelPlay();
            Level = Mathf.Clamp(level, 1, LevelCount);
            Wave = 0;
            Run(Intro(restart));
        }

        /// <summary>Stops the loop (back to the menu). Enemies and debris are cleared by GameManager.</summary>
        public void Stop()
        {
            StopFlow();
            Current = Phase.Idle;
            _toSpawn = 0;
            if (PlayerHealth.Instance != null) PlayerHealth.Instance.Invulnerable = false;
        }

        /// <summary>Called when the pause ends: the right time mode for this phase.</summary>
        public void ApplyTimeMode()
        {
            var time = TimeController.Instance;
            if (time == null) return;
            if (Current == Phase.Fighting || Current == Phase.WaveBreak) time.SetMode(TimeController.Mode.MotionDriven);
            else if (Current == Phase.Dead) time.SetMode(TimeController.Mode.Fixed, 0.08f);
            else time.SetMode(TimeController.Mode.Normal);
        }

        IEnumerator Intro(bool restart)
        {
            Current = Phase.Intro;
            SetInvulnerable(true);
            SetTime(TimeController.Mode.Normal);
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.ClearWorld(keepHeld: true);
                gm.SpawnPickups(Settings.pickupsPerLevel);
            }

            float seconds = Mathf.Max(0.3f, restart ? Settings.introSeconds * 0.5f : Settings.introSeconds);
            Hud.Instance.Title($"POZIOM {Level}", seconds, Red, pulse: true);
            string hint = Level == 1 && !restart ? "\nStój spokojnie, a czas prawie stanie." : "";
            Hud.Instance.Sub(Definition.name + hint, seconds + 1.5f);
            yield return WaitReal(seconds);
            _flow = null;
            BeginWave(1);
        }

        void BeginWave(int wave)
        {
            if (WaveCount == 0)
            {
                // a level edited down to no waves counts as cleared
                Run(Cleared());
                return;
            }
            Wave = wave;
            var w = CurrentWave;
            _toSpawn = Mathf.Max(0, w.enemies);
            _nextSpawnAt = Time.time + 1f;
            Current = Phase.Fighting;
            SetInvulnerable(false);
            SetTime(TimeController.Mode.MotionDriven);
            if (wave > 1) Hud.Instance.Title($"FALA {wave}/{WaveCount}", 1.5f, Color.white, pulse: true);
        }

        void Update()
        {
            if (Current != Phase.Fighting) return;
            var gm = GameManager.Instance;
            if (gm != null && gm.Paused) return;

            var w = CurrentWave;
            // at most 6 at once (budget), fewer at home (GameMode)
            int cap = Mathf.Clamp(Mathf.Min(w.maxAlive, GameMode.Profile.MaxAlive), 1, 6);
            if (_toSpawn > 0 && Enemy.Alive.Count < cap && Time.time >= _nextSpawnAt)
            {
                if (EnemySpawner.TrySpawn(w.difficulty, w.armedShare, w.shotgunShare, w.smgShare))
                {
                    _toSpawn--;
                    _nextSpawnAt = Time.time + Random.Range(Settings.spawnIntervalMin, Mathf.Max(Settings.spawnIntervalMin, Settings.spawnIntervalMax));
                }
                else _nextSpawnAt = Time.time + 0.3f;
            }

            if (_toSpawn == 0 && Enemy.Alive.Count == 0)
                Run(Wave < WaveCount ? WaveBreak() : Cleared());
        }

        IEnumerator WaveBreak()
        {
            Current = Phase.WaveBreak;
            yield return WaitReal(Settings.waveGap);
            _flow = null;
            BeginWave(Wave + 1);
        }

        IEnumerator Cleared()
        {
            Current = Phase.Cleared;
            SetInvulnerable(true);
            SetTime(TimeController.Mode.Normal);
            yield return Hud.Instance.Chant("STOP.", "KLATKA.", Mathf.Max(1, Settings.chantRepeats));
            Hud.Instance.Title($"POZIOM {Level} ZALICZONY", 1.8f, Color.white, pulse: true);
            yield return WaitReal(2f);
            if (Level < LevelCount)
            {
                _flow = null;   // don't let StartLevel stop this coroutine mid-call
                StartLevel(Level + 1);
                yield break;
            }

            // all levels done: back to the menu (stage 6 adds level select)
            Hud.Instance.Title("KONIEC", 2.5f, Red, pulse: true);
            Hud.Instance.Sub("Wszystkie poziomy zaliczone.", 2.5f);
            yield return WaitReal(2.5f);
            _flow = null;
            Current = Phase.Idle;
            SetInvulnerable(false);
            if (GameManager.Instance != null)
                GameManager.Instance.ReturnToMenu($"Wszystkie poziomy zaliczone ({LevelCount}). START zaczyna od nowa.");
        }

        void PlayerHit(Vector3 point)
        {
            if (Current != Phase.Fighting && Current != Phase.WaveBreak) return;
            var gm = GameManager.Instance;
            if (gm != null && gm.Paused) return;
            Run(Death());
        }

        IEnumerator Death()
        {
            Current = Phase.Dead;
            SetInvulnerable(true);
            float delay = Mathf.Max(0.2f, Settings.restartDelay);
            // the world almost stops; the view never moves (comfort)
            SetTime(TimeController.Mode.Fixed, 0.08f);
            Hud.Instance.RedFlash(0.8f);
            Hud.Instance.Title("TRAFIONY", delay + 0.4f, Red, pulse: true);
            Hud.Instance.Sub("Poziom od nowa.", delay);
            SoundBank.Instance?.PlayUi(Sfx.Death, 1f);   // the hit haptic comes from PlayerHealth
            yield return WaitReal(delay);
            _flow = null;
            StartLevel(Level, restart: true);
        }

        /// <summary>Real seconds that don't count while the game is paused (you left the scan).</summary>
        static IEnumerator WaitReal(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                var gm = GameManager.Instance;
                if (gm == null || !gm.Paused) t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>Changes the time mode, unless the game is paused (ApplyTimeMode restores it on resume).</summary>
        static void SetTime(TimeController.Mode mode, float fixedScale = 1f)
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Paused) return;
            if (TimeController.Instance != null) TimeController.Instance.SetMode(mode, fixedScale);
        }

        static void SetInvulnerable(bool value)
        {
            if (PlayerHealth.Instance != null) PlayerHealth.Instance.Invulnerable = value;
        }

        void Run(IEnumerator routine)
        {
            StopFlow();
            _flow = StartCoroutine(routine);
        }

        void StopFlow()
        {
            if (_flow != null) StopCoroutine(_flow);
            _flow = null;
        }
    }
}
