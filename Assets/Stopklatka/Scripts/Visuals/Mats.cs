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
        public const string Weapon = "WeaponBlack";
        public const string Blade = "KatanaBlade";
        public const string Glass = "GlassWhite";
        public const string Target = "StartTarget";
        public const string EnemyTrail = "EnemyTrail";
        public const string PlayerTrail = "PlayerTrail";
        public const string Flash = "MuzzleFlash";
        public const string RedFlash = "RedFlash";
        public const string Text = "HudText";
        public const string Hidden = "Hidden";

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
            bool transparent = name == EnemyTrail || name == PlayerTrail || name == Flash || name == RedFlash || name == Hidden;
            bool text = name == Text;
            var shader = Shader.Find(text ? "Stopklatka/Text" : transparent ? "Stopklatka/Transparent" : "Stopklatka/Crystal");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader) { name = name, enableInstancing = true };
            m.SetColor("_BaseColor", name switch
            {
                Enemy => new Color(0.86f, 0.1f, 0.08f),
                Weapon => new Color(0.07f, 0.07f, 0.08f),
                Blade => new Color(0.78f, 0.8f, 0.84f),
                Glass => new Color(0.86f, 0.93f, 0.96f),
                Target => new Color(0.95f, 0.15f, 0.1f),
                EnemyTrail => new Color(1f, 0.15f, 0.1f, 0.55f),
                PlayerTrail => new Color(0.1f, 0.1f, 0.12f, 0.5f),
                Flash => new Color(1f, 0.9f, 0.6f, 0.9f),
                RedFlash => new Color(1f, 0f, 0f, 0.5f),
                Hidden => new Color(0f, 0f, 0f, 0f),
                _ => Color.white,
            });
            return m;
        }
    }
}
