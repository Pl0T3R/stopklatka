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
        public const string Glass = "GlassWhite";               // white crystals: menu toggles, gauge tick, sparks
        public const string DarkGlass = "GlassDark";            // the bottle and its shards
        public const string Statue = "StatueWhite";             // level props (stage 6): statues
        public const string PropWhite = "PropWhite";            // level props (stage 6): plinths, cars, counters
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

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        static Material CreateFallback(string name)
        {
            bool transparent = name is EnemyTrail or PlayerTrail or Flash or RedFlash or FloorShadow or RingFx or Hidden or Glow;
            bool text = name == Text;
            var shader = Shader.Find(text ? "Stopklatka/Text" : transparent ? "Stopklatka/Transparent" : "Stopklatka/Crystal");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader) { name = name, enableInstancing = true };
            // the same STYLE_GUIDE colors as StopklatkaBuilder.CreateMaterials, without the finer settings
            var color = name switch
            {
                Enemy or EnemyReveal or Target or RingFx => Hex("#FF2B1C"),
                EnemyDark => Hex("#9E0A08"),
                Weapon => Hex("#1E3242"),
                Blade => Hex("#1A2630"),
                DarkGlass => Hex("#22384A"),
                Glass or PropWhite => Hex("#F2F4F5"),
                Statue => Hex("#ECEFF0"),
                EnemyTrail => Hex("#FF1E1E"),
                PlayerTrail => Hex("#0B141A"),
                Flash => Color.white,
                RedFlash => new Color(0.9f, 0.02f, 0.02f),
                FloorShadow => Hex("#5E6E78"),
                Hidden => Color.clear,
                Glow => Color.white,                              // the meshes carry #FFD84D..#FF9A1F as vertex colors
                GlowShard => Hex("#FFD84D"),
                _ => Color.white,
            };
            color.a = name switch
            {
                EnemyTrail => 0.8f, PlayerTrail => 0.6f, Flash or RingFx => 0.85f, RedFlash => 0.6f,
                FloorShadow => 0.38f, Hidden => 0f, _ => 1f,
            };
            m.SetColor("_BaseColor", color);
            if (!transparent && !text) m.SetColor("_ShadowColor", name == GlowShard ? Hex("#FF9A1F") : color * 0.5f);
            if (name is Enemy or EnemyDark or EnemyReveal)
            {
                m.SetFloat("_HatchStrength", 0.25f);
                m.SetColor("_HatchColor", Hex("#B80E0A"));
            }
            if (name == GlowShard) m.SetFloat("_Emission", 0.45f);
            if (name is FloorShadow or RingFx) m.SetFloat("_OcclusionBias", 0.08f);
            if (name == EnemyReveal) m.EnableKeyword("_REVEAL");
            return m;
        }
    }
}
