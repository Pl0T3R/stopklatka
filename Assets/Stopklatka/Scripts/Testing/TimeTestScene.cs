using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// The time test scene (Stopklatka > Build Time Test Scene). Checks how time follows your
    /// movement without a room scan or the game flow. Placed in front of where you stand:
    ///   - a turret about 4 m ahead fires a slow enemy bullet at your head every 2.5 game seconds,
    ///   - a pendulum swings in game time, so you can watch time slow down and speed up,
    ///   - an armed enemy ahead on the right (comes back 3 s after you kill it),
    ///   - a pistol, a katana and a bottle float within reach.
    /// Hits are only counted. The left menu button puts everything in front of you again;
    /// a left thumbstick click shows the counters.
    /// Stand with a few metres of free space ahead: nothing here knows where your real walls are.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class TimeTestScene : MonoBehaviour
    {
        [Tooltip("Turret distance ahead of you (m).")]
        public float TurretDistance = 4f;
        [Tooltip("Game seconds between turret shots.")]
        public float TurretInterval = 2.5f;
        [Tooltip("Turret bullet speed (game m/s). 0 = the enemy bullet speed of the current mode.")]
        public float TurretBulletSpeed;
        public bool SpawnEnemy = true;
        [Tooltip("Enemy distance ahead of you (m); it stands a bit to the right.")]
        public float EnemyDistance = 4.5f;
        [Tooltip("Real seconds before a killed enemy comes back.")]
        public float EnemyRespawnSeconds = 3f;

        const float PendulumPeriod = 2.4f;   // game seconds

        readonly List<GameObject> _props = new();
        Transform _turretHead, _muzzle, _pendulum;
        Enemy _enemy;
        Vector3 _origin, _forward, _right;
        float _nextShot, _respawnAt = -1f;
        int _hits, _shots;
        bool _ready, _enemyUp;

        void Awake()
        {
            Layers.ConfigureCollisions();
            if (GetComponent<PlayerHealth>() == null) gameObject.AddComponent<PlayerHealth>();

            // no scan here: one big invisible floor so dropped and thrown things land
            var floor = new GameObject("TestFloor") { layer = Layers.Room };
            floor.transform.position = new Vector3(0f, -0.1f, 0f);
            floor.AddComponent<BoxCollider>().size = new Vector3(40f, 0.2f, 40f);
        }

        void OnDestroy()
        {
            if (PlayerHealth.Instance != null) PlayerHealth.Instance.Hit -= OnHit;
        }

        IEnumerator Start()
        {
            PlayerHealth.Instance.Hit += OnHit;
            TimeController.Instance.SetMode(TimeController.Mode.Normal);
            Hud.Instance.Title("TEST CZASU", 0f, Color.white);
            Hud.Instance.Sub("Stań prosto i patrz przed siebie.\nPotrzebujesz kilku metrów wolnego miejsca.", 0f);
            // give tracking a moment, so "in front of you" really is in front of you
            yield return new WaitForSecondsRealtime(1.5f);
            Layout();
            Hud.Instance.Title("TEST CZASU", 2.5f, Color.white, pulse: true);
            Hud.Instance.Sub("Stój: czas prawie staje. Ruszaj się: czas płynie.\nStrzał, rzut, chwyt: krótki impuls.\n" +
                             "Lewy kontroler: przycisk menu ustawia wszystko od nowa,\nkliknięcie gałki pokazuje liczniki.", 10f);
            _ready = true;
        }

        void Update()
        {
            var perf = PerformanceManager.Instance;
            var time = TimeController.Instance;
            if (perf != null && perf.ShowReadout && time != null)
                Hud.Instance.Perf($"{perf.Readout()}\nruch {time.Motion:F2}  cel x{time.Target:F2}\ntrafienia {_hits}  strzały {_shots}");
            else Hud.Instance.Perf("");

            if (!_ready) return;
            if (OVRInput.GetDown(OVRInput.Button.Start, OVRInput.Controller.LTouch))
            {
                Layout();
                return;
            }

            UpdateTurret();
            if (_pendulum != null)
                _pendulum.rotation = Quaternion.AngleAxis(35f * Mathf.Sin(Time.time * (2f * Mathf.PI / PendulumPeriod)), _forward);
            UpdateEnemy();
        }

        // --- layout -----------------------------------------------------------------

        void Layout()
        {
            Clear();
            var rig = PlayerRig.Instance;
            var head = rig.Head;
            _forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (_forward.sqrMagnitude < 1e-4f) _forward = Vector3.forward;
            _forward.Normalize();
            _right = Vector3.Cross(Vector3.up, _forward);
            _origin = rig.FloorPosition;
            float eye = head.position.y;

            BuildTurret(_origin + _forward * TurretDistance);
            BuildPendulum(_origin + _forward * 2.2f - _right * 1.3f);
            _respawnAt = SpawnEnemy ? Time.unscaledTime : -1f;

            var near = new Vector3(head.position.x, 0f, head.position.z) + _forward * 0.45f;
            var pistol = Factory.CreateGun(GunKind.Pistol, near + _right * 0.3f + Vector3.up * (eye - 0.45f), Quaternion.LookRotation(_forward));
            var katana = Factory.CreateKatana(near - _right * 0.35f + Vector3.up * (eye - 0.75f), Quaternion.LookRotation(Vector3.up, _forward));
            var bottle = Factory.CreateBottle(near + _right * 0.55f + Vector3.up * (eye - 0.5f), Quaternion.identity);
            foreach (var g in new Grabbable[] { pistol, katana, bottle }) g.SetHovering(true);

            _nextShot = Time.time + TurretInterval;
            TimeController.Instance.ResetTracking();
            TimeController.Instance.SetMode(TimeController.Mode.MotionDriven);
        }

        void Clear()
        {
            foreach (var p in _props) if (p != null) Destroy(p);
            _props.Clear();
            _turretHead = _muzzle = _pendulum = null;
            foreach (var e in new List<Enemy>(Enemy.Alive)) e.Despawn();
            _enemy = null;
            _enemyUp = false;
            BulletSystem.ClearAll();
            ShardSystem.ClearAll();
            Effects.ClearAll();
            foreach (var g in new List<Grabbable>(Grabbable.All))
                if (g.Holder == null && g.EnemyOwner == null) Destroy(g.gameObject);
        }

        void BuildTurret(Vector3 floorPoint)
        {
            var root = new GameObject("Turret").transform;
            root.position = floorPoint;
            _props.Add(root.gameObject);

            var stand = new MeshKit.Builder();
            stand.Prism(Vector3.zero, new Vector3(0f, 1.25f, 0f), 0.16f, 0.06f, 6);
            stand.Prism(Vector3.zero, new Vector3(0f, 0.04f, 0f), 0.3f, 0.28f, 6);
            MeshKit.Part("Stand", root, stand.ToMesh("Turret Stand"), Mats.Get(Mats.Weapon));

            _turretHead = new GameObject("Head").transform;
            _turretHead.SetParent(root, false);
            _turretHead.localPosition = new Vector3(0f, 1.4f, 0f);
            var body = new MeshKit.Builder();
            body.Box(Vector3.zero, new Vector3(0.22f, 0.2f, 0.3f), Quaternion.identity, 0.8f);
            MeshKit.Part("Body", _turretHead, body.ToMesh("Turret Body"), Mats.Get(Mats.Enemy));
            var barrel = new MeshKit.Builder();
            barrel.Prism(new Vector3(0f, 0f, 0.1f), new Vector3(0f, 0f, 0.42f), 0.04f, 0.032f, 6);
            MeshKit.Part("Barrel", _turretHead, barrel.ToMesh("Turret Barrel"), Mats.Get(Mats.Weapon));
            _muzzle = new GameObject("Muzzle").transform;
            _muzzle.SetParent(_turretHead, false);
            _muzzle.localPosition = new Vector3(0f, 0f, 0.45f);
            _turretHead.rotation = Quaternion.LookRotation(-_forward, Vector3.up);
        }

        void BuildPendulum(Vector3 floorPoint)
        {
            var root = new GameObject("Pendulum").transform;
            root.position = floorPoint + Vector3.up * 2.05f;
            _props.Add(root.gameObject);

            var mount = new MeshKit.Builder();
            mount.Box(Vector3.zero, new Vector3(0.3f, 0.05f, 0.08f), Quaternion.identity);
            MeshKit.Part("Mount", root, mount.ToMesh("Pendulum Mount"), Mats.Get(Mats.Weapon));

            _pendulum = new GameObject("Arm").transform;
            _pendulum.SetParent(root, false);
            var rod = new MeshKit.Builder();
            rod.Prism(Vector3.zero, new Vector3(0f, -1.05f, 0f), 0.012f, 0.012f, 5);
            MeshKit.Part("Rod", _pendulum, rod.ToMesh("Pendulum Rod"), Mats.Get(Mats.Weapon));
            var bob = new MeshKit.Builder();
            bob.Ico(new Vector3(0f, -1.13f, 0f), Vector3.one * 0.1f);
            MeshKit.Part("Bob", _pendulum, bob.ToMesh("Pendulum Bob"), Mats.Get(Mats.Enemy));
        }

        // --- turret and enemy (game time: both freeze when you stand still) ----------

        void UpdateTurret()
        {
            if (_turretHead == null) return;
            var head = PlayerRig.Instance.Head.position;
            var aim = Quaternion.LookRotation(head - _turretHead.position, Vector3.up);
            _turretHead.rotation = Quaternion.Slerp(_turretHead.rotation, aim, 1f - Mathf.Exp(-3f * Time.deltaTime));
            if (Time.time < _nextShot) return;

            _nextShot = Time.time + TurretInterval;
            float speed = TurretBulletSpeed > 0f ? TurretBulletSpeed : Tuning.EnemyBulletSpeed;
            var dir = (head - _muzzle.position).normalized;
            BulletSystem.Fire(_muzzle.position, dir * speed, BulletOwner.Enemy, null);
            Effects.MuzzleFlash(_muzzle.position, Quaternion.LookRotation(dir));
            SoundBank.Instance?.Play(Sfx.Pistol, _muzzle.position, 0.9f, 0.8f);
            _shots++;
        }

        void UpdateEnemy()
        {
            // a killed enemy leaves Alive at once and is destroyed at the end of that frame
            if (_enemyUp && (_enemy == null || !Enemy.Alive.Contains(_enemy)))
            {
                _enemyUp = false;
                _enemy = null;
                _respawnAt = Time.unscaledTime + EnemyRespawnSeconds;
            }
            if (_enemyUp || _respawnAt < 0f || Time.unscaledTime < _respawnAt) return;

            _respawnAt = -1f;
            var pos = _origin + _forward * EnemyDistance + _right * 1.6f;
            var face = Quaternion.LookRotation(Vector3.ProjectOnPlane(_origin - pos, Vector3.up), Vector3.up);
            _enemy = Factory.CreateEnemy(pos, face, 1, GunKind.Pistol);
            _enemyUp = true;
        }

        void OnHit(Vector3 point)
        {
            _hits++;
            Hud.Instance.RedFlash();
            Hud.Instance.Title($"TRAFIONY ({_hits})", 1.2f, null, pulse: true);
            SoundBank.Instance?.PlayUi(Sfx.Death, 0.6f);
        }
    }
}
