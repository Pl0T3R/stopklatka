using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// Puts one enemy into the scanned space (used by the levels and by the endless waves): picks a
    /// spot through <see cref="RoomService"/> (door frames, out of view, distance limits) and a weapon
    /// from the given shares.
    /// </summary>
    public static class EnemySpawner
    {
        /// <param name="difficulty">1..9, sets reaction time and time between shots.</param>
        /// <param name="armedShare">Chance of a gun.</param>
        /// <param name="shotgunShare">Of the armed: chance of a shotgun.</param>
        /// <param name="smgShare">Of the armed: chance of an SMG (the rest get pistols).</param>
        public static bool TrySpawn(int difficulty, float armedShare, float shotgunShare, float smgShare)
        {
            var rig = PlayerRig.Instance;
            var rooms = RoomService.Instance;
            if (rig == null || rooms == null) return false;
            if (!rooms.TryFindEnemySpawn(rig.FloorPosition, rig.Head.forward, out var pos)) return false;

            var toPlayer = rig.FloorPosition - pos;
            toPlayer.y = 0f;
            var face = Quaternion.LookRotation(toPlayer.sqrMagnitude > 1e-4f ? toPlayer.normalized : Vector3.forward, Vector3.up);
            Factory.CreateEnemy(pos, face, difficulty, PickWeapon(armedShare, shotgunShare, smgShare));
            return true;
        }

        public static GunKind? PickWeapon(float armedShare, float shotgunShare, float smgShare)
        {
            if (Random.value >= armedShare) return null;
            float r = Random.value;
            if (r < shotgunShare) return GunKind.Shotgun;
            if (r < shotgunShare + smgShare) return GunKind.Smg;
            return GunKind.Pistol;
        }
    }
}
