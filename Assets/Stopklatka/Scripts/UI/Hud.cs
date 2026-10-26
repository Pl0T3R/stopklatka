using System.Collections;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// Titles and subtitles in the world (GAME_SPEC 6, STYLE_GUIDE 10), never glued to your face: a new
    /// title is put once at a fixed spot in front of you, on the real wall ahead when one is 1.5 to 6 m
    /// away (GameConfig ui), else 2.5 m ahead, a little above eye level, and sized for that distance.
    /// It stays there (turning only to face you) and only moves in front of you again when you have
    /// looked away from it for a while. Bold, white or red, with a dark drop shadow (<see cref="WorldText"/>:
    /// TextMeshPro, or the legacy TextMesh fallback). Also the red vignette when you get hit and the
    /// debug readout (which does follow the head).
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public static Hud Instance { get; private set; }

        /// <summary>The legacy text material (the wrist gauge's label uses it).</summary>
        public Material TextMaterial => WorldText.LegacyMaterial;

        static readonly Color Red = new(0.95f, 0.12f, 0.08f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        WorldText _title, _sub, _perf;
        Transform _anchor, _perfRoot;
        MeshRenderer _vignette;
        Color _vignetteColor;
        float _titleUntil, _subUntil, _flashUntil, _flashLength, _pulseAt = -100f, _awayFor;
        float _titleEm = 0.1f, _distance;
        bool _placed, _snapPerf = true;

        void Awake()
        {
            Instance = this;
            _anchor = new GameObject("HUD").transform;
            _title = WorldText.Create("Title", _anchor, 0.1f, Red);
            _sub = WorldText.Create("Subtitle", _anchor, 0.05f, new Color(0.97f, 0.97f, 0.97f), WorldText.Align.Top, 26f);
            _perfRoot = new GameObject("HUD Perf").transform;
            _perf = WorldText.Create("Perf", _perfRoot, 0.036f, new Color(0.7f, 0.9f, 1f), WorldText.Align.Top);
            _perf.Transform.localPosition = new Vector3(0f, -0.24f, 0f);
        }

        void OnDestroy()
        {
            if (_anchor != null) Destroy(_anchor.gameObject);
            if (_perfRoot != null) Destroy(_perfRoot.gameObject);
        }

        /// <param name="seconds">0 = stays until replaced.</param>
        /// <param name="pulse">Pops the text in slightly larger, then settles.</param>
        public void Title(string text, float seconds = 2f, Color? color = null, bool pulse = false)
        {
            _title.Text = text;
            _title.Color = color ?? Red;
            _titleUntil = seconds > 0f ? Time.unscaledTime + seconds : float.MaxValue;
            if (pulse) _pulseAt = Time.unscaledTime;
            Place(force: false);
        }

        public void Sub(string text, float seconds = 3f)
        {
            _sub.Text = text;
            _subUntil = seconds > 0f ? Time.unscaledTime + seconds : float.MaxValue;
            if (string.IsNullOrEmpty(_title.Text)) Place(force: false);
        }

        public void Perf(string text) => _perf.Text = text;

        public void Clear()
        {
            _title.Text = "";
            _sub.Text = "";
        }

        /// <summary>
        /// Puts the titles at a fixed spot ahead: on the real wall in front (raycast against the scanned
        /// room's colliders) if it is within ui.wallMin/MaxDistance, else ui.defaultDistance ahead.
        /// Without force, a spot still within ui.keepAngle of your view is kept (no jumping between words).
        /// </summary>
        void Place(bool force)
        {
            var rig = PlayerRig.Instance;
            if (rig == null) return;
            var ui = Tuning.Ui;
            var head = rig.Head;
            var fwd = Flat(head.forward);
            if (!force && _placed && Vector3.Angle(fwd, Flat(_anchor.position - head.position)) < ui.keepAngle &&
                Mathf.Abs(Vector3.Distance(head.position, _anchor.position) - _distance) < 0.75f) return;

            var origin = head.position + Vector3.up * ui.aboveEyes;
            float distance = ui.defaultDistance;
            if (Physics.Raycast(origin, fwd, out var hit, ui.wallMaxDistance, Layers.RoomMask, QueryTriggerInteraction.Ignore))
            {
                // on the wall when it is far enough, else just in front of whatever is close ahead
                distance = hit.distance >= ui.wallMinDistance ? hit.distance - 0.08f : Mathf.Max(0.8f, Mathf.Min(distance, hit.distance - 0.08f));
            }
            _anchor.position = origin + fwd * distance;
            _distance = distance;
            _anchor.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            _titleEm = distance * ui.titleSize;
            _title.EmHeight = _titleEm;
            _sub.EmHeight = distance * ui.subtitleSize;
            _sub.Transform.localPosition = new Vector3(0f, -_titleEm * 0.8f, 0f);
            _placed = true;
            _awayFor = 0f;
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
            using var perfScope = PerfMarkers.Hud.Auto();
            var rig = PlayerRig.Instance;
            if (rig == null) return;
            float now = Time.unscaledTime;
            if (now > _titleUntil) _title.Text = "";
            if (now > _subUntil) _sub.Text = "";

            var head = rig.Head;
            bool showing = _title.Text.Length > 0 || _sub.Text.Length > 0;
            if (showing && _placed)
            {
                // world-locked: only turn to face you; move back in front of you after looking away a while
                var ui = Tuning.Ui;
                var toText = Flat(_anchor.position - head.position);
                _anchor.rotation = Quaternion.LookRotation(toText, Vector3.up);
                if (Vector3.Angle(Flat(head.forward), toText) > ui.reanchorAngle)
                {
                    _awayFor += Time.unscaledDeltaTime;
                    if (_awayFor > ui.reanchorDelay) Place(force: true);
                }
                else _awayFor = 0f;
            }

            float pulse = 1f + 0.22f * Mathf.Exp(-(now - _pulseAt) * 9f);
            _title.Transform.localScale = Vector3.one * pulse;

            // debug readout: follows the head softly (debug only)
            if (_perf.Text.Length > 0)
            {
                var target = head.position + Flat(head.forward) * 1.7f + Vector3.up * 0.18f;
                _perfRoot.position = _snapPerf ? target : Vector3.Lerp(_perfRoot.position, target, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
                _perfRoot.rotation = Quaternion.LookRotation(_perfRoot.position - head.position, Vector3.up);
                _snapPerf = false;
            }
            else _snapPerf = true;

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

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-4f ? Vector3.forward : v.normalized;
        }

        static void Chime()
        {
            var sound = SoundBank.Instance;
            if (sound != null) sound.PlayUi(Sfx.Chime, 0.7f);
        }

        /// <summary>Superhot-style chant: alternates two words with a chime, each one popping in.</summary>
        public IEnumerator Chant(string a, string b, int times)
        {
            for (int i = 0; i < times; i++)
            {
                Title(a, 0.5f, Color.white, pulse: true);
                Chime();
                yield return new WaitForSecondsRealtime(0.55f);
                Title(b, 0.5f, Red, pulse: true);
                Chime();
                yield return new WaitForSecondsRealtime(0.55f);
            }
        }
    }
}
