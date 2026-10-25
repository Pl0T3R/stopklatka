using System;
using UnityEngine;

namespace Stopklatka
{
    public enum PlayMode { School, Home }

    /// <summary>Numbers that change with the place you play in. Edited in GameConfig (school / home).</summary>
    [Serializable]
    public sealed class ModeProfile
    {
        public string Label;
        public float MinSpawnDistance, MaxSpawnDistance;
        public float PreferredRange;
        public float WalkSpeed;
        public float EnemyBulletSpeed;
        public float ReactionMultiplier;
        [Tooltip("How often enemies appear in your room instead of walking in from another.")]
        [Range(0f, 1f)] public float PlayerRoomChance;
        public int MaxAlive, MaxPerWave;
        [Tooltip("Extra enemies per wave.")]
        public float WaveGrowth;

        public static ModeProfile CreateSchool() => new()
        {
            Label = "SZKOŁA",
            MinSpawnDistance = 2.5f, MaxSpawnDistance = 9f,
            PreferredRange = 4f, WalkSpeed = 1.0f, EnemyBulletSpeed = 6.5f,
            ReactionMultiplier = 1f, PlayerRoomChance = 0.7f,
            MaxAlive = 6, MaxPerWave = 12, WaveGrowth = 1f,
        };

        // A whole house (~150 m2, several rooms, maybe floors): closer, fewer at once, slower bullets.
        public static ModeProfile CreateHome() => new()
        {
            Label = "DOM",
            MinSpawnDistance = 1.8f, MaxSpawnDistance = 7f,
            PreferredRange = 3f, WalkSpeed = 0.9f, EnemyBulletSpeed = 5.5f,
            ReactionMultiplier = 1.15f, PlayerRoomChance = 0.5f,
            MaxAlive = 4, MaxPerWave = 10, WaveGrowth = 0.75f,
        };
    }

    /// <summary>School or home. Picked in the menu and remembered between sessions.</summary>
    public static class GameMode
    {
        const string Key = "stopklatka.mode";
        static PlayMode? _current;

        public static PlayMode Current
        {
            get
            {
                _current ??= (PlayMode)Mathf.Clamp(PlayerPrefs.GetInt(Key, (int)PlayMode.School), 0, 1);
                return _current.Value;
            }
        }

        public static ModeProfile Profile => Current == PlayMode.Home ? GameConfig.Instance.home : GameConfig.Instance.school;

        public static void Toggle() => Set(Current == PlayMode.School ? PlayMode.Home : PlayMode.School);

        public static void Set(PlayMode mode)
        {
            _current = mode;
            PlayerPrefs.SetInt(Key, (int)mode);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _current = null;
    }
}
