using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// The red crystal figure: a faceted torso+head piece and rigid limbs, animated in code
    /// (walk cycle, aiming arm, punch). All enemies share the same meshes and material, so
    /// they batch well. About 9 draw calls per enemy.
    /// </summary>
    public class EnemyBody : MonoBehaviour
    {
        public Transform Torso, ShoulderL, ShoulderR, ElbowL, ElbowR, HandSocketR, HipL, HipR, KneeL, KneeR;
        public Renderer[] Parts;

        /// <summary>World point the right arm aims at, or null for a relaxed walk.</summary>
        public Vector3? AimTarget;
        /// <summary>0..1 punch extension toward PunchTarget.</summary>
        public float Punch;
        public Vector3 PunchTarget;
        /// <summary>0..1 spawn-in progress (scales the figure up from the floor).</summary>
        public float Appear = 1f;
        public float Wobble;

        float _phase, _torsoBaseY;
        Quaternion _aimRot = Quaternion.identity;

        const float ShoulderY = 1.46f, HipY = 0.9f;

        static Mesh _torsoMesh, _upperArmMesh, _forearmMesh, _thighMesh, _shinMesh;

        public void Animate(float dt, Vector3 worldVelocity)
        {
            transform.localScale = Vector3.one * Mathf.SmoothStep(0.05f, 1f, Appear);

            var flatVel = Vector3.ProjectOnPlane(worldVelocity, Vector3.up);
            float speed = flatVel.magnitude;
            float walk = Mathf.Clamp01(speed / 0.8f);
            _phase += speed * dt / 1.4f * Mathf.PI * 2f;
            float s = Mathf.Sin(_phase), c = Mathf.Cos(_phase);

            HipL.localRotation = Quaternion.Euler(s * 28f * walk, 0f, 0f);
            HipR.localRotation = Quaternion.Euler(-s * 28f * walk, 0f, 0f);
            KneeL.localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Sin(_phase - 0.9f)) * 48f * walk, 0f, 0f);
            KneeR.localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Sin(_phase + Mathf.PI - 0.9f)) * 48f * walk, 0f, 0f);

            float breathe = Mathf.Sin(Time.time * 1.7f) * 0.006f;
            var torsoPos = Torso.localPosition;
            torsoPos.y = _torsoBaseY + Mathf.Abs(c) * 0.025f * walk + breathe;
            Torso.localPosition = torsoPos;
            Torso.localRotation = Quaternion.Euler(Wobble * Mathf.Sin(Time.time * 23f) * 6f, s * 5f * walk, Wobble * Mathf.Cos(Time.time * 19f) * 6f);

            // left arm always swings
            ShoulderL.localRotation = Quaternion.Euler(-s * 22f * walk, 0f, -6f);
            ElbowL.localRotation = Quaternion.Euler(-15f - 10f * walk, 0f, 0f);

            // right arm: aim, punch, or swing
            Quaternion target;
            if (Punch > 0f)
            {
                var local = Torso.InverseTransformDirection((PunchTarget - ShoulderR.position).normalized);
                target = Quaternion.FromToRotation(Vector3.down, local);
                ElbowR.localRotation = Quaternion.Euler(Mathf.Lerp(-80f, 0f, Punch), 0f, 0f);
            }
            else if (AimTarget.HasValue)
            {
                var local = Torso.InverseTransformDirection((AimTarget.Value - ShoulderR.position).normalized);
                target = Quaternion.FromToRotation(Vector3.down, local);
                ElbowR.localRotation = Quaternion.identity;
            }
            else
            {
                target = Quaternion.Euler(s * 22f * walk, 0f, 6f);
                ElbowR.localRotation = Quaternion.Euler(-15f - 10f * walk, 0f, 0f);
            }
            _aimRot = Quaternion.Slerp(_aimRot, target, 1f - Mathf.Exp(-12f * dt));
            ShoulderR.localRotation = _aimRot;
        }

        public static EnemyBody Build(Transform root, Material mat)
        {
            EnsureMeshes();
            var body = root.gameObject.AddComponent<EnemyBody>();

            body.Torso = new GameObject("Torso").transform;
            body.Torso.SetParent(root, false);
            var torso = MeshKit.Part("TorsoMesh", body.Torso, _torsoMesh, mat);

            body.ShoulderL = Pivot("ShoulderL", body.Torso, new Vector3(-0.235f, ShoulderY, 0f));
            body.ShoulderR = Pivot("ShoulderR", body.Torso, new Vector3(0.235f, ShoulderY, 0f));
            var upperL = MeshKit.Part("UpperArmL", body.ShoulderL, _upperArmMesh, mat);
            var upperR = MeshKit.Part("UpperArmR", body.ShoulderR, _upperArmMesh, mat);
            body.ElbowL = Pivot("ElbowL", body.ShoulderL, new Vector3(0f, -0.3f, 0f));
            body.ElbowR = Pivot("ElbowR", body.ShoulderR, new Vector3(0f, -0.3f, 0f));
            var foreL = MeshKit.Part("ForearmL", body.ElbowL, _forearmMesh, mat);
            var foreR = MeshKit.Part("ForearmR", body.ElbowR, _forearmMesh, mat);
            body.HandSocketR = Pivot("HandSocketR", body.ElbowR, new Vector3(0f, -0.32f, 0.02f));
            body.HandSocketR.localRotation = Quaternion.Euler(90f, 0f, 0f);   // weapon forward = along the arm

            body.HipL = Pivot("HipL", root, new Vector3(-0.1f, HipY, 0f));
            body.HipR = Pivot("HipR", root, new Vector3(0.1f, HipY, 0f));
            var thighL = MeshKit.Part("ThighL", body.HipL, _thighMesh, mat);
            var thighR = MeshKit.Part("ThighR", body.HipR, _thighMesh, mat);
            body.KneeL = Pivot("KneeL", body.HipL, new Vector3(0f, -0.44f, 0f));
            body.KneeR = Pivot("KneeR", body.HipR, new Vector3(0f, -0.44f, 0f));
            var shinL = MeshKit.Part("ShinL", body.KneeL, _shinMesh, mat);
            var shinR = MeshKit.Part("ShinR", body.KneeR, _shinMesh, mat);

            body.Parts = new Renderer[]
            {
                torso.GetComponent<Renderer>(), upperL.GetComponent<Renderer>(), upperR.GetComponent<Renderer>(),
                foreL.GetComponent<Renderer>(), foreR.GetComponent<Renderer>(), thighL.GetComponent<Renderer>(),
                thighR.GetComponent<Renderer>(), shinL.GetComponent<Renderer>(), shinR.GetComponent<Renderer>(),
            };
            body._torsoBaseY = body.Torso.localPosition.y;
            return body;
        }

        static Transform Pivot(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        static void EnsureMeshes()
        {
            if (_torsoMesh != null) return;
            var rng = new System.Random(2024);

            var t = new MeshKit.Builder();
            t.Ico(new Vector3(0f, 1.63f, 0.01f), new Vector3(0.105f, 0.128f, 0.118f), 0.06f, rng);                 // head
            t.Prism(new Vector3(0f, 1.44f, 0f), new Vector3(0f, 1.53f, 0f), 0.05f, 0.045f, 6);                     // neck
            t.Box(new Vector3(0f, 1.31f, 0f), new Vector3(0.42f, 0.32f, 0.22f), Quaternion.identity, 1.08f, 0.012f, rng); // chest
            t.Box(new Vector3(0f, 1.07f, 0.005f), new Vector3(0.31f, 0.18f, 0.19f), Quaternion.identity, 1.12f, 0.01f, rng); // abdomen
            t.Box(new Vector3(0f, 0.93f, 0f), new Vector3(0.35f, 0.13f, 0.21f), Quaternion.identity, 0.92f, 0.01f, rng);   // pelvis
            _torsoMesh = t.ToMesh("Enemy Torso");

            var ua = new MeshKit.Builder();
            ua.Prism(new Vector3(0f, 0.02f, 0f), new Vector3(0f, -0.29f, 0f), 0.058f, 0.045f, 6, 0.006f, rng);
            _upperArmMesh = ua.ToMesh("Enemy Upper Arm");

            var fa = new MeshKit.Builder();
            fa.Prism(new Vector3(0f, 0f, 0f), new Vector3(0f, -0.26f, 0f), 0.045f, 0.034f, 6, 0.005f, rng);
            fa.Box(new Vector3(0f, -0.31f, 0.01f), new Vector3(0.075f, 0.1f, 0.04f), Quaternion.identity, 0.9f, 0.006f, rng); // hand
            _forearmMesh = fa.ToMesh("Enemy Forearm");

            var th = new MeshKit.Builder();
            th.Prism(new Vector3(0f, 0.02f, 0f), new Vector3(0f, -0.43f, 0f), 0.085f, 0.06f, 7, 0.008f, rng);
            _thighMesh = th.ToMesh("Enemy Thigh");

            var sh = new MeshKit.Builder();
            sh.Prism(new Vector3(0f, 0f, 0f), new Vector3(0f, -0.41f, 0f), 0.06f, 0.045f, 6, 0.006f, rng);
            sh.Box(new Vector3(0f, -0.43f, 0.06f), new Vector3(0.1f, 0.06f, 0.24f), Quaternion.identity, 0.85f, 0.008f, rng); // foot
            _shinMesh = sh.ToMesh("Enemy Shin");
        }
    }
}
