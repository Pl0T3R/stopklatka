using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// Debug gauge above the left wrist: a red bar for the current time scale, a white tick where
    /// it is heading (before smoothing) and the value as text ("x0.03"). Turns to face your eyes.
    /// On with GameConfig > debug > wristTimeIndicator; always on in the time test scene.
    /// </summary>
    public class TimeIndicator : MonoBehaviour
    {
        [Tooltip("Show even when GameConfig turns the gauge off (the time test scene).")]
        public bool AlwaysOn;

        [Tooltip("Position relative to the left controller anchor: above the back of the wrist.")]
        public Vector3 WristOffset = new(0f, 0.05f, -0.1f);

        const float Width = 0.07f;
        const float BarY = -0.009f, BarHeight = 0.007f;

        Transform _root, _fill, _tick;
        TextMesh _label;
        int _shown = -1;

        void Start()
        {
            _root = new GameObject("WristTimeIndicator").transform;

            var plate = new MeshKit.Builder();
            plate.Box(Vector3.zero, new Vector3(Width + 0.01f, 0.034f, 0.004f), Quaternion.identity);
            MeshKit.Part("Plate", _root, plate.ToMesh("Time Gauge Plate"), Mats.Get(Mats.Weapon));

            // unit box starting at x = 0, scaled on X by the time scale; the front of the gauge is -Z
            var bar = new MeshKit.Builder();
            bar.Box(new Vector3(0.5f, 0f, 0f), Vector3.one, Quaternion.identity);
            var barMesh = bar.ToMesh("Time Gauge Bar");
            _fill = MeshKit.Part("Fill", _root, barMesh, Mats.Get(Mats.Enemy)).transform;
            _fill.localPosition = new Vector3(-Width * 0.5f, BarY, -0.003f);
            _tick = MeshKit.Part("Target", _root, barMesh, Mats.Get(Mats.Glass)).transform;
            _tick.localScale = new Vector3(0.0015f, BarHeight + 0.004f, 0.002f);

            var hud = Hud.Instance;
            if (hud != null)
            {
                var go = new GameObject("Value");
                go.transform.SetParent(_root, false);
                go.transform.localPosition = new Vector3(0f, 0.006f, -0.003f);
                go.transform.localScale = Vector3.one * 0.0009f;
                _label = go.AddComponent<TextMesh>();
                _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _label.fontSize = 120;
                _label.fontStyle = FontStyle.Bold;
                _label.characterSize = 1f;
                _label.anchor = TextAnchor.MiddleCenter;
                _label.alignment = TextAlignment.Center;
                _label.color = Color.white;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = hud.TextMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        void LateUpdate()
        {
            if (_root == null) return;
            var rig = PlayerRig.Instance;
            var time = TimeController.Instance;
            bool on = (AlwaysOn || GameConfig.Instance.debug.wristTimeIndicator) && rig != null && time != null;
            if (_root.gameObject.activeSelf != on) _root.gameObject.SetActive(on);
            if (!on) return;

            var head = rig.Head;
            _root.position = rig.LeftController.TransformPoint(WristOffset);
            _root.rotation = Quaternion.LookRotation(_root.position - head.position, head.up);

            float max = Mathf.Max(0.01f, GameConfig.Instance.time.maxTimeScale);
            float fill = Mathf.Clamp01(time.Scale / max);
            _fill.localScale = new Vector3(Mathf.Max(0.0005f, Width * fill), BarHeight, 0.002f);
            _tick.localPosition = new Vector3(-Width * 0.5f + Width * Mathf.Clamp01(time.Target / max) - 0.00075f, BarY, -0.0035f);

            int shown = Mathf.RoundToInt(time.Scale * 100f);
            if (_label != null && shown != _shown)
            {
                _shown = shown;
                _label.text = "x" + (shown / 100f).ToString("0.00");
            }
        }
    }
}
