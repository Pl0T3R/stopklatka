using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// One rigid piece of the enemy figure, and where it breaks off its parent: the glowing cap of
    /// a big chunk sits there (STYLE_GUIDE 8.3).
    /// </summary>
    public sealed class BodyPart
    {
        public Renderer Renderer;
        public Mesh Mesh;
        /// <summary>Cap center, its outward normal and its x/z radii, in the part's own space.</summary>
        public Vector3 CapPosition, CapNormal;
        public Vector2 CapRadius;
        /// <summary>The part is a limb along its local Y axis (it lies down when it comes to rest).</summary>
        public bool Limb;
    }

    /// <summary>
    /// The red crystal figure (STYLE_GUIDE 6): about 1.8 m, broad shoulders, narrow waist, long legs,
    /// an elongated narrow head with no face. 11 faceted rigid parts (hips, chest as an inverted
    /// wedge, head with neck, upper arms, forearms with block hands, thighs, shins with feet),
    /// animated in code: walk, aim with a rising arm, punch with a wind-up, guard, wobble.
    /// Plus a soft shadow on the floor. All enemies share the same meshes and material, so with the
    /// SRP batcher that's 12 cheap draw calls per enemy. While spawning it materializes from the
    /// floor up (Reveal), using a separate material only then.
    /// </summary>
    public class EnemyBody : MonoBehaviour
    {
        public Transform Hips, Chest, Head, ShoulderL, ShoulderR, ElbowL, ElbowR, HandSocketR, HipL, HipR, KneeL, KneeR, Shadow;
        /// <summary>All part renderers (for the reveal material).</summary>
        public Renderer[] Parts;
        /// <summary>The same parts with their break caps (for shattering and the katana cut).</summary>
        public BodyPart[] Pieces;

        /// <summary>World point the right arm aims at, or null for a relaxed walk.</summary>
        public Vector3? AimTarget;
        /// <summary>0..1: how far the aiming arm has come up (it rises while the enemy takes aim).</summary>
        public float AimBlend = 1f;
        /// <summary>0..1: the arm pulled back before a punch.</summary>
        public float Windup;
        /// <summary>0..1 punch extension toward PunchTarget.</summary>
        public float Punch;
        public Vector3 PunchTarget;
        /// <summary>0..1: arms crossed before the face (a bullet flew close or a neighbour died).</summary>
        public float Guard;
        /// <summary>0..1 spawn-in progress (grows the floor shadow).</summary>
        public float Appear = 1f;
        public float Wobble;

        float _phase, _hipsBaseY;
        Quaternion _armR = Quaternion.identity, _elbowR = Quaternion.identity, _armL = Quaternion.identity, _elbowL = Quaternion.identity;

        public const float HipY = 0.94f;
        const float ShoulderX = 0.215f, ShoulderY = 0.27f;   // relative to the chest pivot

        static readonly int RevealYId = Shader.PropertyToID("_RevealY");
        static MaterialPropertyBlock _revealBlock;
        static Mesh _hipsMesh, _chestMesh, _headMesh, _upperArmMesh, _forearmMesh, _thighMesh, _shinMesh, _shadowMesh;

        // Arm poses in the chest's frame (x right, y up, z forward), as directions of the upper arm and forearm.
        static readonly Vector3 GuardUpperR = new Vector3(-0.25f, 0.1f, 1f).normalized;
        static readonly Vector3 GuardForeR = new Vector3(-1f, 0.55f, 0.15f).normalized;
        static readonly Vector3 GuardUpperL = new Vector3(0.25f, 0.12f, 1f).normalized;
        static readonly Vector3 GuardForeL = new Vector3(1f, 0.7f, 0.1f).normalized;
        static readonly Vector3 WindupUpperR = new Vector3(0.35f, -0.55f, -0.75f).normalized;
        static readonly Vector3 WindupForeR = new Vector3(0.1f, 0.35f, 1f).normalized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _revealBlock = null;

        /// <summary>Switches to the reveal material: everything above worldY is cut away.</summary>
        public void BeginReveal(Material reveal, float worldY)
        {
            foreach (var r in Parts) r.sharedMaterial = reveal;
            SetReveal(worldY);
        }

        public void SetReveal(float worldY)
        {
            _revealBlock ??= new MaterialPropertyBlock();
            _revealBlock.SetFloat(RevealYId, worldY);
            foreach (var r in Parts) r.SetPropertyBlock(_revealBlock);
        }

        /// <summary>Back to the shared material (and the SRP batcher).</summary>
        public void EndReveal(Material normal)
        {
            foreach (var r in Parts)
            {
                r.SetPropertyBlock(null);
                r.sharedMaterial = normal;
            }
        }

        /// <summary>
        /// The part nearest to a world point, and the point on that part's box nearest to it
        /// (close to the real surface: hit colliders are a loose capsule).
        /// </summary>
        public int NearestPart(Vector3 point, out Vector3 surfacePoint)
        {
            int best = 0;
            float bestD = float.MaxValue;
            surfacePoint = point;
            for (int i = 0; i < Pieces.Length; i++)
            {
                var t = Pieces[i].Renderer.transform;
                var b = Pieces[i].Mesh.bounds;
                var local = t.InverseTransformPoint(point);
                var clamped = new Vector3(Mathf.Clamp(local.x, b.min.x, b.max.x), Mathf.Clamp(local.y, b.min.y, b.max.y), Mathf.Clamp(local.z, b.min.z, b.max.z));
                var world = t.TransformPoint(clamped);
                float d = (world - point).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; surfacePoint = world; }
            }
            return best;
        }

        /// <summary>Distance from a world point to a part's box (0 inside).</summary>
        public float DistanceToPart(int index, Vector3 point)
        {
            var t = Pieces[index].Renderer.transform;
            var b = Pieces[index].Mesh.bounds;
            var local = t.InverseTransformPoint(point);
            var clamped = new Vector3(Mathf.Clamp(local.x, b.min.x, b.max.x), Mathf.Clamp(local.y, b.min.y, b.max.y), Mathf.Clamp(local.z, b.min.z, b.max.z));
            return Vector3.Distance(t.TransformPoint(clamped), point);
        }

        /// <summary>Width (x) and depth (y) of the body's cross-section at a height above the feet, for cut faces.</summary>
        public static Vector2 CrossSection(float height)
        {
            if (height < 0.5f) return new Vector2(0.3f, 0.13f);      // both shins
            if (height < 0.88f) return new Vector2(0.36f, 0.17f);    // thighs
            if (height < 1.0f) return new Vector2(0.33f, 0.21f);     // pelvis
            if (height < 1.16f) return new Vector2(0.27f, 0.18f);    // waist
            if (height < 1.43f) return Vector2.Lerp(new Vector2(0.3f, 0.2f), new Vector2(0.48f, 0.23f), (height - 1.16f) / 0.27f); // chest wedge
            if (height < 1.52f) return new Vector2(0.26f, 0.17f);    // shoulders to neck
            if (height < 1.56f) return new Vector2(0.1f, 0.1f);      // neck
            return new Vector2(0.18f, 0.2f);                         // head
        }

        public void Animate(float dt, Vector3 worldVelocity)
        {
            if (Shadow != null) Shadow.localScale = Vector3.one * Mathf.SmoothStep(0.2f, 1f, Appear);

            var flatVel = Vector3.ProjectOnPlane(worldVelocity, Vector3.up);
            float speed = flatVel.magnitude;
            float walk = Mathf.Clamp01(speed / 0.8f);
            _phase += speed * dt / 1.4f * Mathf.PI * 2f;
            float s = Mathf.Sin(_phase), c = Mathf.Cos(_phase);

            // legs: a long stride
            HipL.localRotation = Quaternion.Euler(s * 30f * walk, 0f, 0f);
            HipR.localRotation = Quaternion.Euler(-s * 30f * walk, 0f, 0f);
            KneeL.localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Sin(_phase - 0.9f)) * 50f * walk, 0f, 0f);
            KneeR.localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Sin(_phase + Mathf.PI - 0.9f)) * 50f * walk, 0f, 0f);

            // hips bob and twist with the stride; the chest wobbles (stunned, cracking), winds up a punch, leans back in guard
            float breathe = Mathf.Sin(Time.time * 1.7f) * 0.006f;
            var hipsPos = Hips.localPosition;
            hipsPos.y = _hipsBaseY + Mathf.Abs(c) * 0.025f * walk + breathe;
            Hips.localPosition = hipsPos;
            Hips.localRotation = Quaternion.Euler(0f, s * 6f * walk, 0f);
            float wob = Wobble * 6f;
            Chest.localRotation = Quaternion.Euler(
                3f * walk - 8f * Guard + wob * Mathf.Sin(Time.time * 23f),
                -s * 4f * walk + 25f * Windup - 15f * Punch,
                wob * Mathf.Cos(Time.time * 19f));
            Head.localRotation = Quaternion.Euler(10f * Guard, 0f, 0f);

            // left arm: swings, or guards
            var armL = Quaternion.Euler(-s * 24f * walk, 0f, -6f);
            var elbowL = Quaternion.Euler(-15f - 12f * walk, 0f, 0f);
            if (Guard > 0f) Pose(GuardUpperL, GuardForeL, Guard, ref armL, ref elbowL);

            // right arm: relaxed swing -> rising aim -> wind-up -> punch -> guard
            var armR = Quaternion.Euler(s * 24f * walk, 0f, 6f);
            var elbowR = Quaternion.Euler(-15f - 12f * walk, 0f, 0f);
            if (AimTarget.HasValue && AimBlend > 0f)
            {
                var local = Chest.InverseTransformDirection((AimTarget.Value - ShoulderR.position).normalized);
                armR = Quaternion.Slerp(armR, Quaternion.FromToRotation(Vector3.down, local), AimBlend);
                elbowR = Quaternion.Slerp(elbowR, Quaternion.identity, AimBlend);
            }
            if (Windup > 0f) Pose(WindupUpperR, WindupForeR, Windup, ref armR, ref elbowR);
            if (Punch > 0f)
            {
                var local = Chest.InverseTransformDirection((PunchTarget - ShoulderR.position).normalized);
                armR = Quaternion.Slerp(armR, Quaternion.FromToRotation(Vector3.down, local), Punch);
                elbowR = Quaternion.Slerp(elbowR, Quaternion.identity, Punch);
            }
            if (Guard > 0f) Pose(GuardUpperR, GuardForeR, Guard, ref armR, ref elbowR);

            // smoothed, so poses blend into each other; faster while striking
            float k = 1f - Mathf.Exp(-(Punch > 0f ? 20f : 12f) * dt);
            _armR = Quaternion.Slerp(_armR, armR, k);
            _elbowR = Quaternion.Slerp(_elbowR, elbowR, k);
            _armL = Quaternion.Slerp(_armL, armL, k);
            _elbowL = Quaternion.Slerp(_elbowL, elbowL, k);
            ShoulderR.localRotation = _armR;
            ElbowR.localRotation = _elbowR;
            ShoulderL.localRotation = _armL;
            ElbowL.localRotation = _elbowL;
        }

        /// <summary>Blends an arm toward a pose given as upper-arm and forearm directions in the chest's frame.</summary>
        static void Pose(Vector3 upperDir, Vector3 foreDir, float weight, ref Quaternion shoulder, ref Quaternion elbow)
        {
            var upper = Quaternion.FromToRotation(Vector3.down, upperDir);
            var fore = Quaternion.Inverse(upper) * Quaternion.FromToRotation(Vector3.down, foreDir);
            shoulder = Quaternion.Slerp(shoulder, upper, weight);
            elbow = Quaternion.Slerp(elbow, fore, weight);
        }

        public static EnemyBody Build(Transform root, Material mat, Material shadowMat)
        {
            EnsureMeshes();
            var body = root.gameObject.AddComponent<EnemyBody>();

            var shadow = MeshKit.Part("Shadow", root, _shadowMesh, shadowMat);
            shadow.transform.localPosition = new Vector3(0f, 0.008f, 0f);
            body.Shadow = shadow.transform;

            body.Hips = Pivot("Hips", root, new Vector3(0f, HipY, 0f));
            body.Chest = Pivot("Chest", body.Hips, new Vector3(0f, 0.22f, 0.01f));
            body.Head = Pivot("Head", body.Chest, new Vector3(0f, 0.34f, 0f));
            body.ShoulderL = Pivot("ShoulderL", body.Chest, new Vector3(-ShoulderX, ShoulderY, 0f));
            body.ShoulderR = Pivot("ShoulderR", body.Chest, new Vector3(ShoulderX, ShoulderY, 0f));
            body.ElbowL = Pivot("ElbowL", body.ShoulderL, new Vector3(0f, -0.29f, 0f));
            body.ElbowR = Pivot("ElbowR", body.ShoulderR, new Vector3(0f, -0.29f, 0f));
            body.HandSocketR = Pivot("HandSocketR", body.ElbowR, new Vector3(0f, -0.31f, 0.02f));
            body.HandSocketR.localRotation = Quaternion.Euler(90f, 0f, 0f);   // weapon forward = along the arm
            body.HipL = Pivot("HipL", root, new Vector3(-0.095f, HipY, 0f));
            body.HipR = Pivot("HipR", root, new Vector3(0.095f, HipY, 0f));
            body.KneeL = Pivot("KneeL", body.HipL, new Vector3(0f, -0.46f, 0f));
            body.KneeR = Pivot("KneeR", body.HipR, new Vector3(0f, -0.46f, 0f));

            var up = Vector3.up;
            var down = Vector3.down;
            body.Pieces = new[]
            {
                Piece("HipsMesh", body.Hips, _hipsMesh, mat, new Vector3(0f, 0.22f, 0.01f), up, new Vector2(0.13f, 0.088f), false),
                Piece("ChestMesh", body.Chest, _chestMesh, mat, new Vector3(0f, -0.03f, 0f), down, new Vector2(0.13f, 0.085f), false),
                Piece("HeadMesh", body.Head, _headMesh, mat, new Vector3(0f, -0.02f, 0f), down, new Vector2(0.05f, 0.05f), false),
                Piece("UpperArmL", body.ShoulderL, _upperArmMesh, mat, new Vector3(0f, 0.035f, 0f), up, new Vector2(0.064f, 0.06f), true),
                Piece("UpperArmR", body.ShoulderR, _upperArmMesh, mat, new Vector3(0f, 0.035f, 0f), up, new Vector2(0.064f, 0.06f), true),
                Piece("ForearmL", body.ElbowL, _forearmMesh, mat, new Vector3(0f, 0.02f, 0f), up, new Vector2(0.048f, 0.048f), true),
                Piece("ForearmR", body.ElbowR, _forearmMesh, mat, new Vector3(0f, 0.02f, 0f), up, new Vector2(0.048f, 0.048f), true),
                Piece("ThighL", body.HipL, _thighMesh, mat, new Vector3(0f, 0.04f, 0f), up, new Vector2(0.086f, 0.086f), true),
                Piece("ThighR", body.HipR, _thighMesh, mat, new Vector3(0f, 0.04f, 0f), up, new Vector2(0.086f, 0.086f), true),
                Piece("ShinL", body.KneeL, _shinMesh, mat, new Vector3(0f, 0.025f, 0f), up, new Vector2(0.062f, 0.062f), true),
                Piece("ShinR", body.KneeR, _shinMesh, mat, new Vector3(0f, 0.025f, 0f), up, new Vector2(0.062f, 0.062f), true),
            };
            body.Parts = new Renderer[body.Pieces.Length];
            for (int i = 0; i < body.Pieces.Length; i++) body.Parts[i] = body.Pieces[i].Renderer;
            body._hipsBaseY = body.Hips.localPosition.y;
            return body;
        }

        static BodyPart Piece(string name, Transform parent, Mesh mesh, Material mat, Vector3 capPos, Vector3 capNormal, Vector2 capRadius, bool limb)
        {
            var go = MeshKit.Part(name, parent, mesh, mat);
            return new BodyPart
            {
                Renderer = go.GetComponent<MeshRenderer>(),
                Mesh = mesh,
                CapPosition = capPos,
                CapNormal = capNormal,
                CapRadius = capRadius,
                Limb = limb,
            };
        }

        static Transform Pivot(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        /// <summary>
        /// Part meshes, each built around its own pivot (the joint it hangs from).
        /// Rings for lofts: (height, x radius, z radius, z offset).
        /// </summary>
        static void EnsureMeshes()
        {
            if (_hipsMesh != null) return;
            var rng = new System.Random(2024);

            // hips: crotch, pelvis (widest), abdomen, narrow waist
            var hips = new MeshKit.Builder();
            hips.Loft(Vector3.zero, new[]
            {
                new Vector4(-0.12f, 0.09f, 0.075f, 0f), new Vector4(-0.05f, 0.165f, 0.105f, 0f),
                new Vector4(0.06f, 0.15f, 0.1f, 0.005f), new Vector4(0.22f, 0.13f, 0.088f, 0.01f),
            }, 6, 0f, 0.006f, rng);
            _hipsMesh = hips.ToMesh("Enemy Hips");

            // chest: an inverted wedge from the waist up to broad pointed shoulders, sloping to the neck
            var chest = new MeshKit.Builder();
            chest.Loft(Vector3.zero, new[]
            {
                new Vector4(-0.03f, 0.13f, 0.085f, 0f), new Vector4(0.12f, 0.185f, 0.115f, 0.012f),
                new Vector4(0.27f, 0.245f, 0.115f, 0f), new Vector4(0.35f, 0.13f, 0.085f, -0.01f),
            }, 6, 0f, 0.008f, rng);
            _chestMesh = chest.ToMesh("Enemy Chest");

            // head: neck plus a long narrow faceless crystal (about 0.18 x 0.3 m), chin a little forward
            var head = new MeshKit.Builder();
            head.Prism(new Vector3(0f, -0.02f, 0f), new Vector3(0f, 0.08f, 0.005f), 0.048f, 0.042f, 6, 0.003f, rng);
            head.Loft(Vector3.zero, new[]
            {
                new Vector4(0.04f, 0.04f, 0.05f, 0.035f), new Vector4(0.1f, 0.078f, 0.088f, 0.02f),
                new Vector4(0.2f, 0.09f, 0.1f, 0f), new Vector4(0.27f, 0.078f, 0.092f, -0.008f),
                new Vector4(0.3f, 0.035f, 0.05f, -0.012f),
            }, 6, 0f, 0.006f, rng);
            _headMesh = head.ToMesh("Enemy Head");

            // upper arm: deltoid block on a tapered prism (flat top, where a break cap fits)
            var ua = new MeshKit.Builder();
            ua.Box(new Vector3(0f, -0.02f, 0f), new Vector3(0.12f, 0.1f, 0.11f), Quaternion.identity, 0.85f, 0.006f, rng);
            ua.Prism(new Vector3(0f, 0.035f, 0f), new Vector3(0f, -0.29f, 0f), 0.062f, 0.046f, 6, 0.005f, rng);
            _upperArmMesh = ua.ToMesh("Enemy Upper Arm");

            // forearm with a block hand
            var fa = new MeshKit.Builder();
            fa.Prism(new Vector3(0f, 0.02f, 0f), new Vector3(0f, -0.25f, 0f), 0.047f, 0.036f, 6, 0.005f, rng);
            fa.Box(new Vector3(0f, -0.3f, 0.008f), new Vector3(0.07f, 0.1f, 0.042f), Quaternion.identity, 0.9f, 0.006f, rng);
            _forearmMesh = fa.ToMesh("Enemy Forearm");

            var th = new MeshKit.Builder();
            th.Prism(new Vector3(0f, 0.04f, 0f), new Vector3(0f, -0.46f, 0f), 0.085f, 0.058f, 7, 0.007f, rng);
            _thighMesh = th.ToMesh("Enemy Thigh");

            // shin with a long foot
            var sh = new MeshKit.Builder();
            sh.Prism(new Vector3(0f, 0.025f, 0f), new Vector3(0f, -0.41f, 0f), 0.06f, 0.042f, 6, 0.006f, rng);
            sh.Box(new Vector3(0f, -0.44f, 0.055f), new Vector3(0.095f, 0.06f, 0.23f), Quaternion.identity, 0.85f, 0.008f, rng);
            _shinMesh = sh.ToMesh("Enemy Shin");

            // soft dark disc: opaque-ish center fading to nothing at the edge
            var sd = new MeshKit.Builder();
            sd.Ring(Vector3.zero, Quaternion.identity, 0f, 0.17f, 24);
            sd.Ring(Vector3.zero, Quaternion.identity, 0.17f, 0.36f, 24);
            _shadowMesh = sd.ToMesh("Enemy Shadow", p =>
            {
                float r = new Vector2(p.x, p.z).magnitude;
                return new Color(1f, 1f, 1f, r < 0.01f ? 1f : r < 0.2f ? 0.7f : 0f);
            });
        }
    }
}
