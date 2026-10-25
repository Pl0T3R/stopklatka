using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// Game flow: load the scan, menu (floating crystals), pause with plain passthrough if you walk
    /// out of the scanned area. START hands the playing loop to <see cref="LevelManager"/> (3 levels
    /// of waves, GameConfig levels). With levels.useLevels off, the old endless waves run here:
    /// "STOP. KLATKA." when a wave is cleared, restart of the wave when you get hit.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class GameManager : MonoBehaviour
    {
        /// <summary>Playing, WaveCleared and Dead are the endless waves; Level means LevelManager runs the game.</summary>
        public enum State { Booting, Menu, Playing, WaveCleared, Dead, Level }

        public static GameManager Instance { get; private set; }

        public State Current { get; private set; } = State.Booting;
        public int Wave { get; private set; } = 1;
        public int Kills { get; private set; }
        public bool Paused { get; private set; }

        static readonly Color Red = new(0.95f, 0.12f, 0.08f);

        readonly List<MenuTarget> _menuTargets = new();
        int _toSpawn;
        string _menuText = "", _menuProblem, _menuNote;
        Vector3 _menuOrigin, _menuRight = Vector3.right;
        float _nextSpawnAt, _outside, _inside;
        Coroutine _flow;

        void Awake()
        {
            Instance = this;
            Layers.ConfigureCollisions();
            // scenes built before PlayerHealth / LevelManager existed get them here
            if (GetComponent<PlayerHealth>() == null) gameObject.AddComponent<PlayerHealth>();
            if (GetComponent<LevelManager>() == null) gameObject.AddComponent<LevelManager>();
        }

        void OnDestroy()
        {
            if (PlayerHealth.Instance != null) PlayerHealth.Instance.Hit -= PlayerHit;
        }

        IEnumerator Start()
        {
            // Start runs after every Awake, so PlayerHealth.Instance is set by now
            PlayerHealth.Instance.Hit += PlayerHit;
            yield return null;
            Hud.Instance.Title("STOPKLATKA", 0f, Color.white);
            Hud.Instance.Sub("Wczytuję skan pomieszczeń…", 0f);
            Shatter.Prewarm();   // pools for shards, debris, cracks and flashes, while the scan loads
            float until = Time.realtimeSinceStartup + 15f;
            while (RoomService.Instance != null && !RoomService.Instance.Ready && Time.realtimeSinceStartup < until) yield return null;
            var problem = RoomService.Instance != null ? RoomService.Instance.Problem : null;
            EnterMenu(problem);
        }

        void Update()
        {
            var rig = PlayerRig.Instance;
            var perf = PerformanceManager.Instance;
            Hud.Instance.Perf(perf != null && perf.ShowReadout ? perf.Readout() : "");
            if (rig == null || Current == State.Booting) return;

            // Leaving the scanned area pauses the game and shows the real world plainly.
            bool inside = RoomService.Instance == null || RoomService.Instance.IsInsideScan(rig.Head.position);
            if (!inside) { _outside += Time.unscaledDeltaTime; _inside = 0f; }
            else { _inside += Time.unscaledDeltaTime; _outside = 0f; }
            if (!Paused && _outside > 0.6f) SetPaused(true);
            else if (Paused && _inside > 0.5f) SetPaused(false);

            if (OVRInput.GetDown(OVRInput.Button.Start, OVRInput.Controller.LTouch) && Current != State.Menu)
                EnterMenu(null);

            if (Paused || Current != State.Playing) return;

            if (_toSpawn > 0 && Enemy.Alive.Count < Tuning.AliveCap(Wave) && Time.time >= _nextSpawnAt)
            {
                if (TrySpawnEnemy())
                {
                    _toSpawn--;
                    _nextSpawnAt = Time.time + Random.Range(1.0f, 2.2f);
                }
                else _nextSpawnAt = Time.time + 0.3f;
            }

            if (_toSpawn == 0 && Enemy.Alive.Count == 0)
                Run(WaveCleared());
        }

        // --- states ---------------------------------------------------------------

        void EnterMenu(string problem)
        {
            StopFlow();
            if (LevelManager.Instance != null) LevelManager.Instance.Stop();
            ClearMenuTargets();
            Current = State.Menu;
            TimeController.Instance.SetMode(TimeController.Mode.Normal);
            ClearWorld(keepHeld: true);

            var rig = PlayerRig.Instance;
            var head = rig.Head;
            var fwd = Flat(head.forward);
            var right = Vector3.Cross(Vector3.up, fwd);
            float chest = head.position.y - 0.3f;
            var basePos = new Vector3(head.position.x, chest, head.position.z) + fwd * 1.3f;

            _menuOrigin = basePos;
            _menuRight = right;
            _menuTargets.Add(Factory.CreateMenuTarget("START", basePos, Mats.Target, 0.22f, StartGame));
            SpawnStyleTarget();
            SpawnModeTarget();

            if (!PlayerHoldsGun())
            {
                var gunPos = head.position + fwd * 0.45f + right * 0.3f + Vector3.down * 0.45f;
                var gun = Factory.CreateGun(GunKind.Pistol, gunPos, Quaternion.LookRotation(fwd));
                gun.SetHovering(true);
            }

            _menuProblem = problem;
            ShowMenuText();
        }

        void ShowMenuText()
        {
            _menuText = string.IsNullOrEmpty(_menuProblem)
                ? $"Tryb: {GameMode.Profile.Label}. Czas płynie tylko, kiedy się ruszasz.\nWeź pistolet (grip) i strzel w czerwony kryształ (spust)."
                : _menuProblem + "\nMożesz grać wokół siebie: strzel w czerwony kryształ.";
            if (!string.IsNullOrEmpty(_menuNote)) _menuText = _menuNote + "\n" + _menuText;
            Hud.Instance.Title("STOPKLATKA", 0f, Color.white);
            Hud.Instance.Sub(_menuText, 0f);
        }

        /// <summary>Back to the menu with a line on top of the menu text (e.g. after the last level).</summary>
        public void ReturnToMenu(string note)
        {
            EnterMenu(_menuProblem);
            _menuNote = note;
            ShowMenuText();
        }

        /// <summary>START: the levels (GameConfig levels.useLevels), or the old endless waves.</summary>
        void StartGame()
        {
            _menuNote = null;
            var levels = LevelManager.Instance;
            if (Tuning.Levels.useLevels && levels != null && LevelManager.LevelCount > 0) levels.StartLevel(1);
            else StartWave(1);
        }

        /// <summary>LevelManager takes over the playing loop.</summary>
        public void BeginLevelPlay()
        {
            StopFlow();
            ClearMenuTargets();
            Current = State.Level;
        }

        // Toggle crystals shatter when used, so each press spawns a fresh one in the same spot.
        void SpawnStyleTarget()
        {
            var label = $"STYL: {(PassthroughStyle.Instance != null ? PassthroughStyle.Instance.Label : "BIAŁY")}";
            _menuTargets.Add(Factory.CreateMenuTarget(label, _menuOrigin - _menuRight * 0.65f + Vector3.up * 0.05f, Mats.Glass, 0.14f, () =>
            {
                PassthroughStyle.Instance?.Cycle();
                if (Current == State.Menu) SpawnStyleTarget();
            }));
        }

        void SpawnModeTarget()
        {
            _menuTargets.Add(Factory.CreateMenuTarget($"TRYB: {GameMode.Profile.Label}", _menuOrigin + _menuRight * 0.65f + Vector3.up * 0.05f, Mats.Glass, 0.14f, () =>
            {
                GameMode.Toggle();
                if (Current != State.Menu) return;
                SpawnModeTarget();
                ShowMenuText();
            }));
        }

        /// <summary>The old endless waves (GameConfig levels.useLevels off).</summary>
        public void StartWave(int wave)
        {
            StopFlow();
            if (LevelManager.Instance != null) LevelManager.Instance.Stop();
            ClearMenuTargets();
            ClearWorld(keepHeld: true);
            Current = State.Playing;
            Wave = wave;
            _toSpawn = Tuning.EnemiesInWave(wave);
            _nextSpawnAt = Time.time + 1.2f;
            TimeController.Instance.SetMode(TimeController.Mode.MotionDriven);
            SpawnPickups(1 + wave / 3);
            Hud.Instance.Title($"FALA {wave}", 2f, Red, pulse: true);
            Hud.Instance.Sub(wave == 1 ? "Stój spokojnie, a czas prawie stanie." : "", 3f);
        }

        IEnumerator WaveCleared()
        {
            Current = State.WaveCleared;
            TimeController.Instance.SetMode(TimeController.Mode.Normal);
            yield return Hud.Instance.Chant("STOP.", "KLATKA.", 3);
            Hud.Instance.Title($"FALA {Wave} ZALICZONA", 1.6f, Color.white);
            yield return new WaitForSecondsRealtime(1.8f);
            _flow = null;
            StartWave(Wave + 1);
        }

        public void PlayerHit(Vector3 point)
        {
            if (Current != State.Playing || Paused) return;
            Run(Death());
        }

        IEnumerator Death()
        {
            Current = State.Dead;
            TimeController.Instance.SetMode(TimeController.Mode.Fixed, 0.08f);
            Hud.Instance.RedFlash(0.8f);
            Hud.Instance.Title("TRAFIONY", 2.2f, Red, pulse: true);
            SoundBank.Instance?.PlayUi(Sfx.Death, 1f);   // the hit haptic comes from PlayerHealth
            yield return new WaitForSecondsRealtime(2.2f);
            _flow = null;
            StartWave(Wave);
        }

        public void EnemyKilled(Enemy enemy) => Kills++;

        void SetPaused(bool paused)
        {
            Paused = paused;
            PassthroughStyle.Instance?.SetSafety(paused);
            if (paused)
            {
                TimeController.Instance.SetMode(TimeController.Mode.Paused);
                Hud.Instance.Title("PAUZA", 0f, Color.white);
                Hud.Instance.Sub("Jesteś poza zeskanowanym obszarem. Wróć, żeby grać dalej.", 0f);
            }
            else
            {
                if (Current == State.Level && LevelManager.Instance != null) LevelManager.Instance.ApplyTimeMode();
                else TimeController.Instance.SetMode(Current == State.Playing ? TimeController.Mode.MotionDriven : TimeController.Mode.Normal);
                Hud.Instance.Clear();
                if (Current == State.Menu) ShowMenuText();
            }
        }

        // --- spawning -------------------------------------------------------------

        bool TrySpawnEnemy() =>
            EnemySpawner.TrySpawn(Wave, Tuning.ArmedChance(Wave), Tuning.ShotgunChance(Wave), Tuning.SmgChance(Wave));

        /// <summary>A pistol near you if your hands hold no gun, plus a few weapons and throwables around.</summary>
        public void SpawnPickups(int extras)
        {
            var rig = PlayerRig.Instance;
            var rooms = RoomService.Instance;
            if (!PlayerHoldsGun())
            {
                var gun = Factory.CreateGun(GunKind.Pistol, rooms.FindPickupSpot(rig.FloorPosition, 2.5f), Random.rotation);
                gun.Body.linearVelocity = Vector3.zero;
            }
            for (int i = 0; i < extras; i++)
            {
                var spot = rooms.FindPickupSpot(rig.FloorPosition);
                float roll = Random.value;
                if (roll < 0.45f) Factory.CreateThrowable(Factory.RandomThrowableKind(), spot, Quaternion.identity);
                else if (roll < 0.7f) Factory.CreateKatana(spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 90f));
                else
                {
                    float g = Random.value;
                    var kind = g < 0.3f ? GunKind.Shotgun : g < 0.6f ? GunKind.Smg : GunKind.Pistol;
                    Factory.CreateGun(kind, spot, Random.rotation);
                }
            }
        }

        // --- helpers --------------------------------------------------------------

        /// <summary>Removes enemies, bullets, debris and loose items (keepHeld: not what you hold).</summary>
        public void ClearWorld(bool keepHeld)
        {
            foreach (var e in new List<Enemy>(Enemy.Alive)) e.Despawn();
            BulletSystem.ClearAll();
            Effects.ClearAll();   // shards, debris, cracks and flashes
            foreach (var g in new List<Grabbable>(Grabbable.All))
            {
                if (keepHeld && g.Holder != null) continue;
                if (g.EnemyOwner != null) continue;
                Destroy(g.gameObject);
            }
        }

        void ClearMenuTargets()
        {
            foreach (var t in _menuTargets) if (t != null) Destroy(t.gameObject);
            _menuTargets.Clear();
        }

        static bool PlayerHoldsGun()
        {
            foreach (var g in Grabbable.All) if (g.Holder != null && g is Gun) return true;
            return false;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-4f ? Vector3.forward : v.normalized;
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
