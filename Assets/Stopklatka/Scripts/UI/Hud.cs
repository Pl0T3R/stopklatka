using System.Collections;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// World-space text that floats ahead of you, slightly above eye level (never in the lower
    /// middle of your view, where you need to see the floor). Also the red "hit" flash.
    /// Uses the built-in font with a stereo-safe text shader.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public static Hud Instance { get; private set; }

        public Material TextMaterial => _textMat;

        TextMesh _title, _sub, _perf;
        Transform _root;
        Material _textMat;
        Font _font;
        MeshRenderer _flash;
        float _titleUntil, _subUntil, _flashUntil, _flashLength;
        bool _snap = true;

        void Awake()
        {
            Instance = this;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _textMat = new Material(Mats.Get(Mats.Text));
            _textMat.mainTexture = _font.material.mainTexture;
            Font.textureRebuilt += OnFontRebuilt;

            _root = new GameObject("HUD").transform;
            _title = MakeText("Title", new Vector3(0f, 0.06f, 0f), 0.0085f, new Color(0.95f, 0.12f, 0.08f));
            _sub = MakeText("Subtitle", new Vector3(0f, -0.09f, 0f), 0.004f, new Color(0.96f, 0.96f, 0.96f));
            _perf = MakeText("Perf", new Vector3(0f, -0.24f, 0f), 0.003f, new Color(0.7f, 0.9f, 1f));
        }

        void OnDestroy() => Font.textureRebuilt -= OnFontRebuilt;

        void OnFontRebuilt(Font f)
        {
            if (f == _font) _textMat.mainTexture = _font.material.mainTexture;
        }

        TextMesh MakeText(string name, Vector3 localPos, float scale, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * scale;
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.fontSize = 120;
            tm.characterSize = 1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.text = "";
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = _textMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return tm;
        }

        /// <param name="seconds">0 = stays until replaced.</param>
        public void Title(string text, float seconds = 2f, Color? color = null)
        {
            _title.text = text;
            _title.color = color ?? new Color(0.95f, 0.12f, 0.08f);
            _titleUntil = seconds > 0f ? Time.unscaledTime + seconds : float.MaxValue;
            _snap = true;
        }

        public void Sub(string text, float seconds = 3f)
        {
            _sub.text = text;
            _subUntil = seconds > 0f ? Time.unscaledTime + seconds : float.MaxValue;
        }

        public void Perf(string text) => _perf.text = text;

        public void Clear()
        {
            _title.text = "";
            _sub.text = "";
        }

        /// <summary>Brief red tint when you get hit.</summary>
        public void RedFlash(float seconds = 0.6f)
        {
            EnsureFlash();
            _flashUntil = Time.unscaledTime + seconds;
            _flashLength = seconds;
        }

        void EnsureFlash()
        {
            if (_flash != null || PlayerRig.Instance == null) return;
            var b = new MeshKit.Builder();
            b.Box(Vector3.zero, new Vector3(1.2f, 1.2f, 0.001f), Quaternion.identity);
            var go = MeshKit.Part("RedFlash", PlayerRig.Instance.Head, b.ToMesh("Red Flash"), Mats.Get(Mats.RedFlash));
            go.transform.localPosition = new Vector3(0f, 0f, 0.25f);
            _flash = go.GetComponent<MeshRenderer>();
            _flash.material = new Material(Mats.Get(Mats.RedFlash));
            _flash.enabled = false;
        }

        void LateUpdate()
        {
            var rig = PlayerRig.Instance;
            if (rig == null) return;
            float now = Time.unscaledTime;
            if (now > _titleUntil) _title.text = "";
            if (now > _subUntil) _sub.text = "";

            var head = rig.Head;
            var fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            var target = head.position + fwd * 1.7f + Vector3.up * 0.18f;
            _root.position = _snap ? target : Vector3.Lerp(_root.position, target, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
            _root.rotation = Quaternion.LookRotation(_root.position - head.position, Vector3.up);
            _snap = false;

            if (_flash != null)
            {
                float left = _flashUntil - now;
                _flash.enabled = left > 0f;
                if (left > 0f) _flash.material.SetColor("_BaseColor", new Color(1f, 0f, 0f, 0.55f * left / _flashLength));
            }
        }

        /// <summary>Superhot-style chant: alternates two words with a chime.</summary>
        public IEnumerator Chant(string a, string b, int times)
        {
            for (int i = 0; i < times; i++)
            {
                Title(a, 0.5f, Color.white);
                SoundBank.Instance?.PlayUi(Sfx.Chime, 0.7f);
                yield return new WaitForSecondsRealtime(0.55f);
                Title(b, 0.5f, new Color(0.95f, 0.12f, 0.08f));
                SoundBank.Instance?.PlayUi(Sfx.Chime, 0.7f);
                yield return new WaitForSecondsRealtime(0.55f);
            }
        }
    }
}
