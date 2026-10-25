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
            // room walls are thin one-sided meshes: hit them from either side
            Physics.queriesHitBackfaces = true;
        }
    }

    /// <summary>
    /// Shortcuts to the gameplay numbers. The values themselves live in the GameConfig asset
    /// (see GameConfig.cs), so play-testing changes happen in the Inspector, not in code.
    /// </summary>
    public static class Tuning
    {
        static GameConfig C => GameConfig.Instance;

        // Time: how much you have to move before the world runs at full speed (see TimeController)
        public static float MinTimeScale => C.time.minTimeScale;
        public static float MaxTimeScale => C.time.maxTimeScale;
        public static float HeadSpeedForFullTime => C.time.headSpeedForFullTime;
        public static float HeadTurnForFullTime => C.time.headTurnForFullTime;
        public static float HandSpeedForFullTime => C.time.handSpeedForFullTime;
        public static float MotionDeadzone => C.time.motionDeadzone;

        /// <summary>Physics step at full speed: one step per displayed frame.</summary>
        public static float BasePhysicsStep
        {
            get
            {
                var perf = PerformanceManager.Instance;
                float hz = perf != null && perf.RefreshRate > 1f ? perf.RefreshRate : C.performance.targetRefreshRate;
                return 1f / Mathf.Clamp(hz, 60f, 144f);
            }
        }

        // Player
        public static float EnemyStreakLength => C.visuals.enemyStreakLength;
        public static float PlayerStreakLength => C.visuals.playerStreakLength;
        public static SoundSettings Sound => C.sound;

        public static float GrabRadius => C.player.grabRadius;
        public static float ThrowBoost => C.player.throwBoost;
        public static float MaxThrowSpeed => C.player.maxThrowSpeed;
        public static float CatchMinSpeed => C.player.catchMinSpeed;
        public static float CatchRadiusPerSpeed => C.player.catchRadiusPerSpeed;
        public static float CatchRadiusBonusMax => C.player.catchRadiusBonusMax;

        // Bullets (game-time m/s)
        public static float PlayerBulletSpeed => C.combat.playerBulletSpeed;
        public static float EnemyBulletSpeed => GameMode.Profile.EnemyBulletSpeed;
        public static float BulletLifetime => C.combat.bulletLifetime;
        public static float BulletInterceptRadius => C.combat.bulletInterceptRadius;
        public static float KatanaCutRadius => C.combat.katanaCutRadius;

        // Thrown objects (game-time impact speeds)
        public static bool KillOnThrow => C.combat.killOnThrow;
        public static float ThrowStunSpeed => C.combat.throwStunSpeed;
        public static float ThrowKillSpeed => C.combat.throwKillSpeed;
        public static bool ThrownBladeKills => C.combat.thrownBladeKills;
        public static float ThrownBladeKillSpeed => C.combat.thrownBladeKillSpeed;
        public static float BreakSpeed => C.combat.breakSpeed;

        // Enemies (school and home differ, see GameMode.cs)
        public static float EnemyWalkSpeed => GameMode.Profile.WalkSpeed;
        public static float EnemyMinSpawnDistance => GameMode.Profile.MinSpawnDistance;
        public static float EnemyMaxSpawnDistance => GameMode.Profile.MaxSpawnDistance;
        public static float EnemyPreferredRange => GameMode.Profile.PreferredRange;
        public static float EnemySightRange => C.enemies.sightRange;
        public static float EnemyPunchRange => C.enemies.punchRange;

        public static int EnemiesInWave(int wave) =>
            Mathf.Min(2 + Mathf.FloorToInt(wave * GameMode.Profile.WaveGrowth), GameMode.Profile.MaxPerWave);
        public static int AliveCap(int wave) => Mathf.Min(2 + wave / 2, GameMode.Profile.MaxAlive);
        public static float ReactionTime(int wave) =>
            Mathf.Lerp(C.enemies.reactionFirstWave, C.enemies.reactionLateWave, Mathf.Clamp01((wave - 1) / 8f)) * GameMode.Profile.ReactionMultiplier;
        public static float FireInterval(int wave) =>
            Mathf.Lerp(C.enemies.fireIntervalFirstWave, C.enemies.fireIntervalLateWave, Mathf.Clamp01((wave - 1) / 8f));
        public static float ArmedChance(int wave) =>
            Mathf.Lerp(C.enemies.armedFirstWave, C.enemies.armedLateWave, Mathf.Clamp01((wave - 1) / 6f));
        public static float ShotgunChance(int wave) => wave < C.enemies.shotgunFromWave ? 0f : C.enemies.shotgunShare;
        public static float SmgChance(int wave) => wave < C.enemies.smgFromWave ? 0f : C.enemies.smgShare;

        // Enemy aiming, bursts, punches and guard (game seconds)
        public static float AimTellSeconds => C.enemies.aimTellSeconds;
        public static float AimLockAngle => C.enemies.aimLockAngle;
        public static float AimInaccuracy => C.enemies.aimInaccuracy;
        public static int EnemySmgBurstRounds => Mathf.Max(1, C.enemies.smgBurstRounds);
        public static float EnemySmgBurstInterval => Mathf.Max(0.02f, C.enemies.smgBurstInterval);
        public static float EnemySmgBurstSpread => C.enemies.smgBurstSpread;
        public static float PunchWindupSeconds => Mathf.Max(0.05f, C.enemies.punchWindupSeconds);
        public static float PunchStrikeSeconds => Mathf.Max(0.02f, C.enemies.punchStrikeSeconds);
        public static float PunchRecoverSeconds => Mathf.Max(0.05f, C.enemies.punchRecoverSeconds);
        public static bool DefensivePose => C.enemies.defensivePose;
        public static float GuardSeconds => C.enemies.guardSeconds;
        public static float GuardBulletDistance => C.enemies.guardBulletDistance;
        public static float GuardNeighbourDistance => C.enemies.guardNeighbourDistance;

        // Spawn points
        public static float DoorSpawnChance => C.enemies.doorSpawnChance;
        public static float DoorSpawnOffset => C.enemies.doorSpawnOffset;
        public static float OutOfViewChance => C.enemies.outOfViewChance;
        public static float OutOfViewAngle => C.enemies.outOfViewAngle;

        // Enemy death (see ShatterSettings)
        public static ShatterSettings Shatter => C.shatter;

        // Levels (see LevelSettings)
        public static LevelSettings Levels => C.levels;
    }
}
