using Unity.Profiling;

namespace Stopklatka
{
    /// <summary>
    /// Profiler markers around the game's main systems, so a PC session can read them in the Unity
    /// Profiler (connected to the headset) or a Perfetto trace, and the debug readout can show them.
    /// They cost nothing in a non-development build (Unity compiles Begin/End out).
    /// Use: <c>using (PerfMarkers.Bullets.Auto()) { ... }</c>.
    /// </summary>
    public static class PerfMarkers
    {
        public static readonly ProfilerMarker TimeSystem = new(ProfilerCategory.Scripts, "Stopklatka.Time");
        public static readonly ProfilerMarker Hands = new(ProfilerCategory.Scripts, "Stopklatka.Hands");
        public static readonly ProfilerMarker Fist = new(ProfilerCategory.Scripts, "Stopklatka.Fist");
        public static readonly ProfilerMarker Katana = new(ProfilerCategory.Scripts, "Stopklatka.Katana");
        public static readonly ProfilerMarker Bullets = new(ProfilerCategory.Scripts, "Stopklatka.Bullets");
        public static readonly ProfilerMarker Enemies = new(ProfilerCategory.Scripts, "Stopklatka.Enemies");
        public static readonly ProfilerMarker Debris = new(ProfilerCategory.Scripts, "Stopklatka.Debris");
        public static readonly ProfilerMarker Shards = new(ProfilerCategory.Scripts, "Stopklatka.Shards");
        public static readonly ProfilerMarker Cracks = new(ProfilerCategory.Scripts, "Stopklatka.Cracks");
        public static readonly ProfilerMarker Shatter = new(ProfilerCategory.Scripts, "Stopklatka.Shatter");
        public static readonly ProfilerMarker Sound = new(ProfilerCategory.Audio, "Stopklatka.Sound");
        public static readonly ProfilerMarker Hud = new(ProfilerCategory.Scripts, "Stopklatka.Hud");
        public static readonly ProfilerMarker Game = new(ProfilerCategory.Scripts, "Stopklatka.Game");
        public static readonly ProfilerMarker Levels = new(ProfilerCategory.Scripts, "Stopklatka.Levels");
        public static readonly ProfilerMarker Props = new(ProfilerCategory.Scripts, "Stopklatka.Props");
        public static readonly ProfilerMarker Room = new(ProfilerCategory.Scripts, "Stopklatka.Room");
        public static readonly ProfilerMarker Spawn = new(ProfilerCategory.Scripts, "Stopklatka.Spawn");
        public static readonly ProfilerMarker Draw = new(ProfilerCategory.Render, "Stopklatka.InstancedDraw");

        /// <summary>The markers the debug readout lists, with short labels.</summary>
        public static readonly (string Label, string Name, ProfilerCategory Category)[] Readout =
        {
            ("czas", "Stopklatka.Time", ProfilerCategory.Scripts),
            ("ręce", "Stopklatka.Hands", ProfilerCategory.Scripts),
            ("wrogowie", "Stopklatka.Enemies", ProfilerCategory.Scripts),
            ("pociski", "Stopklatka.Bullets", ProfilerCategory.Scripts),
            ("rozpad", "Stopklatka.Shatter", ProfilerCategory.Scripts),
            ("odłamki", "Stopklatka.Shards", ProfilerCategory.Scripts),
            ("kawałki", "Stopklatka.Debris", ProfilerCategory.Scripts),
            ("spawn", "Stopklatka.Spawn", ProfilerCategory.Scripts),
            ("rysowanie", "Stopklatka.InstancedDraw", ProfilerCategory.Render),
        };
    }
}
