using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// Reads the scanned rooms on your floor for the level props (stage 6) and answers the MR safety
    /// questions; it creates nothing:
    ///   - where is scanned furniture (a free-standing prop may only cover a furniture footprint),
    ///   - which stretches of wall are free of door frames, windows, other wall anchors and furniture
    ///     standing against the wall (wall props go only there),
    ///   - where are the door frames (elevator frames go around them, never across the opening).
    /// Only rooms whose floor is within 0.6 m of yours count, so other storeys and stairs get nothing.
    /// MRUK: MRUKRoom.Anchors / FloorAnchors / IsPositionInRoom / IsPositionInSceneVolume,
    /// MRUKAnchor.PlaneRect / VolumeBounds / HasAnyLabel (checked in the MRUK 207 sources). Box axes are
    /// read from the anchor transforms, so no assumption about which local axis is "up".
    /// </summary>
    public static class PropPlanner
    {
        /// <summary>A scanned furniture volume as an upright box.</summary>
        public struct Footprint
        {
            /// <summary>Bottom center.</summary>
            public Vector3 Center;
            /// <summary>Horizontal unit axes of the box.</summary>
            public Vector3 Right, Forward;
            public float HalfX, HalfZ, BottomY, TopY;
            public MRUKAnchor.SceneLabels Label;

            public float MinSide => 2f * Mathf.Min(HalfX, HalfZ);
            public float MaxSide => 2f * Mathf.Max(HalfX, HalfZ);
            public float Area => 4f * HalfX * HalfZ;
            public float Height => TopY - BottomY;
            public bool HasAnyLabel(MRUKAnchor.SceneLabels labels) => (Label & labels) != 0;

            /// <summary>Horizontal distance from p to the footprint rectangle (0 inside).</summary>
            public float FlatDistance(Vector3 p)
            {
                var d = p - Center;
                float x = Mathf.Max(0f, Mathf.Abs(Vector3.Dot(d, Right)) - HalfX);
                float z = Mathf.Max(0f, Mathf.Abs(Vector3.Dot(d, Forward)) - HalfZ);
                return Mathf.Sqrt(x * x + z * z);
            }
        }

        /// <summary>Something on or against a wall: an interval along the wall and a height range.</summary>
        public struct Blocker
        {
            public float From, To, Bottom, Top;
            public bool IsDoor;
            public Vector3 Center;
        }

        /// <summary>A real wall of a room on your floor, with what stands on or against it.</summary>
        public sealed class Wall
        {
            /// <summary>Bottom corner where the distance along the wall is 0.</summary>
            public Vector3 Origin;
            /// <summary>Horizontal unit vectors: along the wall, and into its room.</summary>
            public Vector3 Along, Inward;
            public float Length, BottomY, TopY;
            public readonly List<Blocker> Blockers = new();

            public Vector3 At(float s) => Origin + Along * s;
        }

        /// <summary>A door frame on your floor.</summary>
        public struct Door
        {
            /// <summary>Bottom center, on the wall plane.</summary>
            public Vector3 Base;
            /// <summary>Along the wall, and toward your side of it.</summary>
            public Vector3 Along, Inward;
            public float HalfWidth, Height;
            /// <summary>The wall it sits in on your side (null: none found, so no frame), and where along it.</summary>
            public Wall Wall;
            public float S;
        }

        public sealed class Scan
        {
            public readonly List<Footprint> Furniture = new();
            public readonly List<Wall> Walls = new();
            public readonly List<Door> Doors = new();
            public float FloorY;

            public void Clear()
            {
                Furniture.Clear();
                Walls.Clear();
                Doors.Clear();
            }
        }

        struct PlaneInfo
        {
            public Vector3 C0, C1, C2, C3, Center;
            public float Bottom, Top;
            public bool IsDoor;
        }

        const MRUKAnchor.SceneLabels FurnitureLabels = MRUKAnchor.SceneLabels.TABLE | MRUKAnchor.SceneLabels.COUCH |
                                                       MRUKAnchor.SceneLabels.STORAGE | MRUKAnchor.SceneLabels.BED |
                                                       MRUKAnchor.SceneLabels.OTHER;
        const MRUKAnchor.SceneLabels Ignored = MRUKAnchor.SceneLabels.FLOOR | MRUKAnchor.SceneLabels.CEILING |
                                               MRUKAnchor.SceneLabels.GLOBAL_MESH | MRUKAnchor.SceneLabels.INVISIBLE_WALL_FACE;
        const MRUKAnchor.SceneLabels WallLabels = MRUKAnchor.SceneLabels.WALL_FACE | MRUKAnchor.SceneLabels.INNER_WALL_FACE;

        static readonly List<PlaneInfo> Planes = new();
        static readonly List<Footprint> Volumes = new();
        static readonly List<Vector2> SpanScratch = new();

        /// <summary>Fills scan with the furniture, walls and doors on the floor at playerFloor.y.</summary>
        public static void Collect(Vector3 playerFloor, Scan scan)
        {
            scan.Clear();
            scan.FloorY = playerFloor.y;
            Planes.Clear();
            Volumes.Clear();
            var mruk = MRUK.Instance;
            if (mruk == null) return;
            float tolerance = Tuning.Props.floorTolerance;

            foreach (var room in mruk.Rooms)
            {
                if (room == null || room.FloorAnchors.Count == 0) continue;
                float roomFloor = room.FloorAnchors[0].transform.position.y;
                if (Mathf.Abs(roomFloor - playerFloor.y) > 0.6f) continue;   // another storey

                foreach (var anchor in room.Anchors)
                {
                    if (anchor == null || anchor.HasAnyLabel(Ignored)) continue;
                    if (anchor.VolumeBounds.HasValue)
                    {
                        if (!TryFootprint(anchor, out var fp)) continue;
                        Volumes.Add(fp);   // every volume blocks walls behind it
                        if (anchor.HasAnyLabel(FurnitureLabels) && Mathf.Abs(fp.BottomY - roomFloor) <= tolerance)
                            scan.Furniture.Add(fp);
                        continue;
                    }
                    if (!anchor.PlaneRect.HasValue) continue;
                    if (anchor.HasAnyLabel(WallLabels))
                    {
                        if (TryWall(anchor, room, roomFloor, out var wall)) scan.Walls.Add(wall);
                        continue;
                    }
                    // door frames, windows, wall art, screens...: they block the wall where they are
                    var plane = PlaneOf(anchor);
                    Planes.Add(plane);
                    if (plane.IsDoor && Mathf.Abs(plane.Bottom - roomFloor) <= tolerance) AddDoor(plane, playerFloor, scan);
                }
            }

            foreach (var wall in scan.Walls)
            {
                foreach (var p in Planes)
                    AddBlocker(wall, p.C0, p.C1, p.C2, p.C3, p.Bottom, p.Top, 0.3f, p.IsDoor, p.Center);
                foreach (var v in Volumes)
                {
                    var hx = v.Right * v.HalfX;
                    var hz = v.Forward * v.HalfZ;
                    AddBlocker(wall, v.Center - hx - hz, v.Center + hx - hz, v.Center + hx + hz, v.Center - hx + hz,
                               v.BottomY, v.TopY, 0.45f, false, v.Center);
                }
            }

            // each door: the wall it sits in, on your side
            for (int i = 0; i < scan.Doors.Count; i++)
            {
                var door = scan.Doors[i];
                foreach (var wall in scan.Walls)
                {
                    var rel = door.Base - wall.Origin;
                    float s = Vector3.Dot(rel, wall.Along);
                    if (Mathf.Abs(Vector3.Dot(rel, wall.Inward)) > 0.2f || s < 0f || s > wall.Length) continue;
                    if (Vector3.Dot(wall.Inward, door.Inward) < 0.5f) continue;
                    door.Wall = wall;
                    door.S = s;
                    break;
                }
                scan.Doors[i] = door;
            }
        }

        /// <summary>
        /// The free stretches [x = from, y = to] of a wall between the heights bottom and top: away from
        /// blockers (plus margin) and from the wall's ends (corners). A door blocks its stretch of wall
        /// at every height, so nothing ever goes across or above a door opening.
        /// </summary>
        public static void FreeSpans(Wall wall, float bottom, float top, float margin, List<Vector2> into)
        {
            into.Clear();
            if (wall.Length - 2f * margin > 0.1f) into.Add(new Vector2(margin, wall.Length - margin));
            foreach (var b in wall.Blockers)
            {
                if (!b.IsDoor && (b.Top < bottom || b.Bottom > top)) continue;
                float cutFrom = b.From - margin, cutTo = b.To + margin;
                SpanScratch.Clear();
                foreach (var span in into)
                {
                    if (cutTo <= span.x || cutFrom >= span.y) { SpanScratch.Add(span); continue; }
                    if (cutFrom > span.x) SpanScratch.Add(new Vector2(span.x, cutFrom));
                    if (cutTo < span.y) SpanScratch.Add(new Vector2(cutTo, span.y));
                }
                into.Clear();
                into.AddRange(SpanScratch);
            }
        }

        /// <summary>
        /// True when nothing on the wall overlaps [from, to] between bottom and top. Other doors count at
        /// every height; the door at doorCenter (the one an elevator frame goes around) doesn't count.
        /// </summary>
        public static bool WallFree(Wall wall, float from, float to, float bottom, float top, Vector3 doorCenter)
        {
            if (from < 0f || to > wall.Length) return false;
            foreach (var b in wall.Blockers)
            {
                if (b.To <= from || b.From >= to) continue;
                if (!b.IsDoor && (b.Top < bottom || b.Bottom > top)) continue;
                if (b.IsDoor && Flat(b.Center - doorCenter).magnitude < 0.35f) continue;   // the door itself
                return false;
            }
            return true;
        }

        /// <summary>Horizontal distance from p to the segment a-b.</summary>
        public static float FlatSegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            p.y = a.y = b.y = 0f;
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>A point of floor that is inside a scanned room on your floor and outside all furniture.</summary>
        public static bool FloorPointFree(Vector3 p)
        {
            var mruk = MRUK.Instance;
            if (mruk == null) return false;
            bool inRoom = false;
            foreach (var room in mruk.Rooms)
            {
                if (room == null) continue;
                if (room.IsPositionInSceneVolume(p + Vector3.up * 0.1f, 0.05f)) return false;
                if (room.IsPositionInRoom(p + Vector3.up * 0.3f, true)) inRoom = true;   // with heights: this storey only
            }
            return inRoom;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        static bool TryFootprint(MRUKAnchor anchor, out Footprint fp)
        {
            fp = default;
            var b = anchor.VolumeBounds.Value;
            var t = anchor.transform;
            var ax = t.TransformVector(new Vector3(b.size.x, 0f, 0f));
            var ay = t.TransformVector(new Vector3(0f, b.size.y, 0f));
            var az = t.TransformVector(new Vector3(0f, 0f, b.size.z));
            // the box axis closest to vertical is its height
            float ux = Upness(ax), uy = Upness(ay), uz = Upness(az);
            Vector3 vertical, h1, h2;
            if (ux >= uy && ux >= uz) { vertical = ax; h1 = ay; h2 = az; }
            else if (uy >= uz) { vertical = ay; h1 = ax; h2 = az; }
            else { vertical = az; h1 = ax; h2 = ay; }
            if (Mathf.Max(ux, Mathf.Max(uy, uz)) < 0.8f) return false;   // a tilted box: leave it alone

            h1 = Flat(h1);
            h2 = Flat(h2);
            if (h1.magnitude < 0.1f || h2.magnitude < 0.1f) return false;
            var center = t.TransformPoint(b.center);
            float height = Mathf.Abs(vertical.y);
            fp.BottomY = center.y - height * 0.5f;
            fp.TopY = center.y + height * 0.5f;
            fp.Center = new Vector3(center.x, fp.BottomY, center.z);
            fp.HalfX = h1.magnitude * 0.5f;
            fp.HalfZ = h2.magnitude * 0.5f;
            fp.Right = h1.normalized;
            fp.Forward = h2.normalized;
            fp.Label = anchor.Label;
            return true;
        }

        static float Upness(Vector3 v) => v.sqrMagnitude > 1e-8f ? Mathf.Abs(v.normalized.y) : 0f;

        static bool TryWall(MRUKAnchor anchor, MRUKRoom room, float roomFloor, out Wall wall)
        {
            wall = null;
            var r = anchor.PlaneRect.Value;
            var t = anchor.transform;
            var c00 = t.TransformPoint(new Vector3(r.xMin, r.yMin, 0f));
            var c10 = t.TransformPoint(new Vector3(r.xMax, r.yMin, 0f));
            var c01 = t.TransformPoint(new Vector3(r.xMin, r.yMax, 0f));
            var c11 = t.TransformPoint(new Vector3(r.xMax, r.yMax, 0f));
            var ax = c10 - c00;
            var ay = c01 - c00;
            // the more horizontal edge runs along the wall
            var along = Mathf.Abs(ax.y) <= Mathf.Abs(ay.y) ? ax : ay;
            along.y = 0f;
            float length = along.magnitude;
            if (length < 0.6f) return false;
            along /= length;
            float bottom = Mathf.Min(Mathf.Min(c00.y, c10.y), Mathf.Min(c01.y, c11.y));
            float top = Mathf.Max(Mathf.Max(c00.y, c10.y), Mathf.Max(c01.y, c11.y));
            if (Mathf.Abs(bottom - roomFloor) > Tuning.Props.floorTolerance + 0.1f || top - bottom < 1.5f) return false;

            var origin = new Vector3(c00.x, bottom, c00.z);
            // into the room: the side where a point 0.3 m off the wall is inside this room
            var inward = Vector3.Cross(Vector3.up, along).normalized;
            var mid = origin + along * (length * 0.5f) + Vector3.up * 1f;
            if (!room.IsPositionInRoom(mid + inward * 0.3f, false))
            {
                inward = -inward;
                if (!room.IsPositionInRoom(mid + inward * 0.3f, false)) return false;
            }
            wall = new Wall { Origin = origin, Along = along, Inward = inward, Length = length, BottomY = bottom, TopY = top };
            return true;
        }

        static PlaneInfo PlaneOf(MRUKAnchor anchor)
        {
            var r = anchor.PlaneRect.Value;
            var t = anchor.transform;
            var p = new PlaneInfo
            {
                C0 = t.TransformPoint(new Vector3(r.xMin, r.yMin, 0f)),
                C1 = t.TransformPoint(new Vector3(r.xMax, r.yMin, 0f)),
                C2 = t.TransformPoint(new Vector3(r.xMax, r.yMax, 0f)),
                C3 = t.TransformPoint(new Vector3(r.xMin, r.yMax, 0f)),
                Center = t.TransformPoint(new Vector3(r.center.x, r.center.y, 0f)),
                IsDoor = anchor.HasAnyLabel(MRUKAnchor.SceneLabels.DOOR_FRAME),
            };
            p.Bottom = Mathf.Min(Mathf.Min(p.C0.y, p.C1.y), Mathf.Min(p.C2.y, p.C3.y));
            p.Top = Mathf.Max(Mathf.Max(p.C0.y, p.C1.y), Mathf.Max(p.C2.y, p.C3.y));
            return p;
        }

        static void AddDoor(PlaneInfo p, Vector3 playerFloor, Scan scan)
        {
            // the door's horizontal edge (C0-C1 or C0-C3)
            var e1 = p.C1 - p.C0;
            var e2 = p.C3 - p.C0;
            var along = Mathf.Abs(e1.y) <= Mathf.Abs(e2.y) ? e1 : e2;
            along.y = 0f;
            float width = along.magnitude;
            if (width < 0.4f) return;
            along /= width;
            var bottomCenter = new Vector3(p.Center.x, p.Bottom, p.Center.z);
            // the same door scanned from both rooms: keep one
            foreach (var d in scan.Doors)
                if (Flat(d.Base - bottomCenter).magnitude < 0.3f) return;
            var inward = Vector3.Cross(Vector3.up, along).normalized;
            if (Vector3.Dot(inward, playerFloor - bottomCenter) < 0f) inward = -inward;
            scan.Doors.Add(new Door
            {
                Base = bottomCenter,
                Along = along,
                Inward = inward,
                HalfWidth = width * 0.5f,
                Height = p.Top - p.Bottom,
            });
        }

        /// <summary>Projects an object's corners onto the wall; it blocks the wall if it is on it or close in front.</summary>
        static void AddBlocker(Wall w, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float bottom, float top, float maxFront, bool isDoor, Vector3 center)
        {
            float from = float.MaxValue, to = float.MinValue, near = float.MaxValue, far = float.MinValue;
            Project(w, a, ref from, ref to, ref near, ref far);
            Project(w, b, ref from, ref to, ref near, ref far);
            Project(w, c, ref from, ref to, ref near, ref far);
            Project(w, d, ref from, ref to, ref near, ref far);
            if (near > maxFront || far < -0.3f) return;          // not at this wall
            if (to < 0f || from > w.Length) return;             // beside it
            if (top < w.BottomY - 0.1f || bottom > w.TopY + 0.1f) return;
            w.Blockers.Add(new Blocker { From = from, To = to, Bottom = bottom, Top = top, IsDoor = isDoor, Center = center });
        }

        static void Project(Wall w, Vector3 p, ref float from, ref float to, ref float near, ref float far)
        {
            var rel = p - w.Origin;
            float s = Vector3.Dot(rel, w.Along);
            float n = Vector3.Dot(rel, w.Inward);
            from = Mathf.Min(from, s);
            to = Mathf.Max(to, s);
            near = Mathf.Min(near, n);
            far = Mathf.Max(far, n);
        }
    }
}
