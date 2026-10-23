using System;
using UnityEngine;
using UnityEngine.AI;

namespace Stopklatka
{
    /// <summary>Builds enemies, weapons, bottles and menu targets from code-generated meshes.</summary>
    public static class Factory
    {
        static Mesh _pistol, _shotgun, _katanaHandle, _katanaBlade, _bottle, _target;

        // Pose of a held gun relative to the controller anchor. Barrel follows the controller's forward axis.
        // If guns feel tilted on the headset, tune these two values first.
        public static Vector3 GunHoldPosition = new Vector3(0f, -0.015f, 0.005f);
        public static Vector3 GunHoldEuler = new Vector3(0f, 0f, 0f);

        public static Enemy CreateEnemy(Vector3 position, Quaternion rotation, int wave, GunKind? gun)
        {
            var root = new GameObject("Enemy") { layer = Layers.Enemy };
            root.transform.SetPositionAndRotation(position, rotation);

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.86f, 0f);
            capsule.height = 1.72f;
            capsule.radius = 0.2f;
            var head = root.AddComponent<SphereCollider>();
            head.center = new Vector3(0f, 1.63f, 0f);
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

            var body = EnemyBody.Build(root.transform, Mats.Get(Mats.Enemy));
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Enemy;

            Gun weapon = gun.HasValue ? CreateGun(gun.Value, position, rotation) : null;
            var enemy = root.AddComponent<Enemy>();
            var rooms = RoomService.Instance;
            enemy.Init(body, agent, weapon, wave, rooms != null ? rooms.NavAgentTypeId : 0, rooms != null && rooms.NavReady);
            return enemy;
        }

        public static Gun CreateGun(GunKind kind, Vector3 position, Quaternion rotation)
        {
            EnsureMeshes();
            var go = new GameObject(kind == GunKind.Shotgun ? "Shotgun" : "Pistol");
            go.transform.SetPositionAndRotation(position, rotation);
            MeshKit.Part("Mesh", go.transform, kind == GunKind.Shotgun ? _shotgun : _pistol, Mats.Get(Mats.Weapon));

            var col = go.AddComponent<BoxCollider>();
            if (kind == GunKind.Shotgun) { col.center = new Vector3(0f, 0.01f, 0.18f); col.size = new Vector3(0.05f, 0.1f, 0.85f); }
            else { col.center = new Vector3(0f, -0.01f, 0.05f); col.size = new Vector3(0.04f, 0.12f, 0.19f); }

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = kind == GunKind.Shotgun ? 1.6f : 0.9f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.3f;

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(go.transform, false);
            muzzle.localPosition = kind == GunKind.Shotgun ? new Vector3(0f, 0.03f, 0.62f) : new Vector3(0f, 0.022f, 0.155f);

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
            MeshKit.Part("Handle", go.transform, _katanaHandle, Mats.Get(Mats.Weapon));
            MeshKit.Part("Blade", go.transform, _katanaBlade, Mats.Get(Mats.Blade));

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

        public static Throwable CreateBottle(Vector3 position, Quaternion rotation)
        {
            EnsureMeshes();
            var go = new GameObject("Bottle");
            go.transform.SetPositionAndRotation(position, rotation);
            MeshKit.Part("Mesh", go.transform, _bottle, Mats.Get(Mats.Glass));
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.02f, 0f);
            col.height = 0.28f;
            col.radius = 0.037f;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.45f;
            var t = go.AddComponent<Throwable>();
            t.HoldPosition = new Vector3(0f, -0.01f, 0.01f);
            t.HoldEuler = new Vector3(90f, 0f, 0f);   // neck forward
            return t;
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
            target.Setup(label, visual.GetComponent<Renderer>(), onActivated);
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

            var t = new MeshKit.Builder();
            t.Octahedron(Vector3.zero, 0.5f, 0.75f);
            _target = t.ToMesh("Target");
        }
    }
}
