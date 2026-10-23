using UnityEngine;

namespace Stopklatka
{
    /// <summary>Physics layers. The builder names them in the Tag Manager; code uses the indices.</summary>
    public static class Layers
    {
        public const int Room = 6;        // walls, floor and furniture from the Space Setup scan
        public const int Enemy = 7;
        public const int Grabbable = 8;   // weapons and throwables
        public const int Player = 10;     // head and torso hitboxes (triggers)
        public const int Shard = 11;      // debris from shattered enemies and bottles
        public const int Nav = 12;        // geometry MRUK creates for baking the NavMesh

        public static readonly int RoomMask = 1 << Room;
        public static readonly int EnemyMask = 1 << Enemy;
        public static readonly int GrabbableMask = 1 << Grabbable;
        public static readonly int PlayerMask = 1 << Player;

        public static void ConfigureCollisions()
        {
            Physics.IgnoreLayerCollision(Shard, Shard);
            Physics.IgnoreLayerCollision(Shard, Enemy);
            Physics.IgnoreLayerCollision(Shard, Grabbable);
            Physics.IgnoreLayerCollision(Shard, Player);
            Physics.IgnoreLayerCollision(Grabbable, Player);
            Physics.IgnoreLayerCollision(Enemy, Player);
            Physics.IgnoreLayerCollision(Nav, Grabbable);
            Physics.IgnoreLayerCollision(Nav, Shard);
            Physics.IgnoreLayerCollision(Nav, Enemy);
        }
    }

    /// <summary>Gameplay numbers in one place, so play-testing changes stay simple.</summary>
    public static class Tuning
    {
        // Time: how much you have to move before the world runs at full speed
        public const float MinTimeScale = 0.03f;
        public const float HeadSpeedForFullTime = 1.2f;    // m/s
        public const float HeadTurnForFullTime = 2.8f;     // rad/s
        public const float HandSpeedForFullTime = 2.6f;    // m/s
        public const float MotionDeadzone = 0.06f;         // ignores tracking jitter (m/s)
        public const float BasePhysicsStep = 1f / 120f;

        // Player
        public const float GrabRadius = 0.13f;
        public const float ThrowBoost = 1.1f;
        public const float MaxThrowSpeed = 18f;

        // Bullets (game-time m/s)
        public const float PlayerBulletSpeed = 26f;
        public const float EnemyBulletSpeed = 6.5f;
        public const float BulletLifetime = 8f;

        // Enemies
        public const float EnemyWalkSpeed = 1.0f;
        public const float EnemyMinSpawnDistance = 2.5f;
        public const float EnemyMaxSpawnDistance = 9f;
        public const float EnemyPreferredRange = 4f;
        public const float EnemySightRange = 10f;
        public const float EnemyPunchRange = 0.85f;
        public const int MaxEnemiesAlive = 6;
        public const int MaxEnemiesPerWave = 12;

        public static int EnemiesInWave(int wave) => Mathf.Min(2 + wave, MaxEnemiesPerWave);
        public static int AliveCap(int wave) => Mathf.Min(2 + wave / 2, MaxEnemiesAlive);
        public static float ReactionTime(int wave) => Mathf.Lerp(1.3f, 0.55f, Mathf.Clamp01((wave - 1) / 8f));
        public static float FireInterval(int wave) => Mathf.Lerp(3.0f, 1.6f, Mathf.Clamp01((wave - 1) / 8f));
        public static float ArmedChance(int wave) => Mathf.Lerp(0.55f, 0.85f, Mathf.Clamp01((wave - 1) / 6f));
        public static float ShotgunChance(int wave) => wave < 3 ? 0f : 0.25f;
    }
}
