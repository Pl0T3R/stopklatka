using System.Collections.Generic;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// Builds the white low-poly level props (STYLE_GUIDE 9) from code meshes. Each prop is a root on
    /// the room layer (its box collider stops bullets and blocks sight like real furniture) with one
    /// renderer child, so it costs one draw call. Shapes are shared or cached per size; only
    /// <see cref="LevelProps"/> decides where they go (after <see cref="PropPlanner"/>'s safety checks).
    ///   furniture: plinth (covers the furniture box), counter, car (fits inside the footprint), statue,
    ///   walls (flat, back on the wall at local z = 0, sticking out along +z by at most the given depth):
    ///   window light, floor light, half column, lamp post, rail, trim, elevator frame.
    /// </summary>
    public static class PropKit
    {
        static Mesh _plinth, _counter, _car, _windowQuad, _floorQuad, _trim;
        static readonly Dictionary<long, Mesh> Sized = new();

        // nominal car (m): width 1.8, length 4.2, height 1.37, wheels on the ground at y = 0
        const float CarWidth = 1.8f, CarLength = 4.2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Sized.Clear();

        // --- furniture props -------------------------------------------------------------

        /// <summary>A white block exactly over a furniture box (1 cm inside it): plinth, kerb block.</summary>
        public static GameObject Plinth(PropPlanner.Footprint fp, float height, Material mat)
        {
            EnsureMeshes();
            var root = Root("Plinth", fp.Center, Quaternion.LookRotation(fp.Forward, Vector3.up));
            root.transform.localScale = new Vector3(fp.HalfX * 2f - 0.02f, height, fp.HalfZ * 2f - 0.02f);
            Visual(root, _plinth, mat);
            return root;
        }

        /// <summary>The reception counter of the HOL: a block with a groove and a top slab, over a table.</summary>
        public static GameObject Counter(PropPlanner.Footprint fp, float height, Material mat)
        {
            EnsureMeshes();
            var root = Root("Counter", fp.Center, Quaternion.LookRotation(fp.Forward, Vector3.up));
            root.transform.localScale = new Vector3(fp.HalfX * 2f - 0.02f, height, fp.HalfZ * 2f - 0.02f);
            Visual(root, _counter, mat);
            return root;
        }

        /// <summary>A white sedan scaled uniformly to fit inside the footprint, along its longer side.</summary>
        public static GameObject Car(PropPlanner.Footprint fp, Material mat)
        {
            EnsureMeshes();
            bool alongForward = fp.HalfZ >= fp.HalfX;
            float length = 2f * (alongForward ? fp.HalfZ : fp.HalfX);
            float width = 2f * (alongForward ? fp.HalfX : fp.HalfZ);
            float scale = Mathf.Min(width / CarWidth, length / CarLength) * 0.97f;
            var root = Root("Car", fp.Center, Quaternion.LookRotation(alongForward ? fp.Forward : fp.Right, Vector3.up));
            root.transform.localScale = Vector3.one * scale;
            Visual(root, _car, mat);
            return root;
        }

        /// <summary>A white statue (the enemy figure, posed) standing on top of a footprint, facing a point.</summary>
        public static GameObject Statue(PropPlanner.Footprint fp, int pose, float height, Vector3 facePoint, Material mat)
        {
            var mesh = EnemyBody.StatueMesh(pose);
            var b = mesh.bounds;
            // fit: the height asked for, and any turn of it stays inside the top of the footprint
            float diagonal = new Vector2(b.size.x, b.size.z).magnitude;
            float scale = Mathf.Min(height / Mathf.Max(0.1f, b.size.y), fp.MinSide * 0.9f / Mathf.Max(0.1f, diagonal));
            var face = facePoint - fp.Center;
            face.y = 0f;
            var rotation = Quaternion.LookRotation(face.sqrMagnitude > 1e-4f ? face.normalized : fp.Forward, Vector3.up);
            var offset = rotation * new Vector3(b.center.x, b.min.y, b.center.z) * scale;
            var root = Root("Statue", new Vector3(fp.Center.x, fp.TopY, fp.Center.z) - offset, rotation);
            root.transform.localScale = Vector3.one * scale;
            Visual(root, mesh, mat);
            return root;
        }

        // --- wall props ------------------------------------------------------------------

        /// <summary>A tall soft-edged white panel on the wall: light through a window (unlit, no collider).</summary>
        public static GameObject WindowLight(PropPlanner.Wall w, float s, float width, float bottom, float top, Material mat)
        {
            EnsureMeshes();
            var go = new GameObject("WindowLight");
            go.transform.SetPositionAndRotation(OnWall(w, s, bottom) + w.Inward * 0.02f, Quaternion.LookRotation(w.Inward, Vector3.up));
            go.transform.localScale = new Vector3(width, top - bottom, 1f);
            Visual(go, _windowQuad, mat);
            return go;
        }

        /// <summary>A pale light rectangle on the floor in front of a window panel (a decal: no collider).</summary>
        public static GameObject FloorLight(PropPlanner.Wall w, float s, float width, float depth, Material mat)
        {
            EnsureMeshes();
            var go = new GameObject("FloorLight");
            go.transform.SetPositionAndRotation(OnWall(w, s, w.BottomY + 0.004f) + w.Inward * 0.03f, Quaternion.LookRotation(w.Inward, Vector3.up));
            go.transform.localScale = new Vector3(width, 1f, depth);
            Visual(go, _floorQuad, mat);
            return go;
        }

        /// <summary>Half a column against the wall: base, faceted shaft, capital.</summary>
        public static GameObject HalfColumn(PropPlanner.Wall w, float s, float height, float depth, Material mat)
        {
            long key = Key(1, height, depth);
            if (!Sized.TryGetValue(key, out var mesh) || mesh == null)
            {
                var b = new MeshKit.Builder();
                b.Box(new Vector3(0f, 0.06f, depth * 0.5f), new Vector3(ColumnWidth, 0.12f, depth), Quaternion.identity);
                b.HalfPrism(new Vector3(0f, 0.12f, 0f), depth * 0.78f, Mathf.Max(0.1f, height - 0.24f), 4);
                b.Box(new Vector3(0f, height - 0.06f, depth * 0.5f), new Vector3(ColumnWidth, 0.12f, depth), Quaternion.identity);
                mesh = Sized[key] = b.ToMesh("Half Column");
            }
            return WallProp("HalfColumn", w, s, mesh, mat);
        }

        public const float ColumnWidth = 0.36f;
        /// <summary>A lamp post occupies [s, s + LampWidth] along the wall (post at the left, head to the right).</summary>
        public const float LampWidth = 0.46f;

        /// <summary>A street lamp flat against the wall: base, post, an arm along the wall and a lamp head.</summary>
        public static GameObject LampPost(PropPlanner.Wall w, float s, float height, float depth, Material mat)
        {
            float d = Mathf.Min(depth, 0.14f);
            long key = Key(2, height, d);
            if (!Sized.TryGetValue(key, out var mesh) || mesh == null)
            {
                var b = new MeshKit.Builder();
                b.Box(new Vector3(0f, 0.1f, d * 0.5f), new Vector3(0.16f, 0.2f, d), Quaternion.identity, 0.8f);
                b.Box(new Vector3(0f, height * 0.5f, d * 0.45f), new Vector3(0.06f, height, d * 0.5f), Quaternion.identity);
                b.Box(new Vector3(0.17f, height - 0.08f, d * 0.45f), new Vector3(0.34f, 0.04f, d * 0.4f), Quaternion.identity);
                b.Box(new Vector3(0.3f, height - 0.15f, d * 0.5f), new Vector3(0.16f, 0.08f, d), Quaternion.identity, 0.7f);
                mesh = Sized[key] = b.ToMesh("Lamp Post");
            }
            return WallProp("LampPost", w, s + 0.08f, mesh, mat);
        }

        /// <summary>A low barrier of vertical bars between two rails, against the wall from `from` to `to`.</summary>
        public static GameObject Rail(PropPlanner.Wall w, float from, float to, float depth, Material mat)
        {
            float length = to - from;
            float d = Mathf.Min(depth, 0.1f);
            long key = Key(3, length, d);
            if (!Sized.TryGetValue(key, out var mesh) || mesh == null)
            {
                var b = new MeshKit.Builder();
                float z = d * 0.5f;
                b.Box(new Vector3(0f, 0.88f, z), new Vector3(length, 0.05f, d * 0.6f), Quaternion.identity);
                b.Box(new Vector3(0f, 0.15f, z), new Vector3(length, 0.04f, d * 0.5f), Quaternion.identity);
                b.Box(new Vector3(-length * 0.5f + 0.03f, 0.47f, z), new Vector3(0.06f, 0.94f, d * 0.7f), Quaternion.identity);
                b.Box(new Vector3(length * 0.5f - 0.03f, 0.47f, z), new Vector3(0.06f, 0.94f, d * 0.7f), Quaternion.identity);
                int bars = Mathf.Max(0, Mathf.FloorToInt((length - 0.12f) / 0.12f));
                for (int i = 1; i <= bars; i++)
                {
                    float x = -length * 0.5f + 0.06f + i * (length - 0.12f) / (bars + 1);
                    b.Box(new Vector3(x, 0.515f, z), new Vector3(0.02f, 0.7f, 0.02f), Quaternion.identity);
                }
                mesh = Sized[key] = b.ToMesh("Rail");
            }
            return WallProp("Rail", w, (from + to) * 0.5f, mesh, mat);
        }

        /// <summary>A facade trim (cornice) high on the wall from `from` to `to`, its bottom at height y.</summary>
        public static GameObject Trim(PropPlanner.Wall w, float from, float to, float y, Material mat)
        {
            EnsureMeshes();
            var go = WallProp("Trim", w, (from + to) * 0.5f, _trim, mat, y);
            go.transform.localScale = new Vector3(to - from, 1f, 1f);
            return go;
        }

        /// <summary>
        /// An elevator frame around a door frame: two jambs just outside the opening, a lintel above it
        /// and, if there is room, a floor indicator. The opening itself stays clear.
        /// </summary>
        public static GameObject ElevatorFrame(PropPlanner.Door door, float depth, float maxTop, Material mat)
        {
            float hw = door.HalfWidth, h = door.Height;
            long key = Key(4, hw * 2f, h, maxTop);
            if (!Sized.TryGetValue(key, out var mesh) || mesh == null)
            {
                var b = new MeshKit.Builder();
                float z = depth * 0.5f;
                float jambX = hw + ElevatorGap + ElevatorJamb * 0.5f;
                float lintelTop = Mathf.Min(h + 0.2f, maxTop);
                b.Box(new Vector3(-jambX, lintelTop * 0.5f, z), new Vector3(ElevatorJamb, lintelTop, depth), Quaternion.identity);
                b.Box(new Vector3(jambX, lintelTop * 0.5f, z), new Vector3(ElevatorJamb, lintelTop, depth), Quaternion.identity);
                if (lintelTop - h > 0.06f)
                {
                    float lintelH = lintelTop - h - ElevatorGap;
                    b.Box(new Vector3(0f, h + ElevatorGap + lintelH * 0.5f, z), new Vector3(2f * (jambX + ElevatorJamb * 0.5f), lintelH, depth), Quaternion.identity);
                }
                if (lintelTop + 0.16f <= maxTop)   // the floor indicator above the lintel
                    b.Box(new Vector3(0f, lintelTop + 0.08f, depth * 0.35f), new Vector3(0.32f, 0.12f, depth * 0.7f), Quaternion.identity, 0.9f);
                mesh = Sized[key] = b.ToMesh("Elevator Frame");
            }
            var go = new GameObject("ElevatorFrame") { layer = Layers.Room };
            go.transform.SetPositionAndRotation(door.Base, Quaternion.LookRotation(door.Inward, Vector3.up));
            Visual(go, mesh, mat);
            AddCollider(go, mesh);
            return go;
        }

        /// <summary>Gap (m) between the door opening and the jambs, and the jamb width.</summary>
        public const float ElevatorGap = 0.02f, ElevatorJamb = 0.12f;

        // --- helpers ---------------------------------------------------------------------

        static Vector3 OnWall(PropPlanner.Wall w, float s, float y)
        {
            var p = w.At(s);
            p.y = y;
            return p;
        }

        static GameObject WallProp(string name, PropPlanner.Wall w, float s, Mesh mesh, Material mat, float y = float.NaN)
        {
            var go = new GameObject(name) { layer = Layers.Room };
            go.transform.SetPositionAndRotation(OnWall(w, s, float.IsNaN(y) ? w.BottomY : y), Quaternion.LookRotation(w.Inward, Vector3.up));
            Visual(go, mesh, mat);
            AddCollider(go, mesh);
            return go;
        }

        static GameObject Root(string name, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(name) { layer = Layers.Room };
            go.transform.SetPositionAndRotation(position, rotation);
            return go;
        }

        /// <summary>The renderer child (one draw call). A root on the room layer also gets a box collider.</summary>
        static void Visual(GameObject root, Mesh mesh, Material mat)
        {
            var part = MeshKit.Part("Mesh", root.transform, mesh, mat);
            part.layer = root.layer;
            if (root.layer == Layers.Room && root.GetComponent<Collider>() == null) AddCollider(root, mesh);
        }

        static void AddCollider(GameObject root, Mesh mesh)
        {
            if (root.GetComponent<Collider>() != null) return;
            var col = root.AddComponent<BoxCollider>();
            col.center = mesh.bounds.center;
            col.size = mesh.bounds.size;
        }

        /// <summary>A cache key for a sized mesh: its kind and up to three sizes in centimetres.</summary>
        static long Key(int kind, float a, float b, float c = 0f) =>
            kind * 1_000_000_000_000L + Cm(a) * 100_000_000L + Cm(b) * 10_000L + Cm(c);

        static long Cm(float metres) => Mathf.Clamp(Mathf.RoundToInt(metres * 100f), 0, 9999);

        static void EnsureMeshes()
        {
            if (_plinth != null) return;
            var rng = new System.Random(606);

            // unit plinth (x, z in -0.5..0.5, y in 0..1): base slab, body, top slab; never wider than 1
            var p = new MeshKit.Builder();
            p.Box(new Vector3(0f, 0.03f, 0f), new Vector3(1f, 0.06f, 1f), Quaternion.identity);
            p.Box(new Vector3(0f, 0.5f, 0f), new Vector3(0.94f, 0.88f, 0.94f), Quaternion.identity);
            p.Box(new Vector3(0f, 0.97f, 0f), new Vector3(1f, 0.06f, 1f), Quaternion.identity);
            _plinth = p.ToMesh("Plinth");

            // unit counter: lower body, a groove, upper body, top slab
            var c = new MeshKit.Builder();
            c.Box(new Vector3(0f, 0.24f, 0f), new Vector3(0.97f, 0.48f, 0.97f), Quaternion.identity);
            c.Box(new Vector3(0f, 0.51f, 0f), new Vector3(0.92f, 0.06f, 0.92f), Quaternion.identity);
            c.Box(new Vector3(0f, 0.735f, 0f), new Vector3(0.97f, 0.39f, 0.97f), Quaternion.identity);
            c.Box(new Vector3(0f, 0.965f, 0f), new Vector3(1f, 0.07f, 1f), Quaternion.identity);
            _counter = c.ToMesh("Counter");

            // sedan from a few blocks (STYLE_GUIDE 2, image 1): body, cabin, bumpers, four wheels
            var car = new MeshKit.Builder();
            car.Box(new Vector3(0f, 0.62f, 0f), new Vector3(1.76f, 0.5f, 3.9f), Quaternion.identity, 0.96f, 0.01f, rng);
            car.Box(new Vector3(0f, 1.12f, -0.2f), new Vector3(1.5f, 0.5f, 2.0f), Quaternion.identity, 0.78f, 0.01f, rng);
            car.Box(new Vector3(0f, 0.48f, 2.0f), new Vector3(1.7f, 0.22f, 0.2f), Quaternion.identity, 0.95f);
            car.Box(new Vector3(0f, 0.48f, -2.0f), new Vector3(1.7f, 0.22f, 0.2f), Quaternion.identity, 0.95f);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? 1f : -1f, z = i < 2 ? 1.3f : -1.3f;
                car.Prism(new Vector3(x * 0.68f, 0.33f, z), new Vector3(x * 0.9f, 0.33f, z), 0.33f, 0.33f, 8);
            }
            _car = car.ToMesh("Car");

            // soft-edged unit panel (x -0.5..0.5, y 0..1): solid inside, clear at the border
            _windowQuad = SoftRect("Window Light", false);
            _floorQuad = SoftRect("Floor Light", true);

            // unit-length trim (x -0.5..0.5): cornice band, a thinner band above and a lip below
            var t = new MeshKit.Builder();
            t.Box(new Vector3(0f, 0.06f, 0.04f), new Vector3(1f, 0.12f, 0.08f), Quaternion.identity);
            t.Box(new Vector3(0f, 0.16f, 0.025f), new Vector3(1f, 0.08f, 0.05f), Quaternion.identity);
            t.Box(new Vector3(0f, -0.02f, 0.02f), new Vector3(1f, 0.04f, 0.04f), Quaternion.identity);
            _trim = t.ToMesh("Trim");
        }

        /// <summary>
        /// A flat rectangle with an inner part and a border ring whose outer edge is transparent (vertex
        /// alpha), for the window light on walls (XY) and the light on the floor (XZ, fading away from the wall).
        /// </summary>
        static Mesh SoftRect(string name, bool onFloor)
        {
            const float edge = 0.15f;
            var b = new MeshKit.Builder();
            Vector3 P(float x, float v) => onFloor ? new Vector3(x, 0f, v) : new Vector3(x, v, 0f);
            var o0 = P(-0.5f, 0f); var o1 = P(0.5f, 0f); var o2 = P(0.5f, 1f); var o3 = P(-0.5f, 1f);
            var i0 = P(-0.5f + edge, edge); var i1 = P(0.5f - edge, edge); var i2 = P(0.5f - edge, 1f - edge); var i3 = P(-0.5f + edge, 1f - edge);
            var behind = onFloor ? new Vector3(0f, -1f, 0.5f) : new Vector3(0f, 0.5f, -1f);
            b.Quad(i0, i1, i2, i3, behind);
            b.Quad(o0, o1, i1, i0, behind);
            b.Quad(o1, o2, i2, i1, behind);
            b.Quad(o2, o3, i3, i2, behind);
            b.Quad(o3, o0, i0, i3, behind);
            return b.ToMesh(name, pos =>
            {
                float v = onFloor ? pos.z : pos.y;
                bool outer = Mathf.Abs(Mathf.Abs(pos.x) - 0.5f) < 1e-3f || v < 1e-3f || v > 1f - 1e-3f;
                float a = outer ? 0f : 1f;
                if (onFloor) a *= 1f - 0.65f * v;   // light spill fades away from the wall
                return new Color(1f, 1f, 1f, a);
            });
        }
    }
}
