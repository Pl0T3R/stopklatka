using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// How the real world looks through the cameras:
    ///   NORMALNY - untouched,
    ///   BIAŁY    - washed-out and bright (default),
    ///   SUPERHOT - brightness mapped onto the white palette of STYLE_GUIDE.md (cool greys to white),
    ///              so only the enemies stay red and your weapons black.
    /// Remembered between sessions. Always plain while paused or outside the scan, for safety.
    /// Also asks the system to hide the boundary while passthrough is on, so you can walk between rooms.
    /// </summary>
    public class PassthroughStyle : MonoBehaviour
    {
        public enum Style { Normal, White, Superhot }

        public static PassthroughStyle Instance { get; private set; }

        public Style Current { get; private set; } = Style.White;

        public string Label => Current switch
        {
            Style.Normal => "NORMALNY",
            Style.Superhot => "SUPERHOT",
            _ => "BIAŁY",
        };

        const string Key = "stopklatka.style";

        OVRPassthroughLayer _layer;
        Gradient _superhotGradient;
        bool _safety;

        void Awake()
        {
            Instance = this;
            Current = (Style)Mathf.Clamp(PlayerPrefs.GetInt(Key, (int)Style.White), 0, 2);
            // STYLE_GUIDE 0 and 3: camera brightness mapped onto the white palette, cool and bright.
            // The darkest key stays darker than the guide's shadow (#B9C6CC) on purpose: edges of real
            // furniture, steps and doors must stay easy to see (safety).
            _superhotGradient = new Gradient();
            _superhotGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Hex("#7E8E96"), 0f),      // darkest: cool grey, still readable
                    new GradientColorKey(Hex("#B9C6CC"), 0.25f),   // soft shadow
                    new GradientColorKey(Hex("#E6EBED"), 0.55f),   // walls
                    new GradientColorKey(Hex("#F2F4F5"), 0.8f),    // lit floor
                    new GradientColorKey(Color.white, 1f),         // windows, light
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        }

        // the passthrough color map takes display (sRGB) colors, the same as the hex values in the guide
        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.gray;

        void Start()
        {
            _layer = FindFirstObjectByType<OVRPassthroughLayer>();
            if (_layer == null) Debug.LogError("[Stopklatka] No OVRPassthroughLayer in the scene.");
            Apply();
        }

        void Update()
        {
            var manager = OVRManager.instance;
            if (manager != null && _layer != null)
                manager.shouldBoundaryVisibilityBeSuppressed = _layer.enabled;
        }

        public void Cycle()
        {
            Current = (Style)(((int)Current + 1) % 3);
            PlayerPrefs.SetInt(Key, (int)Current);
            PlayerPrefs.Save();
            Apply();
        }

        /// <summary>Plain passthrough while paused or when you leave the scanned area.</summary>
        public void SetSafety(bool on)
        {
            _safety = on;
            Apply();
        }

        void Apply()
        {
            if (_layer == null) return;
            if (_safety || Current == Style.Normal)
            {
                _layer.DisableColorMap();
                return;
            }
            if (Current == Style.White) _layer.SetBrightnessContrastSaturation(0.15f, 0.08f, -0.75f);
            else _layer.SetColorMapControls(-0.05f, 0.1f, 0f, _superhotGradient);
        }
    }
}
