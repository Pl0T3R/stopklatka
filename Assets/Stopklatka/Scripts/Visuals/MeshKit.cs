using System.Collections.Generic;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// Builds low-poly, flat-shaded ("faceted crystal") meshes in code. Every triangle gets its own
    /// vertices so each facet catches light separately, which gives the Superhot look.
    /// Faces are always turned outward from the shape's center, so shapes must be roughly convex.
    /// </summary>
    public static class MeshKit
    {
        public class Builder
        {
            readonly List<Vector3> _v = new();
            readonly List<Vector3> _n = new();
            readonly List<int> _t = new();

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 shapeCenter)
            {
                var n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) return;
                if (Vector3.Dot(n, (a + b + c) / 3f - shapeCenter) < 0f) { (b, c) = (c, b); n = -n; }
                n.Normalize();
                int i = _v.Count;
                _v.Add(a); _v.Add(b); _v.Add(c);
                _n.Add(n); _n.Add(n); _n.Add(n);
                _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 center)
            {
                Tri(a, b, c, center);
                Tri(a, c, d, center);
            }

            /// <summary>Box with an optional narrower/wider top (taper) and random corner jitter.</summary>
            public void Box(Vector3 center, Vector3 size, Quaternion rotation, float topTaper = 1f, float jitter = 0f, System.Random rng = null)
            {
                var h = size * 0.5f;
                var c = new Vector3[8];
                for (int i = 0; i < 8; i++)
                {
                    float sx = (i & 1) == 0 ? -1f : 1f, sy = (i & 2) == 0 ? -1f : 1f, sz = (i & 4) == 0 ? -1f : 1f;
                    float k = sy > 0f ? topTaper : 1f;
                    var p = new Vector3(sx * h.x * k, sy * h.y, sz * h.z * k);
                    if (jitter > 0f && rng != null) p += Jitter(rng, jitter);
                    c[i] = center + rotation * p;
                }
                Quad(c[0], c[1], c[3], c[2], center); // -z
                Quad(c[4], c[5], c[7], c[6], center); // +z
                Quad(c[0], c[2], c[6], c[4], center); // -x
                Quad(c[1], c[3], c[7], c[5], center); // +x
                Quad(c[0], c[1], c[5], c[4], center); // -y
                Quad(c[2], c[3], c[7], c[6], center); // +y
            }

            /// <summary>Frustum between two points with N sides (a limb, a barrel, a bottle).</summary>
            public void Prism(Vector3 from, Vector3 to, float radiusFrom, float radiusTo, int sides, float jitter = 0f, System.Random rng = null, bool caps = true)
            {
                var axis = to - from;
                if (axis.sqrMagnitude < 1e-10f) return;
                var rot = Quaternion.FromToRotation(Vector3.up, axis.normalized);
                var center = (from + to) * 0.5f;
                var a = new Vector3[sides];
                var b = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float ang = i * Mathf.PI * 2f / sides;
                    var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    a[i] = from + rot * (dir * radiusFrom);
                    b[i] = to + rot * (dir * radiusTo);
                    if (jitter > 0f && rng != null) { a[i] += Jitter(rng, jitter); b[i] += Jitter(rng, jitter); }
                }
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    Quad(a[i], a[j], b[j], b[i], center);
                    if (caps)
                    {
                        Tri(from, a[i], a[j], center);
                        Tri(to, b[j], b[i], center);
                    }
                }
            }

            /// <summary>Icosahedron (20 facets) with per-axis radii: heads, shards, gems.</summary>
            public void Ico(Vector3 center, Vector3 radii, float jitter = 0f, System.Random rng = null)
            {
                float t = (1f + Mathf.Sqrt(5f)) / 2f;
                var p = new[]
                {
                    new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                    new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                    new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
                };
                for (int i = 0; i < p.Length; i++)
                {
                    var q = p[i].normalized;
                    if (jitter > 0f && rng != null) q += Jitter(rng, jitter);
                    p[i] = center + Vector3.Scale(q, radii);
                }
                int[] f =
                {
                    0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                    3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
                };
                for (int i = 0; i < f.Length; i += 3) Tri(p[f[i]], p[f[i + 1]], p[f[i + 2]], center);
            }

            /// <summary>Double pyramid (octahedron), used for the start target.</summary>
            public void Octahedron(Vector3 center, float radius, float height)
            {
                var top = center + Vector3.up * height;
                var bottom = center - Vector3.up * height;
                var ring = new Vector3[4];
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI * 0.5f;
                    ring[i] = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                }
                for (int i = 0; i < 4; i++)
                {
                    int j = (i + 1) % 4;
                    Tri(top, ring[i], ring[j], center);
                    Tri(bottom, ring[j], ring[i], center);
                }
            }

            /// <summary>Flat ring (or disc when inner is 0) in the XZ plane of rotation, for floor marks and the hit vignette.</summary>
            public void Ring(Vector3 center, Quaternion rotation, float inner, float outer, int segments)
            {
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                    var d0 = rotation * new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                    var d1 = rotation * new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    if (inner <= 0f) Tri(center, center + d0 * outer, center + d1 * outer, center);
                    else Quad(center + d0 * inner, center + d0 * outer, center + d1 * outer, center + d1 * inner, center);
                }
            }

            /// <param name="color">Vertex color from the vertex position (used by the transparent shader
            /// for fades along streaks and toward the edges of shadows). White when omitted.</param>
            public Mesh ToMesh(string name, System.Func<Vector3, Color> color = null)
            {
                var m = new Mesh { name = name };
                if (_v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                var colors = new List<Color>(_v.Count);
                foreach (var v in _v) colors.Add(color != null ? color(v) : Color.white);
                m.SetVertices(_v);
                m.SetNormals(_n);
                m.SetColors(colors);
                m.SetTriangles(_t, 0);
                m.RecalculateBounds();
                m.UploadMeshData(true);
                return m;
            }
        }

        static Vector3 Jitter(System.Random rng, float amount) =>
            new Vector3((float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1)) * amount;

        public static GameObject Part(string name, Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go;
        }
    }
}
