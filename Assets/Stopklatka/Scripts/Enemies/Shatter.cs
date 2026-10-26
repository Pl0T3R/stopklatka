using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// How an enemy breaks (STYLE_GUIDE 8). Nothing is cut at runtime: the body's own part meshes are
    /// copied into pooled debris, glowing caps are placed where pieces broke off, and pooled shards
    /// burst out. Numbers in GameConfig shatter.
    ///   - Break: parts near the hit turn into shards, 30-60 small shards burst from the hit, the
    ///     other parts fall as big chunks with a yellow-orange cap at their break.
    ///   - Cut (katana): each part goes to one side of the cut plane by its center; the two halves
    ///     get a flat glowing cross-section at the plane, fly apart and burst into shards.
    /// </summary>
    public static class Shatter
    {
        const int MaxParts = 16;

        /// <summary>
        /// Builds every pool and glow mesh the death effects use, so the first kill doesn't create
        /// dozens of GameObjects in one frame. Call while loading.
        /// </summary>
        public static void Prewarm()
        {
            ShardSystem.Prewarm();
            DebrisSystem.Prewarm();
            Cracks.Prewarm();
            Effects.Prewarm();
            _ = GlowKit.Cap;
            _ = GlowKit.Crack;
        }
        static readonly bool[] Pulverized = new bool[MaxParts];
        static readonly int[] Side = new int[MaxParts];
        static readonly Vector3[] Corners = new Vector3[8];

        /// <summary>Breaks the body into chunks and shards from a hit at point along dir.</summary>
        public static void Break(EnemyBody body, Vector3 point, Vector3 dir, float floorY)
        {
            using var perfScope = PerfMarkers.Shatter.Auto();
            var cfg = Tuning.Shatter;
            dir = SafeDir(dir, body.transform.forward);
            var pieces = body.Pieces;
            int count = Mathf.Min(pieces.Length, MaxParts);

            // the part that was hit, and any part very close to the hit, turn to dust
            int nearest = body.NearestPart(point, out _);
            int pulverized = 0;
            for (int i = 0; i < count; i++)
            {
                Pulverized[i] = i == nearest || body.DistanceToPart(i, point) < cfg.pulverizeRadius;
                if (Pulverized[i]) pulverized++;
            }

            int total = Random.Range(Mathf.Max(0, cfg.shardsMin), Mathf.Max(cfg.shardsMin, cfg.shardsMax) + 1);
            int fromHit = Mathf.RoundToInt(total * 0.55f);
            int perPart = pulverized > 0 ? Mathf.Max(1, (total - fromHit) / pulverized) : 0;

            // 1. a spray from the hit point along the hit: smaller and faster
            for (int k = 0; k < fromHit; k++)
            {
                var pos = point + Random.insideUnitSphere * 0.05f;
                var v = dir * (cfg.shardSpeed * Random.Range(0.5f, 1.3f)) + Random.insideUnitSphere * cfg.shardSpread + Vector3.up * 0.4f;
                Emit(cfg, pos, v, Random.Range(cfg.shardSizeMin, Mathf.Lerp(cfg.shardSizeMin, cfg.shardSizeMax, 0.6f)));
            }

            // 2. the pulverized parts fill their volume with shards
            for (int i = 0; i < count; i++)
            {
                if (!Pulverized[i]) continue;
                var b = pieces[i].Renderer.bounds;
                for (int k = 0; k < perPart; k++)
                {
                    var pos = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y), Random.Range(b.min.z, b.max.z));
                    var v = dir * Random.Range(0.4f, 1.6f) + (pos - b.center).normalized * Random.Range(0.3f, 1.2f) + Vector3.up * 0.4f;
                    Emit(cfg, pos, v, Random.Range(cfg.shardSizeMin, cfg.shardSizeMax));
                }
            }

            // 3. the rest break off as big chunks with a glowing cap where they broke
            var red = Mats.Get(Mats.Enemy);
            var glow = Mats.Get(Mats.Glow);
            var center = body.transform.position + Vector3.up;
            var tumble = Vector3.Cross(Vector3.up, dir);
            for (int i = 0; i < count; i++)
            {
                if (Pulverized[i]) continue;
                var part = pieces[i];
                var t = part.Renderer.transform;
                var db = DebrisSystem.Begin(t.position, t.rotation, 2);
                DebrisSystem.AddPiece(db, part.Mesh, red, t.position, t.rotation, Vector3.one, false);
                AddPartCap(db, t, part, glow);

                var partCenter = part.Renderer.bounds.center;
                float near = 1f / (1f + (partCenter - point).sqrMagnitude * 4f);
                var v = dir * (cfg.chunkPush * Random.Range(0.4f, 1f) * (0.3f + near))
                        + (partCenter - center).normalized * 0.35f + Vector3.up * Random.Range(0.1f, 0.6f);
                var w = Random.insideUnitSphere * Random.Range(1.5f, 4.5f) + tumble * (2f * near);
                db.Launch(v, w, floorY, cfg.chunkLifetime * Random.Range(0.9f, 1.1f), settleFlat: true, burstAtEnd: false, burstShards: 0);
            }
        }

        /// <summary>
        /// Cuts the body in half along a plane (through point, with the given normal). Returns false if
        /// the plane misses the body (every part on one side); then the caller shatters it instead.
        /// </summary>
        public static bool Cut(EnemyBody body, Vector3 point, Vector3 normal, Vector3 swingDir, float floorY)
        {
            using var perfScope = PerfMarkers.Shatter.Auto();
            var cfg = Tuning.Shatter;
            var pieces = body.Pieces;
            int count = Mathf.Min(pieces.Length, MaxParts);
            if (normal.sqrMagnitude < 1e-6f) return false;
            var n = normal.normalized;
            if (n.y < 0f) n = -n;   // +1 is the upper side
            swingDir = SafeDir(swingDir, body.transform.forward);

            int upper = 0;
            for (int i = 0; i < count; i++)
            {
                Side[i] = Vector3.Dot(pieces[i].Renderer.bounds.center - point, n) >= 0f ? 1 : -1;
                if (Side[i] > 0) upper++;
            }
            if (upper == 0 || upper == count) return false;

            var red = Mats.Get(Mats.Enemy);
            var glow = Mats.Get(Mats.Glow);
            for (int side = 1; side >= -1; side -= 2)
            {
                // the half's own root at its centroid, so it tumbles about its middle
                var centroid = Vector3.zero;
                int parts = 0;
                for (int i = 0; i < count; i++)
                    if (Side[i] == side) { centroid += pieces[i].Renderer.bounds.center; parts++; }
                centroid /= parts;

                var db = DebrisSystem.Begin(centroid, Quaternion.identity, parts + 1);
                float reach = float.MaxValue;   // how close this half's parts come to the plane (negative: they cross it)
                for (int i = 0; i < count; i++)
                {
                    if (Side[i] != side) continue;
                    var t = pieces[i].Renderer.transform;
                    DebrisSystem.AddPiece(db, pieces[i].Mesh, red, t.position, t.rotation, Vector3.one, false);
                    reach = Mathf.Min(reach, NearestToPlane(t, pieces[i].Mesh.bounds, point, n, side));
                }
                // the flat glowing cross-section sits where this half's parts end, on a plane parallel to the cut
                AddCutCap(db, body.transform, point + n * (side * reach), n, side, glow);

                bool top = side > 0;
                var v = n * (side * cfg.cutSeparationSpeed * (top ? 1f : 0.5f))
                        + swingDir * (cfg.cutSwingSpeed * (top ? 1f : 0.2f)) + Vector3.up * (top ? 0.3f : 0f);
                var w = Vector3.Cross(Vector3.up, swingDir) * (top ? 2.2f : 0.8f) + Random.insideUnitSphere * (top ? 0.6f : 0.3f);
                db.Launch(v, w, floorY, cfg.cutHalfSeconds * Random.Range(0.95f, 1.1f), settleFlat: false, burstAtEnd: true, burstShards: cfg.cutShardsPerHalf);
            }

            // a fan of small shards from the cut line, thrown along the swing
            var along = Vector3.Cross(n, Vector3.ProjectOnPlane(swingDir, n).sqrMagnitude > 1e-4f ? Vector3.ProjectOnPlane(swingDir, n).normalized : body.transform.right);
            for (int k = 0; k < cfg.cutSprayShards; k++)
            {
                var pos = point + along * Random.Range(-0.22f, 0.22f) + Random.insideUnitSphere * 0.03f;
                var v = swingDir * Random.Range(1f, 3f) + n * Random.Range(-0.6f, 0.6f) + Random.insideUnitSphere * 0.5f;
                Emit(cfg, pos, v, Random.Range(cfg.shardSizeMin, Mathf.Lerp(cfg.shardSizeMin, cfg.shardSizeMax, 0.5f)));
            }
            return true;
        }

        /// <summary>A small red, dark red or glowing shard.</summary>
        static void Emit(ShatterSettings cfg, Vector3 pos, Vector3 velocity, float size)
        {
            var mat = Random.value < cfg.glowShardShare ? Mats.Get(Mats.GlowShard)
                : Random.value < cfg.darkShardShare ? Mats.Get(Mats.EnemyDark) : Mats.Get(Mats.Enemy);
            ShardSystem.Emit(mat, pos, velocity, size, ShardSystem.RandomLifetime());
        }

        /// <summary>The glowing cap of a big chunk, at the joint where it broke off.</summary>
        static void AddPartCap(DebrisBody db, Transform t, BodyPart part, Material glow)
        {
            // the cap mesh faces +Y; flip it for parts that broke at their lower end
            var rot = t.rotation * (part.CapNormal.y < 0f ? Quaternion.Euler(180f, 0f, 0f) : Quaternion.identity);
            var pos = t.TransformPoint(part.CapPosition + part.CapNormal * 0.003f);
            var scale = new Vector3(part.CapRadius.x * 1.04f, 1f, part.CapRadius.y * 1.04f);
            DebrisSystem.AddPiece(db, GlowKit.Cap, glow, pos, rot, scale, true);
        }

        /// <summary>
        /// The cross-section of the body where a cut half ends: centered on the body's axis, sized from
        /// <see cref="EnemyBody.CrossSection"/> and stretched along the slope of a slanted cut.
        /// A nearly vertical cut gets a tall face down the body.
        /// </summary>
        static void AddCutCap(DebrisBody db, Transform root, Vector3 planePoint, Vector3 n, int side, Material glow)
        {
            var up = Vector3.up;
            var axisBase = root.position;
            float ny = n.y;
            float height;
            Vector3 center;
            if (ny > 0.3f)
            {
                height = Mathf.Clamp(Vector3.Dot(planePoint - axisBase, n) / ny, 0.1f, 1.75f);
                center = axisBase + up * height;
            }
            else
            {
                height = 0.95f;
                center = axisBase + up * height;
                center -= n * Vector3.Dot(center - planePoint, n);
            }
            var cs = EnemyBody.CrossSection(height);

            // in-plane axes: a runs down the slope of the cut, b across it
            var a = Vector3.ProjectOnPlane(up, n);
            if (a.sqrMagnitude < 1e-4f) a = Vector3.ProjectOnPlane(root.forward, n);
            a.Normalize();
            var b = Vector3.Cross(n, a);
            var right = root.right;
            var fwd = root.forward;
            float extB = Mathf.Abs(Vector3.Dot(b, right)) * cs.x + Mathf.Abs(Vector3.Dot(b, fwd)) * cs.y;
            var aFlat = Vector3.ProjectOnPlane(a, up);
            aFlat = aFlat.sqrMagnitude > 1e-4f ? aFlat.normalized : fwd;
            float extA = ny > 0.3f
                ? (Mathf.Abs(Vector3.Dot(aFlat, right)) * cs.x + Mathf.Abs(Vector3.Dot(aFlat, fwd)) * cs.y) / Mathf.Max(ny, 0.35f)
                : 1.5f;
            extA = Mathf.Min(extA, 1.6f);

            var pos = center - n * (side * 0.004f);   // just outside the half, so it never hides in its surface
            var rot = Quaternion.LookRotation(a, n);   // mesh +Y = plane normal, mesh +Z = down the slope
            DebrisSystem.AddPiece(db, GlowKit.Cap, glow, pos, rot, new Vector3(extB * 0.5f, 1f, extA * 0.5f), true);
        }

        /// <summary>Signed distance from the plane (toward this side) of the part's box corner nearest to it.</summary>
        static float NearestToPlane(Transform t, Bounds b, Vector3 point, Vector3 n, int side)
        {
            var min = b.min;
            var max = b.max;
            Corners[0] = new Vector3(min.x, min.y, min.z);
            Corners[1] = new Vector3(max.x, min.y, min.z);
            Corners[2] = new Vector3(min.x, max.y, min.z);
            Corners[3] = new Vector3(max.x, max.y, min.z);
            Corners[4] = new Vector3(min.x, min.y, max.z);
            Corners[5] = new Vector3(max.x, min.y, max.z);
            Corners[6] = new Vector3(min.x, max.y, max.z);
            Corners[7] = new Vector3(max.x, max.y, max.z);
            float best = float.MaxValue;
            for (int i = 0; i < 8; i++)
                best = Mathf.Min(best, side * Vector3.Dot(t.TransformPoint(Corners[i]) - point, n));
            return best;
        }

        static Vector3 SafeDir(Vector3 dir, Vector3 fallback) =>
            dir.sqrMagnitude > 1e-6f ? dir.normalized : fallback;
    }
}
