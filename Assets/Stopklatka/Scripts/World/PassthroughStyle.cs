using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// Makes the real school look washed-out and bright (Superhot's white world) while enemies stay red.
    /// Kept moderate for safety, switchable from the menu, and turned off whenever the game pauses.
    /// Also asks the system to hide the boundary while passthrough is on, so you can walk the corridors.
    /// </summary>
    public class PassthroughStyle : MonoBehaviour
    {
        public static PassthroughStyle Instance { get; private set; }

        public bool Stylized { get; private set; } = true;

        OVRPassthroughLayer _layer;
        bool _safety;

        void Awake() => Instance = this;

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

        public void Toggle()
        {
            Stylized = !Stylized;
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
            if (Stylized && !_safety) _layer.SetBrightnessContrastSaturation(0.12f, 0.08f, -0.7f);
            else _layer.DisableColorMap();
        }
    }
}
