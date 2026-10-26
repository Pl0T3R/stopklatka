using System;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.AI;

namespace Stopklatka
{
    /// <summary>Builds enemies, weapons, throwables, the player's fists and menu targets from code-generated meshes.</summary>
    public static class Factory
    {
        static Mesh _pistol, _shotgun, _smg, _katanaHandle, _katanaBlade, _bottle, _mug, _ashtray, _block, _ball, _target, _menuCube;
        static Mesh _fistLeft, _fistRight;

        // Pose of a held gun relative to the controller anchor. Barrel follows the controller's forward axis.
        // If guns feel tilted on the headset, tune these two values first.
        public static Vector3 GunHoldPosition = new Vector3(0f, -0.015f, 0.005f);
        public static Vector3 GunHoldEuler = new Vector3(0f, 0f, 0f);

        // Pose of the right fist relative to the right controller anchor (the left one is mirrored).
        // Rolled so the thumb is on top and the back of the hand faces outward, like a hand around
        // the controller's grip. Tune on the headset if the fist doesn't sit over your real hand.
        public static Vector3 FistPosition = new Vector3(0f, -0.02f, -0.01f);
        public static Vector3 FistEuler = new Vector3(0f, 0f, -90f);
        /// <summary>Center of the punch hit sphere in the fist mesh's own space (the knuckles).</summary>
        public static readonly Vector3 FistHitCenter = new Vector3(0f, 0f, 0.04f);

        public static void FistPose(bool left, out Vector3 position, out Quaternion rotation)
        {
            float m = left ? -1f : 1f;
            position = new Vector3(FistPosition.x * m, FistPosition.y, FistPosition.z);
            rotation = Quaternion.Euler(FistEuler.x, FistEuler.y * m, FistEuler.z * m);
        }

        /// <summary>
        /// An enemy at this pose, starting to form, with an optional gun. Pooled enemies and guns are
        /// reused (GameConfig performance.poolEnemies); otherwise they are built here.
        /// </summary>
        public static Enemy CreateEnemy(Vector3 position, Quaternion rotation, int wave, GunKind? gun)
        {
            using var perfScope = PerfMarkers.Spawn.Auto();
            var enemy = EnemyPool.Get(position, rotation);
            Gun weapon = gun.HasValue ? CreateGun(gun.Value, position, rotation) : null;
            var rooms = RoomService.Instance;
            enemy.Init(weapon, wave, rooms != null ? rooms.NavAgentTypeId : 0, rooms != null && rooms.NavReady);
            return enemy;
        }

        /// <summary>
        /// Builds a new enemy, inactive (the caller places and activates it): the body, its colliders,
        /// a kinematic body and a NavMeshAgent (off until Enemy.Init puts it on the NavMesh).
        /// </summary>
        public static Enemy BuildEnemy(Vector3 position, Quaternion rotation)
        {
            var root = new GameObject("Enemy") { layer = Layers.Enemy };
            // inactive while it is put together, so the agent isn't created off the NavMesh
            root.SetActive(false);
            root.transform.SetPositionAndRotation(position, rotation);

            // body capsule (broad shoulders: a bit wider than the waist) and the long head
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.85f, 0f);
            capsule.height = 1.7f;
            capsule.radius = 0.22f;
            var head = root.AddComponent<SphereCollider>();
            head.center = new Vector3(0f, 1.66f, 0.01f);
            head.radius = 0.13f;
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var agent = root.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            agent.radius = 0.25f;
            agent.height = 1.75f;
            agent.baseOffset = 0f;
            agent.speed = Tuning.EnemyWalkSpeed;
            agent.angularSpeed = 360f;
            agent.acceleration = 6f;
            agent.stoppingDistance = 0.25f;
            agent.updateRotation = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            var body = EnemyBody.Build(root.transform, Mats.Get(Mats.Enemy), Mats.Get(Mats.FloorShadow));
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Enemy;

            var enemy = root.AddComponent<Enemy>();
            enemy.Setup(body, agent);
            return enemy;
        }

