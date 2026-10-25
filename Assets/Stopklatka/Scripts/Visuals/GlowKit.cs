using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// Meshes for the yellow-orange glow where an enemy breaks (STYLE_GUIDE 8): the flat cap on a
    /// break or a katana cut, a crack strip, and the kill flash. They all use the one
    /// <see cref="Mats.Glow"/> material (white, unlit) and carry their colors as vertex colors:
    /// #FFD84D in the middle, #FF9A1F at the edge. Vertex colors are not gamma-corrected by Unity,
    /// so the palette goes in as linear values.
    /// </summary>
    public static class GlowKit
    {
        /// <summary>#FFD84D: the bright core (linear, for vertex colors).</summary>
        public static readonly Color Core = new Color(1f, 0.847f, 0.302f).linear;
        /// <summary>#FF9A1F: the orange edge (linear, for vertex colors).</summary>
        public static readonly Color Edge = new Color(1f, 0.604f, 0.122f).linear;

        static Mesh _cap, _crack, _flash;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _cap = null; _crack = null; _flash = null; }

        /// <summary>
        /// Flat faceted disc of radius 1 in the XZ plane, facing +Y (drawn from both sides).
        /// Scale x/z to the cross-section it covers. Core color in the middle, edge color at the rim.
        /// </summary>
        public static Mesh Cap
        {
            get
            {
                if (_cap != null) return _cap;
                var rng = new System.Random(404);
                var b = new MeshKit.Builder();
                const int sides = 7;
                var rim = new Vector3[sides];
                var mid = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float ang = (i + (float)rng.NextDouble() * 0.35f) * Mathf.PI * 2f / sides;
                    float r = 0.9f + (float)rng.NextDouble() * 0.1f;
                    rim[i] = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                    mid[i] = rim[i] * 0.55f;
                }
                // a hair of height in the middle so the facets catch nothing but still read as a crystal face
                var center = Vector3.zero;
                var below = Vector3.down;
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    b.Tri(center, mid[i], mid[j], below);
                    b.Quad(mid[i], rim[i], rim[j], mid[j], below);
                }
                _cap = b.ToMesh("Glow Cap", p =>
                {
                    float r = new Vector2(p.x, p.z).magnitude;
                    return Color.Lerp(Core, Edge, Mathf.SmoothStep(0.35f, 0.95f, r));
                });
                return _cap;
            }
        }

        /// <summary>
        /// A crack: a thin jagged strip in the XZ plane (facing +Y) from the origin along +X, length 1,
        /// width 1. Scale x to the crack length and z to its width. Bright at the start, orange at the tip.
        /// </summary>
        public static Mesh Crack
        {
            get
            {
                if (_crack != null) return _crack;
                var b = new MeshKit.Builder();
                // three segments with a kink, narrowing to a point
                var spine = new[] { new Vector3(0f, 0f, 0f), new Vector3(0.38f, 0f, 0.12f), new Vector3(0.7f, 0f, -0.1f), new Vector3(1f, 0f, 0.05f) };
                var width = new[] { 0.5f, 0.38f, 0.22f, 0f };
                var below = Vector3.down;
                for (int i = 0; i < spine.Length - 1; i++)
                {
                    var a0 = spine[i] + Vector3.forward * width[i];
                    var a1 = spine[i] - Vector3.forward * width[i];
                    var b0 = spine[i + 1] + Vector3.forward * width[i + 1];
                    var b1 = spine[i + 1] - Vector3.forward * width[i + 1];
                    b.Tri(a0, a1, b1, spine[i] + below);
                    b.Tri(a0, b1, b0, spine[i] + below);
                }
                _crack = b.ToMesh("Glow Crack", p => Color.Lerp(Core, Edge, Mathf.Clamp01(p.x * 1.2f)));
                return _crack;
            }
        }

        /// <summary>Elongated faceted blob for the kill flash: yellow core fading out at its outer facets.</summary>
        public static Mesh Flash
        {
            get
            {
                if (_flash != null) return _flash;
                var b = new MeshKit.Builder();
                b.Ico(Vector3.zero, new Vector3(0.5f, 0.5f, 0.9f), 0.25f, new System.Random(6));
                _flash = b.ToMesh("Glow Flash", p =>
                {
                    var c = Color.Lerp(Core, Edge, Mathf.Clamp01(p.magnitude * 1.4f - 0.3f));
                    c.a = 0.95f;
                    return c;
                });
                return _flash;
            }
        }
    }
}
