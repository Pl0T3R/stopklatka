using System;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// One hit and you're down. Bullets and punches report hits here (head and torso hitboxes come
    /// from PlayerRig, placed on the headset). Whoever runs the scene decides what a hit means:
    /// GameManager restarts the level, the time test scene just counts it.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class PlayerHealth : MonoBehaviour
    {
        public static PlayerHealth Instance { get; private set; }

        /// <summary>Raised with the point that hit you.</summary>
        public event Action<Vector3> Hit;

        /// <summary>Ignore hits (menu, death animation).</summary>
        public bool Invulnerable { get; set; }

        void Awake() => Instance = this;

        public void TakeHit(Vector3 point)
        {
            if (Invulnerable || !isActiveAndEnabled) return;
            Hit?.Invoke(point);
        }
    }
}
