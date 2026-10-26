using System.Collections.Generic;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// Reuses enemies (GameConfig performance.poolEnemies). Building one makes about 25 GameObjects,
    /// two colliders, a kinematic body and a NavMeshAgent, which stalls the frame it spawns in; a dead
    /// enemy is switched off and kept here instead of destroyed, and <see cref="Enemy.Init"/> resets
    /// it completely (state, aim, guard, punch, pose, reveal, colliders, agent). A few are built while
    /// the scan loads (<see cref="Prewarm"/>), so the first wave builds nothing.
    /// </summary>
    public static class EnemyPool
    {
        static readonly Stack<Enemy> Free = new();
        static Transform _root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Free.Clear();
            _root = null;
        }

        static bool Enabled => GameConfig.Instance.performance.poolEnemies;

        static Transform Root
        {
            get
            {
                if (_root == null) _root = new GameObject("Pool: Enemies").transform;
                return _root;
            }
        }

        /// <summary>An enemy at this pose, active but not started (call Enemy.Init next).</summary>
        public static Enemy Get(Vector3 position, Quaternion rotation)
        {
            Enemy enemy = null;
            while (Free.Count > 0 && enemy == null) enemy = Free.Pop();   // skip any destroyed by a scene change
            if (enemy == null) enemy = Factory.BuildEnemy(position, rotation);
            var t = enemy.transform;
            t.SetParent(null, false);
            t.SetPositionAndRotation(position, rotation);
            enemy.gameObject.SetActive(true);
            return enemy;
        }

        /// <summary>Takes a dead enemy back. False when pooling is off: the caller destroys it.</summary>
        public static bool Release(Enemy enemy)
        {
            if (!Enabled || enemy == null) return false;
            // glowing cracks drawn on its parts must not show up on its next life
            Cracks.ReleaseOn(enemy.transform);
            enemy.gameObject.SetActive(false);
            enemy.transform.SetParent(Root, false);
            Free.Push(enemy);
            return true;
        }

        /// <summary>Builds enemies now (while loading) so spawning in a fight only reuses them.</summary>
        public static void Prewarm(int count)
        {
            if (!Enabled) return;
            for (int i = Free.Count; i < count; i++)
            {
                var enemy = Factory.BuildEnemy(Vector3.zero, Quaternion.identity);
                enemy.transform.SetParent(Root, false);
                Free.Push(enemy);
            }
        }
    }
}
