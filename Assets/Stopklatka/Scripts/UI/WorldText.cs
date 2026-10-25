using TMPro;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// A world-space text (STYLE_GUIDE 10): bold, white or red, with a dark drop shadow so it reads over
    /// a bright room. TextMeshPro (TMP Essentials' LiberationSans SDF; its dynamic fallback adds the
    /// Polish letters) with an underlay and a thin dark outline. When TMP is off in GameConfig ui, or its
    /// font or material is missing, the legacy TextMesh with a dark copy behind it, so there is always text.
    /// Sizes are font heights in metres; the root transform is for position, rotation and pop-in scale.
    /// </summary>
    public sealed class WorldText
    {
        public enum Align { Center, Top, Bottom }

        public Transform Transform { get; }

        readonly TextMeshPro _pro;
        readonly RectTransform _rect;
        readonly TextMesh _main, _shadow;
        readonly Align _align;
        readonly float _wrapEms;
        string _text = "";

        // shared resources, made once
        static bool _initDone, _useTmp;
        static TMP_FontAsset _tmpFont;
        static Material _tmpMaterial;
        static Font _legacyFont;
        static Material _legacyMaterial;

        static readonly int UnderlayColorId = Shader.PropertyToID("_UnderlayColor");
        static readonly int UnderlayOffsetXId = Shader.PropertyToID("_UnderlayOffsetX");
        static readonly int UnderlayOffsetYId = Shader.PropertyToID("_UnderlayOffsetY");
        static readonly int UnderlaySoftnessId = Shader.PropertyToID("_UnderlaySoftness");
        static readonly int UnderlayDilateId = Shader.PropertyToID("_UnderlayDilate");
        static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        /// <summary>True when texts use TextMeshPro, false for the legacy TextMesh fallback.</summary>
        public static bool UsingTmp
        {
            get { Init(); return _useTmp; }
        }

        /// <summary>The legacy text material (built-in font, stereo-safe shader). Follows font texture rebuilds.</summary>
        public static Material LegacyMaterial
        {
            get { InitLegacy(); return _legacyMaterial; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Font.textureRebuilt -= OnFontRebuilt;
            _initDone = false;
            _useTmp = false;
            _tmpFont = null;
            _tmpMaterial = null;
            _legacyFont = null;
            _legacyMaterial = null;
        }

        /// <param name="emHeight">Font height in metres.</param>
        /// <param name="wrapEms">Wrap lines wider than this many font heights (0 = only at line breaks).</param>
        public static WorldText Create(string name, Transform parent, float emHeight, Color color, Align align = Align.Center, float wrapEms = 0f)
        {
            Init();
            return new WorldText(name, parent, emHeight, color, align, wrapEms);
        }

        WorldText(string name, Transform parent, float emHeight, Color color, Align align, float wrapEms)
        {
            _align = align;
            _wrapEms = wrapEms;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Transform = root.transform;

            if (_useTmp)
            {
                var go = new GameObject("Text", typeof(RectTransform));
                go.transform.SetParent(Transform, false);
                _rect = go.GetComponent<RectTransform>();
                _rect.pivot = new Vector2(0.5f, align == Align.Top ? 1f : align == Align.Bottom ? 0f : 0.5f);
                _pro = go.AddComponent<TextMeshPro>();
                _pro.font = _tmpFont;
                _pro.fontSharedMaterial = _tmpMaterial;
                _pro.fontStyle = FontStyles.Bold;
                _pro.alignment = align == Align.Top ? TextAlignmentOptions.Top : align == Align.Bottom ? TextAlignmentOptions.Bottom : TextAlignmentOptions.Center;
                _pro.textWrappingMode = wrapEms > 0f ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                _pro.overflowMode = TextOverflowModes.Overflow;
                _pro.text = "";
                NoShadows(go.GetComponent<MeshRenderer>());
            }
            else
            {
                _main = MakeLegacy("Text", Transform, 1);
                // the drop shadow: a dark copy a little down-right and behind, drawn first
                _shadow = MakeLegacy("Shadow", _main.transform, 0);
                _shadow.transform.localPosition = new Vector3(0.8f, -0.8f, 1f);
            }
            EmHeight = emHeight;
            Color = color;
        }

        public string Text
        {
            get => _text;
            set
            {
                value ??= "";
                if (value == _text) return;
                _text = value;
                if (_pro != null) _pro.text = value;
                else
                {
                    _main.text = value;
                    _shadow.text = value;
                }
            }
        }

        public Color Color
        {
            set
            {
                if (_pro != null)
                {
                    _pro.color = value;
                    return;
                }
                _main.color = value;
                var s = Tuning.Ui.shadowColor;
                _shadow.color = new Color(s.r, s.g, s.b, s.a * value.a);
            }
        }

        /// <summary>Font height in metres.</summary>
        public float EmHeight
        {
            set
            {
                if (_pro != null)
                {
                    // a TextMeshPro (3D) font size of 1 is 0.1 m
                    _pro.fontSize = value * 10f;
                    float width = _wrapEms > 0f ? value * _wrapEms : value * 60f;
                    _rect.sizeDelta = new Vector2(width, value * 1.5f);
                }
                else
                {
                    // legacy TextMesh at font size 120: about 12 units per line
                    _main.transform.localScale = Vector3.one * (value / 12f);
                }
            }
        }

        public bool Visible
        {
            set
            {
                if (Transform.gameObject.activeSelf != value) Transform.gameObject.SetActive(value);
            }
        }

        public void Destroy()
        {
            if (Transform != null) Object.Destroy(Transform.gameObject);
        }

        TextMesh MakeLegacy(string name, Transform parent, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = _legacyFont;
            tm.fontSize = 120;
            tm.fontStyle = FontStyle.Bold;
            tm.characterSize = 1f;
            tm.anchor = _align == Align.Top ? TextAnchor.UpperCenter : _align == Align.Bottom ? TextAnchor.LowerCenter : TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.text = "";
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = _legacyMaterial;
            r.sortingOrder = order;
            NoShadows(r);
            return tm;
        }

        static void NoShadows(Renderer r)
        {
            if (r == null) return;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // --- shared resources ------------------------------------------------------------

        static void Init()
        {
            if (_initDone) return;
            _initDone = true;
            InitLegacy();
            var ui = Tuning.Ui;
            if (!ui.useTextMeshPro)
            {
                Debug.Log("[Stopklatka] Texts: legacy TextMesh (GameConfig ui.useTextMeshPro is off).");
                return;
            }
            _tmpFont = Resources.Load<TMP_FontAsset>(ui.tmpFont);
            if (_tmpFont == null)
            {
                Debug.LogWarning($"[Stopklatka] Texts: TMP font Resources/{ui.tmpFont} missing, using the legacy TextMesh. " +
                                 "Run Stopklatka > Build Game (it imports TMP Essential Resources).");
                return;
            }
            // the TMP Essentials drop-shadow material ships with underlay and outline on, so those shader
            // variants are in the build; our copy only changes the values
            var source = Resources.Load<Material>(ui.tmpMaterial);
            if (source == null)
            {
                Debug.LogWarning($"[Stopklatka] Texts: TMP material Resources/{ui.tmpMaterial} missing, texts have no drop shadow.");
                source = _tmpFont.material;
            }
            _tmpMaterial = new Material(source) { name = "Stopklatka TMP Text" };
            var shadow = ui.shadowColor;
            if (_tmpMaterial.HasProperty(UnderlayColorId))
            {
                _tmpMaterial.SetColor(UnderlayColorId, shadow);
                _tmpMaterial.SetFloat(UnderlayOffsetXId, 0.6f);
                _tmpMaterial.SetFloat(UnderlayOffsetYId, -0.6f);
                _tmpMaterial.SetFloat(UnderlaySoftnessId, 0.25f);
                _tmpMaterial.SetFloat(UnderlayDilateId, 0.1f);
            }
            if (_tmpMaterial.HasProperty(OutlineWidthId))
            {
                _tmpMaterial.SetColor(OutlineColorId, new Color(shadow.r, shadow.g, shadow.b, 0.85f));
                _tmpMaterial.SetFloat(OutlineWidthId, 0.08f);
            }
            _useTmp = true;
            Debug.Log($"[Stopklatka] Texts: TextMeshPro ({_tmpFont.name}).");
        }

        static void InitLegacy()
        {
            if (_legacyMaterial != null) return;
            _legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _legacyMaterial = new Material(Mats.Get(Mats.Text)) { mainTexture = _legacyFont.material.mainTexture };
            Font.textureRebuilt -= OnFontRebuilt;
            Font.textureRebuilt += OnFontRebuilt;
        }

        static void OnFontRebuilt(Font f)
        {
            if (f == _legacyFont && _legacyMaterial != null) _legacyMaterial.mainTexture = _legacyFont.material.mainTexture;
        }
    }
}