        /// <summary>
        /// Builds the enemy and gun pools while loading (GameConfig performance.prewarmEnemies and
        /// prewarmGunsPerKind), so spawning in a fight only reuses objects.
        /// </summary>
        public static void PrewarmPools()
        {
            var perf = GameConfig.Instance.performance;
            EnemyPool.Prewarm(perf.prewarmEnemies);
            GunPool.Prewarm(perf.prewarmGunsPerKind);
        }

        /// <summary>A gun of this kind: from the gun pool (GameConfig performance.poolEnemies) or new.</summary>
        public static Gun CreateGun(GunKind kind, Vector3 position, Quaternion rotation)
        {
            var pooled = GunPool.Get(kind, position, rotation);
            return pooled != null ? pooled : BuildGun(kind, position, rotation);
        }

        /// <summary>
        /// Removes a loose item (clearing the world, the time test's spare pistols): guns go back to
        /// the gun pool, everything else is destroyed.
        /// </summary>
        public static void Recycle(Grabbable item)
        {
            if (item == null) return;
            if (item is Gun gun && GunPool.Release(gun)) return;
            Object.Destroy(item.gameObject);
        }

        /// <summary>Builds a new gun (pistol, shotgun or SMG), active and ready.</summary>
        public static Gun BuildGun(GunKind kind, Vector3 position, Quaternion rotation)
        {
            EnsureMeshes();
            var go = new GameObject(kind.ToString());
            go.transform.SetPositionAndRotation(position, rotation);
            var mesh = kind switch { GunKind.Shotgun => _shotgun, GunKind.Smg => _smg, _ => _pistol };
            Item(MeshKit.Part("Mesh", go.transform, mesh, Mats.Get(Mats.Weapon)));

            var col = go.AddComponent<BoxCollider>();
            switch (kind)
            {
                case GunKind.Shotgun: col.center = new Vector3(0f, 0.01f, 0.18f); col.size = new Vector3(0.05f, 0.1f, 0.85f); break;
                case GunKind.Smg: col.center = new Vector3(0f, -0.048f, 0.043f); col.size = new Vector3(0.045f, 0.19f, 0.365f); break;
                default: col.center = new Vector3(0f, -0.01f, 0.05f); col.size = new Vector3(0.04f, 0.12f, 0.19f); break;
            }

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = kind switch { GunKind.Shotgun => 1.6f, GunKind.Smg => 1.2f, _ => 0.9f };
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.3f;

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(go.transform, false);
            muzzle.localPosition = kind switch
            {
                GunKind.Shotgun => new Vector3(0f, 0.03f, 0.62f),
                GunKind.Smg => new Vector3(0f, 0.025f, 0.225f),
                _ => new Vector3(0f, 0.022f, 0.155f),
            };

            var gunComp = go.AddComponent<Gun>();
            gunComp.Kind = kind;
            gunComp.Muzzle = muzzle;
            gunComp.Ammo = gunComp.MaxAmmo;
            gunComp.HoldPosition = GunHoldPosition;
            gunComp.HoldEuler = GunHoldEuler;
            return gunComp;
        }

        public static Katana CreateKatana(Vector3 position, Quaternion rotation)
        {
            EnsureMeshes();
            var go = new GameObject("Katana");
            go.transform.SetPositionAndRotation(position, rotation);
            Item(MeshKit.Part("Handle", go.transform, _katanaHandle, Mats.Get(Mats.Weapon)));
            Item(MeshKit.Part("Blade", go.transform, _katanaBlade, Mats.Get(Mats.Blade)));

            var handle = go.AddComponent<BoxCollider>();
            handle.center = new Vector3(0f, 0f, 0.02f);
            handle.size = new Vector3(0.04f, 0.045f, 0.3f);
            var bladeGo = new GameObject("BladeCollider");
            bladeGo.transform.SetParent(go.transform, false);
            var blade = bladeGo.AddComponent<BoxCollider>();
            blade.center = new Vector3(0f, 0f, 0.53f);
            blade.size = new Vector3(0.02f, 0.05f, 0.74f);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1.1f;
            rb.angularDamping = 0.2f;

            var bladeBase = new GameObject("BladeBase").transform;
            bladeBase.SetParent(go.transform, false);
            bladeBase.localPosition = new Vector3(0f, 0f, 0.18f);
            var tip = new GameObject("BladeTip").transform;
            tip.SetParent(go.transform, false);
            tip.localPosition = new Vector3(0f, 0f, 0.9f);

            var k = go.AddComponent<Katana>();
            k.BladeBase = bladeBase;
            k.BladeTip = tip;
            k.HoldPosition = new Vector3(0f, -0.01f, -0.02f);
            k.HoldEuler = new Vector3(-30f, 0f, 0f);   // blade angled up a little from the controller
            return k;
        }

