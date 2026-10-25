using UnityEngine;
using UnityEngine.XR;

namespace Stopklatka
{
    /// <summary>
    /// Asks for the refresh rate from GameConfig (90 Hz by default, 120 as an option), keeps the
    /// GPU/CPU clocked for it and logs what the headset actually runs at.
    /// Resolution is handled by OVRManager's dynamic resolution (render scale 1.0..1.205, set by the
    /// builder): it renders at ~2024 px per eye and drops only when needed to hold the frame rate.
    /// Left thumbstick click toggles an on-screen readout.
    /// </summary>
    public class PerformanceManager : MonoBehaviour
    {
        public static PerformanceManager Instance { get; private set; }

        /// <summary>The display refresh rate in use (Hz). Physics runs one step per displayed frame.</summary>
        public float RefreshRate { get; private set; }

        public float Fps { get; private set; }
        public float WorstFrameMs { get; private set; }
        public bool ShowReadout { get; private set; }

        float _accTime, _worst;
        int _frames;
        float _nextLog;

        void Awake()
        {
            Instance = this;
            RefreshRate = GameConfig.Instance.performance.targetRefreshRate;
            ShowReadout = GameConfig.Instance.debug.performanceReadout;
        }

        void Start()
        {
            var perf = GameConfig.Instance.performance;
            ApplyRefreshRate(perf.targetRefreshRate);
            OVRManager.foveatedRenderingLevel = perf.foveation;
            OVRManager.useDynamicFoveatedRendering = perf.dynamicFoveation;
            OVRManager.suggestedCpuPerfLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;
            OVRManager.suggestedGpuPerfLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;
            _nextLog = Time.unscaledTime + 3f;
        }

        void ApplyRefreshRate(float target)
        {
            float[] available = OVRManager.display != null ? OVRManager.display.displayFrequenciesAvailable : null;
            float chosen = 0f;
            if (available != null)
            {
                foreach (var f in available)
                {
                    if (Mathf.Abs(f - target) < 0.5f) { chosen = f; break; }
                    if (f <= target && f > chosen) chosen = f;
                }
            }
            if (chosen > 0f) OVRPlugin.systemDisplayFrequency = chosen;
            float actual = OVRPlugin.systemDisplayFrequency;
            RefreshRate = actual > 1f ? actual : chosen > 0f ? chosen : target;
            Debug.Log($"[Stopklatka] Refresh rates available: {(available == null ? "none" : string.Join(", ", available))}; " +
                      $"requested {chosen} Hz, running {RefreshRate} Hz, physics step 1/{RefreshRate:F0} s");
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _accTime += dt;
            _frames++;
            _worst = Mathf.Max(_worst, dt);
            if (_accTime >= 0.5f)
            {
                Fps = _frames / _accTime;
                WorstFrameMs = _worst * 1000f;
                _accTime = 0f; _frames = 0; _worst = 0f;
            }

            if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch))
                ShowReadout = !ShowReadout;

            if (Time.unscaledTime >= _nextLog)
            {
                _nextLog = Time.unscaledTime + 5f;
                Debug.Log($"[Stopklatka] {Fps:F0} fps, worst {WorstFrameMs:F1} ms, display {OVRPlugin.systemDisplayFrequency} Hz, " +
                          $"eye texture {XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight}, " +
                          $"scale {XRSettings.eyeTextureResolutionScale:F3}, viewport {XRSettings.renderViewportScale:F2}, time {Time.timeScale:F2}");
            }
        }

        public string Readout() =>
            $"{Fps:F0} FPS  ({WorstFrameMs:F1} ms)\n{OVRPlugin.systemDisplayFrequency:F0} Hz  " +
            $"{Mathf.RoundToInt(XRSettings.eyeTextureWidth * XRSettings.renderViewportScale)}x" +
            $"{Mathf.RoundToInt(XRSettings.eyeTextureHeight * XRSettings.renderViewportScale)}\nczas x{Time.timeScale:F2}";
    }
}
