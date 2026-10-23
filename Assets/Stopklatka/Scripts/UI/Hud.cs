using System.Collections;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// World-space text that floats ahead of you, slightly above eye level (never in the lower
    /// middle of your view, where you need to see the floor). Bold, with a dark drop shadow so it
    /// stays readable over the bright, washed-out room. Also the red vignette when you get hit.
    /// Uses the built-in font with a stereo-safe text shader.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public static Hud Instance { get; private set; }

        public Material TextMaterial => _textMat;

        /// <summary>A text line and its drop shadow (a dark copy just behind it).</summary>
        class Line
        {
            public TextMesh Main, Shadow;
            public float Scale;

            public string Text
            {
                get => Main.text;
                set { Main.text = value; Shadow.text = value; }
            }

            public Color Color
            {
                set
                {
                    Main.color = value;
                    Shadow.color = new Color(0.04f, 0.04f, 0.06f, 0.6f * value.a);
                }
            }
        }

        static readonly Color Red = new(0.95f, 0.12f, 0.08f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Line _title, _sub, _perf;
        Transform _root;
        Material _textMat;
        Font _font;
        MeshRenderer _vignette;
        Color _vignetteColor;
        float _titleUntil, _subUntil, _flashUntil, _flashLength, _pulseAt = -100f;
        bool _snap = true;

        void Awake()
        {
            Instance = this;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _textMat = new Material(Mats.Get(Mats.Text));
            _textMat.mainTexture = _font.material.mainTexture;
            Font.textureRebuilt += OnFontRebuilt;

            _root = new GameObject("HUD").transform;
            _title = MakeLine("Title", new Vector3(0f, 0.06f, 0f), 0.0085f, Red);
            _sub = MakeLine("Subtitle", new Vector3(0f, -0.09f, 0f), 0.004f, new Color(0.97f, 0.97f, 0.97f));
            _perf = MakeLine("Perf", new Vector3(0f, -0.24f, 0f), 0.003f, new Color(0.7f, 0.9f, 1f));
        }

        void OnDestroy() => Font.textureRebuilt -= OnFontRebuilt;

        void OnFontRebuilt(Font f)
        {
            if (f == _font) _textMat.mainTexture = _font.material.mainTexture;
        }

        Line MakeLine(string name, Vector3 localPos, float scale, Color color)
        {
            var main = MakeText(name, _root, 1);
            main.transform.localPosition = localPos;
            main.transform.localScale = Vector3.one * scale;
            // shadow: child of the text (follows its scale), a little down-right and behind; drawn first
            var shadow = MakeText(name + " Shadow", main.transform, 0);
            shadow.transform.localPosition = new Vector3(0.8f, -0.8f, 1f);   // ~7% of the glyph height
            var line = new Line { Main = main, Shadow = shadow, Scale = scale };
            line.Color = color;
            return line;
        }

        TextMesh MakeText(string name, Transform parent, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.fontSize = 120;
            tm.fontStyle = FontStyle.Bold;
            tm.characterSize = 1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.text = "";
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = _textMat;
            r.sortingOrder = order;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return tm;
        }

        /// <param name="seconds">0 = stays until replaced.</param>
        /// <param name="pulse">Pops the text in slightly larger, then settles.</param>
        public void Title(string text, float seconds = 2f, Color? color = null, bool pulse = false)
        {
            _title.Text = text;
            _title.Color = color ?? Red;
            _titleUntil = seconds > 0f ? Time.unscaledTime + seconds : float.MaxValue;
            if (pulse) _pulseAt = Time.unscaledTime;
            _snap = true;
        }

        public void Sub(string text, float seconds = 3f)
        {
            _sub.Text = text;
            _subUntil = seconds > 0f ? Time.unscaledTime + seconds : float.MaxValue;
        }

        public void Perf(string text) => _perf.Text = text;

        public void Clear()
        {
            _title.Text = "";
            _sub.Text = "";
        }

        /// <summary>Red vignette around the edges of your view when you get hit.</summary>
        public void RedFlash(float seconds = 0.6f)
        {
            EnsureVignette();
            _flashUntil = Time.unscaledTime + seconds;
            _flashLength = seconds;
        }

        void EnsureVignette()
        {
            if (_vignette != null || PlayerRig.Instance == null) return;
            // rings facing the eyes: clear in the middle, solid at the edges of the field of view
            var b = new MeshKit.Builder();
            var facing = Quaternion.Euler(90f, 0f, 0f);
            b.Ring(Vector3.zero, facing, 0.07f, 0.16f, 40);
            b.Ring(Vector3.zero, facing, 0.16f, 0.3f, 40);
            b.Ring(Vector3.zero, facing, 0.3f, 0.5f, 40);
            var mesh = b.ToMesh("Hit Vignette", p =>
            {
                float r = new Vector2(p.x, p.y).magnitude;
                return new Color(1f, 1f, 1f, r < 0.08f ? 0f : r < 0.17f ? 0.35f : r < 0.31f ? 0.85f : 1f);
            });
            var go = MeshKit.Part("HitVignette", PlayerRig.Instance.Head, mesh, Mats.Get(Mats.RedFlash));
            go.transform.localPosition = new Vector3(0f, 0f, 0.2f);
            _vignette = go.GetComponent<MeshRenderer>();
            _vignette.material = new Material(Mats.Get(Mats.RedFlash));
            _vignetteColor = _vignette.sharedMaterial.GetColor(BaseColorId);
            _vignette.enabled = false;
        }

        void LateUpdate()
        {
            var rig = PlayerRig.Instance;
            if (rig == null) return;
            float now = Time.unscaledTime;
            if (now > _titleUntil) _title.Text = "";
            if (now > _subUntil) _sub.Text = "";

            var head = rig.Head;
            var fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            var target = head.position + fwd * 1.7f + Vector3.up * 0.18f;
            _root.position = _snap ? target : Vector3.Lerp(_root.position, target, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
            _root.rotation = Quaternion.LookRotation(_root.position - head.position, Vector3.up);
            _snap = false;

            float pulse = 1f + 0.22f * Mathf.Exp(-(now - _pulseAt) * 9f);
            _title.Main.transform.localScale = Vector3.one * (_title.Scale * pulse);

            if (_vignette != null)
            {
                float left = _flashUntil - now;
                _vignette.enabled = left > 0f;
                if (left > 0f)
                {
                    var c = _vignetteColor;
                    c.a *= Mathf.SmoothStep(0f, 1f, left / _flashLength);
                    _vignette.sharedMaterial.SetColor(BaseColorId, c);
                }
            }
        }

        /// <summary>Superhot-style chant: alternates two words with a chime, each one popping in.</summary>
        public IEnumerator Chant(string a, string b, int times)
        {
            for (int i = 0; i < times; i++)
            {
                Title(a, 0.5f, Color.white, pulse: true);
                SoundBank.Instance?.PlayUi(Sfx.Chime, 0.7f);
                yield return new WaitForSecondsRealtime(0.55f);
                Title(b, 0.5f, Red, pulse: true);
                SoundBank.Instance?.PlayUi(Sfx.Chime, 0.7f);
                yield return new WaitForSecondsRealtime(0.55f);
            }
        }
    }
}