        public static Throwable CreateBottle(Vector3 position, Quaternion rotation) =>
            CreateThrowable(ThrowableKind.Bottle, position, rotation);

        /// <summary>A random dark throwable or a bottle, for pickups.</summary>
        public static ThrowableKind RandomThrowableKind()
        {
            float r = UnityEngine.Random.value;
            return r < 0.35f ? ThrowableKind.Bottle : r < 0.55f ? ThrowableKind.Mug : r < 0.7f ? ThrowableKind.Ashtray
                : r < 0.85f ? ThrowableKind.Block : ThrowableKind.Ball;
        }

        /// <summary>Bottle (dark glass), mug, ashtray, block or ball (black).</summary>
        public static Throwable CreateThrowable(ThrowableKind kind, Vector3 position, Quaternion rotation)
        {
            EnsureMeshes();
            var go = new GameObject(kind.ToString());
            go.transform.SetPositionAndRotation(position, rotation);
            var rb = go.AddComponent<Rigidbody>();
            var holdPosition = new Vector3(0f, -0.01f, 0.04f);
            var holdEuler = Vector3.zero;
            switch (kind)
            {
                case ThrowableKind.Bottle:
                {
                    Item(MeshKit.Part("Mesh", go.transform, _bottle, Mats.Get(Mats.DarkGlass)));
                    var col = go.AddComponent<CapsuleCollider>();
                    col.center = new Vector3(0f, 0.02f, 0f);
                    col.height = 0.28f;
                    col.radius = 0.037f;
                    rb.mass = 0.45f;
                    holdPosition = new Vector3(0f, -0.01f, 0.01f);
                    holdEuler = new Vector3(90f, 0f, 0f);   // neck forward
                    break;
                }
                case ThrowableKind.Mug:
                {
                    Item(MeshKit.Part("Mesh", go.transform, _mug, Mats.Get(Mats.Weapon)));
                    var col = go.AddComponent<BoxCollider>();
                    col.center = new Vector3(0.012f, 0f, 0f);
                    col.size = new Vector3(0.104f, 0.096f, 0.08f);
                    rb.mass = 0.35f;
                    break;
                }
                case ThrowableKind.Ashtray:
                {
                    Item(MeshKit.Part("Mesh", go.transform, _ashtray, Mats.Get(Mats.Weapon)));
                    var col = go.AddComponent<BoxCollider>();
                    col.size = new Vector3(0.135f, 0.03f, 0.135f);
                    rb.mass = 0.7f;
                    holdPosition = new Vector3(0f, -0.015f, 0.06f);
                    break;
                }
                case ThrowableKind.Block:
                {
                    Item(MeshKit.Part("Mesh", go.transform, _block, Mats.Get(Mats.Weapon)));
                    go.AddComponent<BoxCollider>().size = Vector3.one * 0.085f;
                    rb.mass = 0.6f;
                    break;
                }
                default:
                {
                    Item(MeshKit.Part("Mesh", go.transform, _ball, Mats.Get(Mats.Weapon)));
                    go.AddComponent<SphereCollider>().radius = 0.045f;
                    rb.mass = 0.4f;
                    rb.angularDamping = 0.4f;
                    break;
                }
            }
            var t = go.AddComponent<Throwable>();
            t.Setup(kind);
            t.HoldPosition = holdPosition;
            t.HoldEuler = holdEuler;
            return t;
        }

