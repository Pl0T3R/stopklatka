using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace Stopklatka
{
    /// <summary>
    /// Asks for the refresh rate from GameConfig (90 Hz by default, 120 as an option), keeps the
    /// GPU/CPU clocked for it and logs what the headset actually runs at.
    /// Resolution is handled by OVRManager's dynamic resolution (render scale 1.0..1.205, set by the
    /// builder): it renders at ~2024 px per eye and drops only when needed to hold the frame rate.
    /// If that path can't work on this device or build (GameConfig performance.resolutionFallback),
    /// this script lowers the render scale itself when frames are missed and raises it back slowly:
    /// frame rate wins over resolution. Optionally (off by default) it steps the display down to
    /// 72 Hz when frames are still missed at the lowest resolution.
    /// Left thumbstick click toggles an on-screen readout: frame time, draw calls, SetPass calls,
    /// triangles, GC allocations per frame and the game's own profiler markers.
    /// </summary>
    public class PerformanceManager : MonoBehaviour
    {
        public static PerformanceManager Instance { get; private set; }

        /// <summary>The display refresh rate in use (Hz). Physics runs one step per displayed frame.</summary>
        public float RefreshRate { get; private set; }

        public float Fps { get; private set; }
        public float WorstFrameMs { get; private set; }
        public float AverageFrameMs { get; private set; }
        public bool ShowReadout { get; private set; }

        /// <summary>Changes whenever the readout text is rebuilt (twice a second while it shows).</summary>
        public int ReadoutStamp { get; private set; }

        /// <summary>The resolution fallback is in charge of the render scale (see the class summary).</summary>
        public bool FallbackActive { get; private set; }

        float _targetRate;
        float _accTime, _worst;
        int _frames;
        float _nextLog, _nextReadout;
        string _readout = "";
        readonly StringBuilder _text = new(320);

        // resolution fallback and refresh step-down
        bool _fallbackDecided, _steppedDown, _warnedIdle;
        float _decideAt, _scale, _overFor, _underFor, _upWait, _lastUpAt = -100f, _stepDownFor, _idleOverFor;

        // readout counters (Unity's profiler counters; many only exist in development builds)
        ProfilerRecorder _drawCalls, _setPass, _triangles, _gcAlloc, _mainThread;
        ProfilerRecorder[] _markers;
        bool _recording;

        void Awake()
        {
            Instance = this;
            _targetRate = GameConfig.Instance.performance.targetRefreshRate;
            RefreshRate = _targetRate;
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
            _decideAt = Time.unscaledTime + 2f;   // OVRManager settles its dynamic resolution first
            _upWait = Mathf.Max(1f, perf.fallbackUpAfter);
        }

        void OnDestroy()
        {
            if (_recording) StopRecorders();
            if (Instance == this) Instance = null;
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
                AverageFrameMs = _accTime * 1000f / _frames;
                WorstFrameMs = _worst * 1000f;
                float window = _accTime;
                _accTime = 0f; _frames = 0; _worst = 0f;
                OnWindow(window);
            }

            if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch))
                ShowReadout = !ShowReadout;
            UpdateReadout();

            if (Time.unscaledTime >= _nextLog)
            {
                _nextLog = Time.unscaledTime + 5f;
                Debug.Log($"[Stopklatka] {Fps:F0} fps, avg {AverageFrameMs:F1} ms, worst {WorstFrameMs:F1} ms, display {OVRPlugin.systemDisplayFrequency} Hz, " +
                          $"eye texture {XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight}, " +
                          $"scale {XRSettings.eyeTextureResolutionScale:F3}, viewport {XRSettings.renderViewportScale:F2}" +
                          $"{(FallbackActive ? " (fallback)" : "")}, instanced {InstanceDrawer.LastBatches} draws / {InstanceDrawer.LastInstances} objects, time {Time.timeScale:F2}");
            }
        }

        // --- frame budget: resolution fallback and refresh step-down ------------------------------

        float BudgetMs => 1000f / Mathf.Max(30f, RefreshRate);

        /// <summary>Every half second: were frames missed, and what to do about it.</summary>
        void OnWindow(float seconds)
        {
            var perf = GameConfig.Instance.performance;
            bool over = AverageFrameMs > BudgetMs * Mathf.Max(1f, perf.overBudgetFactor);
            if (!_fallbackDecided && Time.unscaledTime >= _decideAt) DecideFallback(perf);
            if (FallbackActive) StepResolution(perf, over, seconds);
            else WatchOvrResolution(over, seconds);
            StepRefreshRate(perf, over, seconds);
        }

        void DecideFallback(PerformanceSettings perf)
        {
            _fallbackDecided = true;
            string reason = null;
            switch (perf.resolutionFallback)
            {
                case ResolutionFallbackMode.Off:
                    return;
                case ResolutionFallbackMode.Force:
                    reason = "forced in GameConfig";
                    var manager = OVRManager.instance;
                    if (manager != null) manager.enableDynamicResolution = false;   // one of us drives the scale
                    break;
                default:
                    if (Application.isEditor) return;   // OVRManager's path only exists in Android builds
                    reason = OvrDynamicResolutionMissing();
                    if (reason == null) return;
                    break;
            }
            FallbackActive = true;
            _scale = Mathf.Max(perf.fallbackMinScale, perf.fallbackMaxScale);
            XRSettings.renderViewportScale = 1f;
            ApplyScale(_scale);
            Debug.Log($"[Stopklatka] Resolution fallback on ({reason}): render scale {_scale:F2}, " +
                      $"range {perf.fallbackMinScale:F2}..{perf.fallbackMaxScale:F2}.");
        }

        /// <summary>Why OVRManager's dynamic resolution can't run here, or null if it can.</summary>
        static string OvrDynamicResolutionMissing()
        {
            var manager = OVRManager.instance;
            if (manager == null) return "no OVRManager";
            // OVRManager switches it off itself (and logs why) on unsupported Unity / OpenXR versions
            if (!manager.enableDynamicResolution) return "OVRManager dynamic resolution is off";
            if (!OVRPlugin.GetEyeLayerRecommendedResolution(out _)) return "the runtime gives no recommended eye resolution";
            return null;
        }

        void StepResolution(PerformanceSettings perf, bool over, float seconds)
        {
            float min = Mathf.Min(perf.fallbackMinScale, perf.fallbackMaxScale);
            float max = Mathf.Max(perf.fallbackMinScale, perf.fallbackMaxScale);
            float step = Mathf.Max(0.01f, perf.fallbackStep);
            if (over)
            {
                _overFor += seconds;
                _underFor = 0f;
                if (_overFor < perf.fallbackDownAfter || _scale <= min + 0.001f) return;
                // a step up that had to be taken back: wait longer before the next one
                if (Time.unscaledTime - _lastUpAt < perf.fallbackDownAfter + 3f) _upWait = Mathf.Min(_upWait * 2f, 120f);
                SetFallbackScale(Mathf.Max(min, _scale - step), "frames missed");
                _overFor = 0f;
            }
            else
            {
                _underFor += seconds;
                _overFor = 0f;
                if (_underFor < _upWait || _scale >= max - 0.001f) return;
                SetFallbackScale(Mathf.Min(max, _scale + step), "no missed frames");
                _lastUpAt = Time.unscaledTime;
                _underFor = 0f;
            }
        }

        void SetFallbackScale(float scale, string why)
        {
            Debug.Log($"[Stopklatka] Resolution fallback: render scale {_scale:F2} -> {scale:F2} ({why}, " +
                      $"average {AverageFrameMs:F1} ms, budget {BudgetMs:F1} ms).");
            _scale = scale;
            ApplyScale(scale);
        }

        /// <summary>The eye textures' scale, the same two places OVRManager sets at startup.</summary>
        static void ApplyScale(float scale)
        {
            XRSettings.eyeTextureResolutionScale = scale;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.renderScale = scale;
        }

        /// <summary>
        /// OVRManager's path claims to work: if frames are missed for a long time while its viewport
        /// scale never moves, say so once (it may be silently doing nothing on this build).
        /// </summary>
        void WatchOvrResolution(bool over, float seconds)
        {
            if (_warnedIdle || Application.isEditor) return;
            if (!over || XRSettings.renderViewportScale < 0.999f) { _idleOverFor = 0f; return; }
            _idleOverFor += seconds;
            if (_idleOverFor < 10f) return;
            _warnedIdle = true;
            Debug.LogWarning("[Stopklatka] Frames missed for 10 s while the viewport scale stays 1.00: OVRManager's dynamic " +
                             "resolution may not be acting (or the CPU is the limit). To test the fallback, set GameConfig " +
                             "performance.resolutionFallback to Force.");
        }

        /// <summary>At the lowest resolution and still missing frames: 72 Hz (GameConfig performance.allowRefreshStepDown).</summary>
        void StepRefreshRate(PerformanceSettings perf, bool over, float seconds)
        {
            if (!perf.allowRefreshStepDown || _steppedDown || RefreshRate <= perf.stepDownRate + 0.5f) return;
            if (!over || !ResolutionAtMinimum(perf)) { _stepDownFor = 0f; return; }
            _stepDownFor += seconds;
            if (_stepDownFor < perf.stepDownAfter) return;
            float from = RefreshRate;
            ApplyRefreshRate(perf.stepDownRate);
            _steppedDown = RefreshRate < from - 0.5f;
            _stepDownFor = 0f;
            Debug.Log($"[Stopklatka] Refresh step-down: {from:F0} Hz -> {RefreshRate:F0} Hz (frames missed for {perf.stepDownAfter:F0} s at the lowest resolution).");
        }

        bool ResolutionAtMinimum(PerformanceSettings perf)
        {
            if (FallbackActive) return _scale <= Mathf.Min(perf.fallbackMinScale, perf.fallbackMaxScale) + 0.001f;
            var manager = OVRManager.instance;
            if (manager == null || !manager.enableDynamicResolution) return true;   // nothing else left to lower
            float lowest = manager.minDynamicResolutionScale / Mathf.Max(0.01f, manager.maxDynamicResolutionScale);
            return XRSettings.renderViewportScale <= lowest + 0.02f;
        }

        /// <summary>Back to the configured refresh rate after a step-down (called in the menu).</summary>
        public void RestoreTargetRefreshRate()
        {
            if (!_steppedDown) return;
            _steppedDown = false;
            float from = RefreshRate;
            ApplyRefreshRate(_targetRate);
            Debug.Log($"[Stopklatka] Refresh step-down undone: {from:F0} Hz -> {RefreshRate:F0} Hz.");
        }

        // --- readout ----------------------------------------------------------------------------

        void UpdateReadout()
        {
            if (!ShowReadout)
            {
                if (_recording) StopRecorders();
                return;
            }
            if (!_recording) StartRecorders();
            if (Time.unscaledTime < _nextReadout) return;
            _nextReadout = Time.unscaledTime + Mathf.Max(0.1f, GameConfig.Instance.debug.readoutInterval);
            BuildReadout();
        }

        void StartRecorders()
        {
            _recording = true;
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 15);
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 15);
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 15);
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 15);
            _mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            var list = PerfMarkers.Readout;
            _markers = new ProfilerRecorder[list.Length];
            for (int i = 0; i < list.Length; i++)
                _markers[i] = ProfilerRecorder.StartNew(list[i].Category, list[i].Name, 15,
                    ProfilerRecorderOptions.Default | ProfilerRecorderOptions.SumAllSamplesInFrame);
        }

        void StopRecorders()
        {
            _recording = false;
            _drawCalls.Dispose();
            _setPass.Dispose();
            _triangles.Dispose();
            _gcAlloc.Dispose();
            _mainThread.Dispose();
            if (_markers != null)
                for (int i = 0; i < _markers.Length; i++) _markers[i].Dispose();
            _markers = null;
        }

        /// <summary>Average of the recorder's last samples; false if Unity doesn't provide this counter here.</summary>
        static bool Average(ref ProfilerRecorder r, out double value)
        {
            value = 0;
            if (!r.Valid || r.Count == 0) return false;
            int n = r.Count;
            long sum = 0;
            for (int i = 0; i < n; i++) sum += r.GetSample(i).Value;
            value = sum / (double)n;
            return true;
        }

        void AppendCount(string label, ref ProfilerRecorder r, float divide = 1f, string unit = "")
        {
            _text.Append(label).Append(' ');
            if (Average(ref r, out var v)) _text.Append((v / divide).ToString(divide > 1f ? "F1" : "F0")).Append(unit);
            else _text.Append('–');
            _text.Append("  ");
        }

        void BuildReadout()
        {
            var t = _text;
            t.Clear();
            t.Append(Fps.ToString("F0")).Append(" FPS  śr ").Append(AverageFrameMs.ToString("F1"))
             .Append(" ms  maks ").Append(WorstFrameMs.ToString("F1")).Append(" ms\n");
            float viewport = XRSettings.renderViewportScale;
            t.Append(OVRPlugin.systemDisplayFrequency.ToString("F0")).Append(" Hz  ")
             .Append(Mathf.RoundToInt(XRSettings.eyeTextureWidth * viewport)).Append('x')
             .Append(Mathf.RoundToInt(XRSettings.eyeTextureHeight * viewport)).Append("  skala ")
             .Append((XRSettings.eyeTextureResolutionScale * viewport).ToString("F2"))
             .Append(FallbackActive ? " (zapas)" : "").Append(_steppedDown ? " (obniżone Hz)" : "").Append('\n');
            AppendCount("DC", ref _drawCalls);
            AppendCount("SetPass", ref _setPass);
            AppendCount("trójk.", ref _triangles, 1000f, "k");
            t.Append('\n');
            AppendCount("GC", ref _gcAlloc, 1f, " B/kl.");
            AppendCount("wątek gł.", ref _mainThread, 1e6f, " ms");
            t.Append("\ninstancje ").Append(InstanceDrawer.LastBatches).Append(" draw / ")
             .Append(InstanceDrawer.LastInstances).Append(" obiektów");
            if (GameConfig.Instance.debug.readoutMarkers && _markers != null)
            {
                var list = PerfMarkers.Readout;
                int shown = 0;
                for (int i = 0; i < _markers.Length; i++)
                {
                    if (!Average(ref _markers[i], out var ns)) continue;
                    t.Append(shown % 3 == 0 ? "\n" : "  ").Append(list[i].Label).Append(' ').Append((ns / 1e6).ToString("F2"));
                    shown++;
                }
                if (shown == 0) t.Append("\nmarkery: tylko w Development Build");
            }
            t.Append("\nczas x").Append(Time.timeScale.ToString("F2"));
            _readout = t.ToString();
            ReadoutStamp++;
        }

        /// <summary>The readout text (rebuilt at GameConfig debug.readoutInterval, not every frame).</summary>
        public string Readout() => _readout;
    }
}
