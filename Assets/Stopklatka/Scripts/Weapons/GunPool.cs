using System.Collections.Generic;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// Reuses guns (GameConfig performance.poolEnemies, like the enemies that carry them). Guns that
    /// are cleared from the world (a level restarts, an enemy despawns with its gun) come back here
    /// instead of being destroyed; <see cref="Gun.ResetForReuse"/> gives them full ammo, no holder,
    /// no ignored collisions and visible renderers. Guns dropped in a fight stay in the world as
    /// pickups, as before.
    /// </summary>
    public static class GunPool
    {
        static readonly Stack<Gun>[] Free = { new(), new(), new() };   // by GunKind
        static Transform _root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            foreach (var s in Free) s.Clear();
            _root = null;
        }

        static bool Enabled => GameConfig.Instance.performance.poolEnemies;

        static Transform Root
        {
            get
            {
                if (_root == null) _root = new GameObject("Pool: Guns").transform;
                return _root;
            }
        }

        static Stack<Gun> StackFor(GunKind kind) => Free[Mathf.Clamp((int)kind, 0, Free.Length - 1)];

        /// <summary>A pooled gun at this pose, active and ready; null if there is none (or pooling is off).</summary>
        public static Gun Get(GunKind kind, Vector3 position, Quaternion rotation)
        {
            if (!Enabled) return null;
            var stack = StackFor(kind);
            Gun gun = null;
            while (stack.Count > 0 && gun == null) gun = stack.Pop();   // skip any destroyed elsewhere
            if (gun == null) return null;
            var t = gun.transform;
            t.SetParent(null, false);
            t.SetPositionAndRotation(position, rotation);
            gun.gameObject.SetActive(true);
            gun.ResetForReuse();
            return gun;
        }

        /// <summary>Takes a gun back. False when pooling is off or it is in a hand: the caller destroys it.</summary>
        public static bool Release(Gun gun)
        {
            if (!Enabled || gun == null || gun.Holder != null) return false;
            gun.ResetForReuse();
            gun.gameObject.SetActive(false);
            gun.transform.SetParent(Root, false);
            StackFor(gun.Kind).Push(gun);
            return true;
        }

        /// <summary>Builds guns now (while loading): perKind of each kind.</summary>
        public static void Prewarm(int perKind)
        {
            if (!Enabled) return;
            for (int k = 0; k < Free.Length; k++)
                for (int i = Free[k].Count; i < perKind; i++)
                {
                    var gun = Factory.BuildGun((GunKind)k, Vector3.zero, Quaternion.identity);
                    Release(gun);
                }
        }
    }
}