        /// <summary>
        /// Weapons and throwables are drawn in groups while nobody holds them (InstanceDrawer);
        /// <see cref="Grabbable"/> suspends this while the item is in your hand.
        /// </summary>
        static void Item(GameObject part) => InstancedPart.Attach(part, InstanceGroup.Items);

        /// <summary>The player's black faceted fist (built palm down, knuckles along +Z), mirrored for the left hand.</summary>
        public static Mesh FistMesh(bool left)
        {
            if (left && _fistLeft != null) return _fistLeft;
            if (!left && _fistRight != null) return _fistRight;
            float m = left ? -1f : 1f;
            var rng = new System.Random(left ? 31 : 32);
            var f = new MeshKit.Builder();
            Vector3 V(float x, float y, float z) => new Vector3(x * m, y, z);
            Quaternion R(float x, float y, float z) => Quaternion.Euler(x, y * m, z * m);

            f.Box(V(0f, 0f, -0.008f), new Vector3(0.084f, 0.062f, 0.08f), R(0f, 0f, 0f), 0.92f, 0.003f, rng);          // back of the hand and palm
            for (int i = 0; i < 4; i++)                                                                                  // curled fingers, one knuckle each
            {
                float x = -0.03f + i * 0.02f;
                f.Box(V(x, 0.004f, 0.044f), new Vector3(0.019f, 0.044f - i * 0.003f, 0.03f), R(6f, 0f, 0f), 0.9f, 0.0015f, rng);
            }
            f.Box(V(-0.046f, -0.012f, 0.02f), new Vector3(0.02f, 0.024f, 0.052f), R(0f, 28f, 0f), 0.85f, 0.0015f, rng); // thumb across the fingers
            f.Prism(V(0f, -0.004f, -0.046f), V(0f, -0.008f, -0.16f), 0.031f, 0.036f, 6, 0.002f, rng);                // wrist cuff
            var mesh = f.ToMesh(left ? "Fist Left" : "Fist Right");
            if (left) _fistLeft = mesh; else _fistRight = mesh;
            return mesh;
        }

        public static MenuTarget CreateMenuTarget(string label, Vector3 position, string material, float size, Action onActivated)
        {
            EnsureMeshes();
            var root = new GameObject($"Target {label}") { layer = Layers.Enemy };
            root.transform.position = position;
            var visual = MeshKit.Part("Crystal", root.transform, _target, Mats.Get(material));
            visual.layer = Layers.Enemy;
            visual.transform.localScale = Vector3.one * size;
            var col = root.AddComponent<SphereCollider>();
            col.radius = 0.12f * size / 0.2f;
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var target = root.AddComponent<MenuTarget>();
            target.Setup(label, visual.GetComponent<Renderer>(), onActivated, 0.24f, 0.034f);
            return target;
        }

