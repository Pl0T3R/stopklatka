using System.Collections.Generic;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// Materials created by the builder in Resources/Materials (so their shaders ship in the build).
    /// Falls back to building them from the shader if an asset is missing.
    /// </summary>
    public static class Mats
    {
        public const string Enemy = "EnemyCrystal";
        public const string EnemyDark = "EnemyCrystalDark";     // second red for shards
        public const string EnemyReveal = "EnemyReveal";        // spawn-in variant (_REVEAL keyword)
        public const string Weapon = "WeaponBlack";
        public const string Blade = "KatanaBlade";
        public const string Glass = "GlassWhite";
        public const string Target = "StartTarget";
        public const string EnemyTrail = "EnemyTrail";
        public const string PlayerTrail = "PlayerTrail";
        public const string Flash = "MuzzleFlash";
        public const string RedFlash = "RedFlash";              // hit vignette
        public const string FloorShadow = "FloorShadow";
        public const string RingFx = "SpawnRing";
        public const string Text = "HudText";
        public const string Hidden = "Hidden";
        public const string Glow = "FractureGlow";             // fracture caps, cut faces, cracks, kill flash (vertex colors)
        public const string GlowShard = "ShardGlow";           // glowing yellow-orange shards (broken crystal core)

        static readonly Dictionary<string, Material> Cache = new();

        public static Material Get(string name)
        {
            if (Cache.TryGetValue(name, out var m) && m != null) return m;
            m = Resources.Load<Material>($"Materials/{name}");
            if (m == null)
            {
                Debug.LogWarning($"[Stopklatka] Material {name} missing, creating a fallback. Run Stopklatka > Build Game.");
                m = CreateFallback(name);
            }
            Cache[name] = m;
            return m;
        }

        static Material CreateFallback(string name)
        {
            bool transparent = name is EnemyTrail or PlayerTrail or Flash or RedFlash or FloorShadow or RingFx or Hidden or Glow;
            bool text = name == Text;
            var shader = Shader.Find(text ? "Stopklatka/Text" : transparent ? "Stopklatka/Transparent" : "Stopklatka/Crystal");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader) { name = name, enableInstancing = true };
            var color = name switch
            {
                Enemy or EnemyReveal => new Color(0.92f, 0.13f, 0.09f),
                EnemyDark => new Color(0.62f, 0.05f, 0.05f),
                Weapon => new Color(0.1f, 0.1f, 0.11f),
                Blade => new Color(0.85f, 0.87f, 0.9f),
                Glass => new Color(0.9f, 0.95f, 0.97f),
                Target => new Color(0.98f, 0.16f, 0.1f),
                EnemyTrail => new Color(1f, 0.12f, 0.08f, 0.75f),
                PlayerTrail => new Color(0.08f, 0.08f, 0.1f, 0.6f),
                Flash => new Color(1f, 0.92f, 0.65f, 0.9f),
                RedFlash => new Color(0.9f, 0.02f, 0.02f, 0.6f),
                FloorShadow => new Color(0.04f, 0.04f, 0.06f, 0.42f),
                RingFx => new Color(1f, 0.12f, 0.08f, 0.85f),
                Hidden => new Color(0f, 0f, 0f, 0f),
                Glow => Color.white,                              // the meshes carry #FFD84D..#FF9A1F as vertex colors
                GlowShard => new Color(1f, 0.847f, 0.302f),       // #FFD84D
                _ => Color.white,
            };
            m.SetColor("_BaseColor", color);
            if (!transparent && !text) m.SetColor("_ShadowColor", name == GlowShard ? new Color(1f, 0.604f, 0.122f) : color * 0.5f);
            if (name == GlowShard) m.SetFloat("_Emission", 0.45f);
            if (name is FloorShadow or RingFx) m.SetFloat("_OcclusionBias", 0.08f);
            if (name == EnemyReveal) m.EnableKeyword("_REVEAL");
            return m;
        }
    }
}
