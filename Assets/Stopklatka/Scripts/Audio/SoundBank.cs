using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Stopklatka
{
    public enum Sfx { Pistol, Shotgun, Empty, Shatter, GlassBreak, Swing, Slice, Deflect, Thud, Grab, Chime, Death, Spawn, Ricochet, Smg, Punch, Clash }

    /// <summary>
    /// All sounds are synthesized at startup (no audio files). 3D one-shots come from a pool of
    /// AudioSources. World sounds slow down with time (GAME_SPEC 7): through the AudioMixer's exposed
    /// WorldPitch (Resources/Audio/StopklatkaMixer, made by Stopklatka > 5. Create Audio Mixer) or,
    /// without a mixer, by setting the pitch of each playing sound. UI sounds never slow down.
    /// Ambient (2D, quiet): a low hum while time stands still that fades as time speeds up, and an airy
    /// rush that rises as you move. Numbers in GameConfig sound.
    /// </summary>
    public class SoundBank : MonoBehaviour
    {
        public static SoundBank Instance { get; private set; }

        // the mixer and its parts, by name (the builder creates them with these names)
        public const string MixerResource = "Audio/StopklatkaMixer";
        public const string WorldGroup = "World", UiGroup = "UI", AmbientGroup = "Ambient";
        public const string WorldPitchParam = "WorldPitch", AmbientVolumeParam = "AmbientVolume";

        const int Rate = 44100;
        const int Voices = 24;

        readonly Dictionary<Sfx, AudioClip> _clips = new();
        readonly List<AudioSource> _voices = new();
        readonly List<bool> _worldTime = new();
        readonly List<float> _basePitch = new();
        AudioMixer _mixer;
        AudioMixerGroup _world, _ui, _ambient;
        bool _mixerPitch;
        AudioSource _hum, _air;
        int _next;

        void Awake()
        {
            Instance = this;
            var rng = new System.Random(1234);
            _clips[Sfx.Pistol] = Gunshot("pistol", 0.32f, 1.0f, 95f, rng);
            _clips[Sfx.Shotgun] = Gunshot("shotgun", 0.55f, 1.0f, 60f, rng);
            _clips[Sfx.Empty] = Click("empty", 2600f, 0.05f);
            _clips[Sfx.Shatter] = Shatter("shatter", 0.9f, 1800f, 6200f, rng);
            _clips[Sfx.GlassBreak] = Shatter("glass", 0.6f, 2600f, 8000f, rng);
            _clips[Sfx.Swing] = Whoosh("swing", 0.28f, rng);
            _clips[Sfx.Slice] = Shatter("slice", 0.45f, 3000f, 9000f, rng);
            _clips[Sfx.Deflect] = Ping("deflect", 0.35f, 3400f);
            _clips[Sfx.Thud] = Thud("thud", 0.18f, 140f, rng);
            _clips[Sfx.Grab] = Click("grab", 900f, 0.04f);
            _clips[Sfx.Chime] = Chime("chime", 0.9f);
            _clips[Sfx.Death] = Thud("death", 0.9f, 55f, rng);
            _clips[Sfx.Spawn] = Shatter("spawn", 0.5f, 900f, 2500f, rng, reverse: true);
            _clips[Sfx.Ricochet] = Ping("ricochet", 0.22f, 2200f);
            _clips[Sfx.Smg] = Gunshot("smg", 0.2f, 0.85f, 120f, rng);
            _clips[Sfx.Punch] = Thud("punch", 0.22f, 85f, rng);
            _clips[Sfx.Clash] = Ping("clash", 0.3f, 4600f);

            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject($"Voice {i}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = 0.8f;
                src.maxDistance = 25f;
                src.dopplerLevel = 0f;
                _voices.Add(src);
                _worldTime.Add(true);
                _basePitch.Add(1f);
            }

            SetupMixer();
            _hum = Loop("Still Hum", StillHum("hum", 4f, rng));
            _air = Loop("Motion Ambient", Air("air", 4f, rng));
        }

        void SetupMixer()
        {
            if (!Tuning.Sound.useMixer)
            {
                Debug.Log("[Stopklatka] Audio: mixer off in GameConfig, pitch set per sound.");
                return;
            }
            _mixer = Resources.Load<AudioMixer>(MixerResource);
            if (_mixer == null)
            {
                Debug.LogWarning($"[Stopklatka] Audio: no mixer at Resources/{MixerResource}, pitch set per sound. Run Stopklatka > 5. Create Audio Mixer.");
                return;
            }
            _world = FindGroup(WorldGroup);
            _ui = FindGroup(UiGroup);
            _ambient = FindGroup(AmbientGroup);
            _mixerPitch = _world != null && _mixer.GetFloat(WorldPitchParam, out _);
            Debug.Log($"[Stopklatka] Audio: mixer {_mixer.name}, world pitch {(_mixerPitch ? "in the mixer" : "per sound (" + WorldPitchParam + " not exposed)")}, " +
                      $"groups World {_world != null}, UI {_ui != null}, Ambient {_ambient != null}");
        }

        AudioMixerGroup FindGroup(string name)
        {
            var groups = _mixer.FindMatchingGroups(name);
            foreach (var g in groups) if (g.name == name) return g;
            return groups.Length > 0 ? groups[0] : null;
        }

        AudioSource Loop(string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = 0f;
            src.outputAudioMixerGroup = _ambient;
            src.Play();
            return src;
        }

        /// <param name="worldTime">If true the sound slows down with game time.</param>
        public void Play(Sfx sfx, Vector3 position, float volume = 1f, float pitch = 1f, bool worldTime = true)
        {
            if (!_clips.TryGetValue(sfx, out var clip)) return;
            var src = _voices[_next];
            _worldTime[_next] = worldTime;
            _basePitch[_next] = pitch;
            _next = (_next + 1) % _voices.Count;
            src.transform.position = position;
            src.clip = clip;
            src.volume = volume;
            // world sounds go through the World group (slowed by the mixer), the rest through UI
            src.outputAudioMixerGroup = worldTime ? _world : _ui;
            src.pitch = pitch * (worldTime && !_mixerPitch ? WorldPitch() : 1f);
            src.Play();
        }

        public void PlayUi(Sfx sfx, float volume = 0.8f)
        {
            var head = PlayerRig.Instance != null ? PlayerRig.Instance.Head.position : Vector3.zero;
            Play(sfx, head, volume, 1f, false);
        }

        /// <summary>Pitch of world sounds for the current time scale (GameConfig sound.slowPitch..fullPitch).</summary>
        static float WorldPitch()
        {
            var c = Tuning.Sound;
            float s = Time.timeScale;
            return s <= 0f ? 0.01f : Mathf.Max(0.01f, Mathf.Lerp(c.slowPitch, c.fullPitch, Mathf.Sqrt(s)));
        }

        void Update()
        {
            using var perfScope = PerfMarkers.Sound.Auto();
            float p = WorldPitch();
            if (_mixerPitch) _mixer.SetFloat(WorldPitchParam, p);
            else
            {
                for (int i = 0; i < _voices.Count; i++)
                    if (_worldTime[i] && _voices[i].isPlaying) _voices[i].pitch = Mathf.Max(0.01f, _basePitch[i] * p);
            }

            var c = Tuning.Sound;
            if (_mixer != null) _mixer.SetFloat(AmbientVolumeParam, c.ambientDb);   // false (ignored) if not exposed

            // ambient only while playing in motion-driven time; silent in the menu and while paused
            var tc = TimeController.Instance;
            float still = 0f, moving = 0f;
            if (tc != null && tc.CurrentMode == TimeController.Mode.MotionDriven)
            {
                still = 1f - Mathf.InverseLerp(Tuning.MinTimeScale, Tuning.MaxTimeScale, tc.Scale);
                moving = tc.Motion;
            }
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.05f, c.ambientResponse));
            _hum.volume = Mathf.Lerp(_hum.volume, still * c.stillHumVolume, k);
            _air.volume = Mathf.Lerp(_air.volume, moving * c.motionAmbientVolume, k);
            _air.pitch = Mathf.Lerp(0.9f, 1.15f, moving);
        }

        // --- synthesis -------------------------------------------------------------

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        static AudioClip Gunshot(string name, float seconds, float gain, float thumpHz, System.Random r)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float crack = Noise(r) * Mathf.Exp(-t * 38f);
                lp += (Noise(r) - lp) * 0.08f;                         // low rumble
                float body = lp * 3f * Mathf.Exp(-t * 9f);
                float thump = Mathf.Sin(2f * Mathf.PI * thumpHz * t * (1f - t)) * Mathf.Exp(-t * 14f);
                d[i] = Mathf.Clamp((crack * 0.7f + body * 0.5f + thump * 0.9f) * gain, -1f, 1f);
            }
            return Make(name, d);
        }

        static AudioClip Click(string name, float hz, float seconds)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                d[i] = Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-t * 90f) * 0.6f;
            }
            return Make(name, d);
        }

        static AudioClip Shatter(string name, float seconds, float lowHz, float highHz, System.Random r, bool reverse = false)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            const int pings = 18;
            for (int k = 0; k < pings; k++)
            {
                float hz = Mathf.Lerp(lowHz, highHz, (float)r.NextDouble());
                float start = (float)r.NextDouble() * seconds * 0.35f;
                float decay = 8f + (float)r.NextDouble() * 18f;
                float amp = 0.12f + (float)r.NextDouble() * 0.12f;
                int s0 = (int)(start * Rate);
                for (int i = s0; i < n; i++)
                {
                    float t = (i - s0) / (float)Rate;
                    d[i] += Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-t * decay) * amp;
                }
            }
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                d[i] = Mathf.Clamp(d[i] + Noise(r) * 0.35f * Mathf.Exp(-t * 20f), -1f, 1f);
            }
            if (reverse) System.Array.Reverse(d);
            return Make(name, d);
        }

        static AudioClip Whoosh(string name, float seconds, System.Random r)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Sin(Mathf.PI * t / seconds);
                lp += (Noise(r) - lp) * Mathf.Lerp(0.05f, 0.3f, env);
                d[i] = lp * env * 1.4f;
            }
            return Make(name, d);
        }

        static AudioClip Ping(string name, float seconds, float hz)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = hz * (1f - 0.25f * t / seconds);
                d[i] = (Mathf.Sin(2f * Mathf.PI * f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * f * 2.7f * t)) * Mathf.Exp(-t * 12f) * 0.45f;
            }
            return Make(name, d);
        }

        static AudioClip Thud(string name, float seconds, float hz, System.Random r)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                d[i] = (Mathf.Sin(2f * Mathf.PI * hz * t) * 0.9f + Noise(r) * 0.25f * Mathf.Exp(-t * 60f)) * Mathf.Exp(-t * (6f / seconds));
            }
            return Make(name, d);
        }

        static AudioClip Chime(string name, float seconds)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            float[] notes = { 880f, 1318.5f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float v = 0f;
                for (int k = 0; k < notes.Length; k++)
                {
                    float tk = t - k * 0.12f;
                    if (tk < 0f) continue;
                    v += Mathf.Sin(2f * Mathf.PI * notes[k] * tk) * Mathf.Exp(-tk * 4f) * 0.35f;
                }
                d[i] = v;
            }
            return Make(name, d);
        }

        /// <summary>Makes a seamless loop: the extra samples after [length] are crossfaded into the start.</summary>
        static float[] Seamless(float[] raw, int length)
        {
            int fade = raw.Length - length;
            var d = new float[length];
            System.Array.Copy(raw, d, length);
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                d[i] = raw[i] * t + raw[length + i] * (1f - t);
            }
            return d;
        }

        /// <summary>The low hum of stopped time: a deep rumble and two low tones.</summary>
        static AudioClip StillHum(string name, float seconds, System.Random r)
        {
            int n = (int)(seconds * Rate);
            var raw = new float[n + Rate / 2];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)Rate;
                lp += (Noise(r) - lp) * 0.02f;
                lp2 += (lp - lp2) * 0.05f;
                // whole cycles over the clip length (45 Hz and 67.5 Hz over 4 s), so the tones loop cleanly
                float hum = Mathf.Sin(2f * Mathf.PI * 45f * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * 67.5f * t) * 0.15f;
                raw[i] = Mathf.Clamp(hum + lp2 * 4f, -1f, 1f);
            }
            return Make(name, Seamless(raw, n));
        }

        /// <summary>A soft airy rush (band-limited noise) with one slow swell per loop.</summary>
        static AudioClip Air(string name, float seconds, System.Random r)
        {
            int n = (int)(seconds * Rate);
            var raw = new float[n + Rate / 2];
            float lp = 0f, slow = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)Rate;
                lp += (Noise(r) - lp) * 0.12f;      // low-passed noise...
                slow += (lp - slow) * 0.01f;        // ...minus its rumble
                float swell = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * t / seconds);
                raw[i] = Mathf.Clamp((lp - slow) * 1.6f * swell, -1f, 1f);
            }
            return Make(name, Seamless(raw, n));
        }
    }
}