        /// <summary>
        /// A black faceted menu cube (GAME_SPEC 8) with its label above: grab it with the grip (or punch,
        /// shoot or cut it) and it breaks into black shards and runs the action. On the enemy layer like
        /// the crystals, so bullets, the fist and the katana find it too.
        /// </summary>
        public static MenuTarget CreateMenuCube(string label, Vector3 position, float size, Action onActivated)
        {
            EnsureMeshes();
            var root = new GameObject($"Menu {label}") { layer = Layers.Enemy };
            root.transform.position = position;
            var visual = MeshKit.Part("Cube", root.transform, _menuCube, Mats.Get(Mats.Weapon));
            visual.layer = Layers.Enemy;
            visual.transform.localScale = Vector3.one * size;
            visual.transform.localRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 90f), 0f);
            root.AddComponent<BoxCollider>().size = Vector3.one * (size * 1.1f);
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var target = root.AddComponent<MenuTarget>();
            target.TouchActivates = false;
            target.Grabbable = true;
            target.Size = size * 0.5f;
            target.Setup(label, visual.GetComponent<Renderer>(), onActivated, size * 0.5f + 0.035f, Tuning.Ui.labelSize);
            return target;
        }

        static void EnsureMeshes()
        {
            if (_pistol != null) return;
            var rng = new System.Random(9);

            var p = new MeshKit.Builder();
            p.Box(new Vector3(0f, -0.05f, -0.012f), new Vector3(0.03f, 0.095f, 0.036f), Quaternion.Euler(-14f, 0f, 0f), 1f, 0.002f, rng); // grip
            p.Box(new Vector3(0f, 0.018f, 0.055f), new Vector3(0.032f, 0.036f, 0.19f), Quaternion.identity, 0.92f, 0.001f, rng);           // slide
            p.Box(new Vector3(0f, -0.012f, 0.03f), new Vector3(0.012f, 0.022f, 0.045f), Quaternion.identity);                             // trigger guard
            p.Prism(new Vector3(0f, 0.022f, 0.14f), new Vector3(0f, 0.022f, 0.158f), 0.008f, 0.008f, 6);                                  // muzzle
            _pistol = p.ToMesh("Pistol");

            var s = new MeshKit.Builder();
            s.Box(new Vector3(0f, -0.045f, -0.01f), new Vector3(0.032f, 0.09f, 0.038f), Quaternion.Euler(-18f, 0f, 0f), 1f, 0.002f, rng); // grip
            s.Box(new Vector3(0f, 0.005f, -0.17f), new Vector3(0.035f, 0.06f, 0.26f), Quaternion.Euler(6f, 0f, 0f), 1.15f, 0.003f, rng); // stock
            s.Box(new Vector3(0f, 0.012f, 0.06f), new Vector3(0.042f, 0.05f, 0.2f), Quaternion.identity, 0.95f, 0.002f, rng);            // receiver
            s.Prism(new Vector3(0f, 0.03f, 0.1f), new Vector3(0f, 0.03f, 0.63f), 0.012f, 0.011f, 8);                                     // barrel
            s.Prism(new Vector3(0f, 0.005f, 0.22f), new Vector3(0f, 0.005f, 0.4f), 0.02f, 0.02f, 8);                                    // pump
            _shotgun = s.ToMesh("Shotgun");

            // SMG: about 35 cm, long magazine down in front of the grip, stub stock
            var m = new MeshKit.Builder();
            m.Box(new Vector3(0f, -0.05f, -0.012f), new Vector3(0.03f, 0.095f, 0.036f), Quaternion.Euler(-14f, 0f, 0f), 1f, 0.002f, rng);  // grip
            m.Box(new Vector3(0f, 0.02f, 0.06f), new Vector3(0.038f, 0.05f, 0.24f), Quaternion.identity, 0.9f, 0.002f, rng);                // receiver
            m.Box(new Vector3(0f, -0.075f, 0.1f), new Vector3(0.022f, 0.13f, 0.034f), Quaternion.Euler(8f, 0f, 0f), 1f, 0.001f, rng);       // magazine
            m.Box(new Vector3(0f, 0.008f, -0.1f), new Vector3(0.026f, 0.036f, 0.08f), Quaternion.Euler(4f, 0f, 0f), 1f, 0.001f, rng);      // stock
            m.Box(new Vector3(0f, -0.012f, 0.03f), new Vector3(0.012f, 0.022f, 0.045f), Quaternion.identity);                               // trigger guard
            m.Prism(new Vector3(0f, 0.025f, 0.18f), new Vector3(0f, 0.025f, 0.225f), 0.01f, 0.009f, 6);                                    // barrel
            _smg = m.ToMesh("SMG");

            var h = new MeshKit.Builder();
            h.Box(new Vector3(0f, 0f, 0.02f), new Vector3(0.032f, 0.036f, 0.28f), Quaternion.identity, 1f, 0.001f, rng);
            h.Prism(new Vector3(0f, 0f, 0.155f), new Vector3(0f, 0f, 0.172f), 0.05f, 0.05f, 8);   // guard
            _katanaHandle = h.ToMesh("Katana Handle");

            var b = new MeshKit.Builder();
            b.Box(new Vector3(0f, 0f, 0.5f), new Vector3(0.008f, 0.034f, 0.66f), Quaternion.identity);
            // tip: a thin wedge
            var tipBase = 0.83f;
            var c = new Vector3(0f, 0.005f, tipBase + 0.035f);
            b.Tri(new Vector3(-0.004f, -0.017f, tipBase), new Vector3(-0.004f, 0.017f, tipBase), new Vector3(0f, 0.012f, tipBase + 0.07f), c);
            b.Tri(new Vector3(0.004f, -0.017f, tipBase), new Vector3(0.004f, 0.017f, tipBase), new Vector3(0f, 0.012f, tipBase + 0.07f), c);
            b.Tri(new Vector3(-0.004f, -0.017f, tipBase), new Vector3(0.004f, -0.017f, tipBase), new Vector3(0f, 0.012f, tipBase + 0.07f), c);
            _katanaBlade = b.ToMesh("Katana Blade");

            var bo = new MeshKit.Builder();
            bo.Prism(new Vector3(0f, -0.12f, 0f), new Vector3(0f, 0.06f, 0f), 0.036f, 0.036f, 8, 0.0015f, rng);
            bo.Prism(new Vector3(0f, 0.06f, 0f), new Vector3(0f, 0.1f, 0f), 0.036f, 0.014f, 8);
            bo.Prism(new Vector3(0f, 0.1f, 0f), new Vector3(0f, 0.16f, 0f), 0.013f, 0.012f, 8);
            _bottle = bo.ToMesh("Bottle");

            var mu = new MeshKit.Builder();
            mu.Prism(new Vector3(0f, -0.048f, 0f), new Vector3(0f, 0.048f, 0f), 0.04f, 0.041f, 8, 0.001f, rng);   // body
            mu.Box(new Vector3(0.052f, 0.024f, 0f), new Vector3(0.024f, 0.012f, 0.012f), Quaternion.identity);      // handle: top
            mu.Box(new Vector3(0.052f, -0.024f, 0f), new Vector3(0.024f, 0.012f, 0.012f), Quaternion.identity);     // bottom
            mu.Box(new Vector3(0.064f, 0f, 0f), new Vector3(0.012f, 0.06f, 0.012f), Quaternion.identity);            // side
            _mug = mu.ToMesh("Mug");

            var a = new MeshKit.Builder();
            a.Prism(new Vector3(0f, -0.013f, 0f), new Vector3(0f, 0.011f, 0f), 0.068f, 0.06f, 8, 0.001f, rng);
            for (int i = 0; i < 3; i++)   // cigarette rests on the rim
            {
                float ang = i * 120f;
                var dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                a.Box(dir * 0.052f + Vector3.up * 0.015f, new Vector3(0.022f, 0.009f, 0.016f), Quaternion.Euler(0f, ang, 0f));
            }
            _ashtray = a.ToMesh("Ashtray");

            var bl = new MeshKit.Builder();
            bl.Box(Vector3.zero, Vector3.one * 0.085f, Quaternion.identity, 0.94f, 0.002f, rng);
            _block = bl.ToMesh("Block");

            var ba = new MeshKit.Builder();
            ba.Ico(Vector3.zero, Vector3.one * 0.045f, 0.06f, rng);
            _ball = ba.ToMesh("Ball");

            var t = new MeshKit.Builder();
            t.Octahedron(Vector3.zero, 0.5f, 0.75f);
            _target = t.ToMesh("Target");

            // unit menu cube: slightly irregular facets, like the throwable block
            var mc = new MeshKit.Builder();
            mc.Box(Vector3.zero, Vector3.one, Quaternion.identity, 0.94f, 0.025f, rng);
            _menuCube = mc.ToMesh("Menu Cube");
        }
    }
}
