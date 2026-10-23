using System.Collections.Generic;
using UnityEngine;

namespace Stopklatka
{
    public enum Sfx { Pistol, Shotgun, Empty, Shatter, GlassBreak, Swing, Slice, Deflect, Thud, Grab, Chime, Death, Spawn, Ricochet }

    /// <summary>
    /// All sounds are synthesized at startup (no audio files). 3D one-shots come from a pool of
    /// AudioSources whose pitch follows the time scale, so sounds slow down with the world.
    /// A quiet drone rises and falls with how fast time is moving.
    /// </summary>
    public class SoundBank : MonoBehaviour
    {
        public static SoundBank Instance { get; private set; }

        const int Rate = 44100;
        const int Voices = 24;

        readonly Dictionary<Sfx, AudioClip> _clips = new();
        readonly List<AudioSource> _voices = new();
        readonly List<bool> _worldTime = new();
        AudioSource _drone;
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
            }

            var droneGo = new GameObject("Drone");
            droneGo.transform.SetParent(transform, false);
            _drone = droneGo.AddComponent<AudioSource>();
            _drone.clip = Drone("drone", 4f, rng);
            _drone.loop = true;
            _drone.spatialBlend = 0f;
            _drone.volume = 0f;
            _drone.Play();
        }

        /// <param name="worldTime">If true the sound slows down with game time.</param>
        public void Play(Sfx sfx, Vector3 position, float volume = 1f, float pitch = 1f, bool worldTime = true)
        {
            if (!_clips.TryGetValue(sfx, out var clip)) return;
            var src = _voices[_next];
            _worldTime[_next] = worldTime;
            _next = (_next + 1) % _voices.Count;
            src.transform.position = position;
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch * (worldTime ? WorldPitch() : 1f);
            src.Play();
        }

        public void PlayUi(Sfx sfx, float volume = 0.8f)
        {
            var head = PlayerRig.Instance != null ? PlayerRig.Instance.Head.position : Vector3.zero;
            Play(sfx, head, volume, 1f, false);
        }

        static float WorldPitch()
        {
            float s = Time.timeScale;
            return s <= 0f ? 0.01f : Mathf.Lerp(0.35f, 1f, Mathf.Sqrt(s));
        }

        void Update()
        {
            float p = WorldPitch();
            for (int i = 0; i < _voices.Count; i++)
                if (_worldTime[i] && _voices[i].isPlaying) _voices[i].pitch = Mathf.Max(0.01f, p);

            var tc = TimeController.Instance;
            bool slowMode = tc != null && tc.CurrentMode == TimeController.Mode.MotionDriven;
            float targetVol = slowMode ? Mathf.Lerp(0.05f, 0.16f, tc.Scale) : 0f;
            _drone.volume = Mathf.MoveTowards(_drone.volume, targetVol, Time.unscaledDeltaTime * 0.5f);
            _drone.pitch = slowMode ? Mathf.Lerp(0.6f, 1.1f, tc.Scale) : 1f;
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

        static AudioClip Drone(string name, float seconds, System.Random r)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += (Noise(r) - lp) * 0.01f;
                // whole cycles over the clip length so the loop is seamless
                float hum = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.25f;
                d[i] = hum * 0.5f + lp * 2f;
            }
            return Make(name, d);
        }
    }
}
