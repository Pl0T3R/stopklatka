using UnityEngine;

namespace Stopklatka
{
    /// <summary>A bottle: breaks on impact and knocks out whoever it hits.</summary>
    public class Throwable : Grabbable
    {
        protected override void Awake()
        {
            base.Awake();
            Breakable = true;
            StunSpeed = 2f;
        }

        public override void OnRelease(Vector3 velocity, Vector3 angularVelocity)
        {
            // a little extra spin reads better in slow motion
            base.OnRelease(velocity, angularVelocity + Random.onUnitSphere * Mathf.Min(velocity.magnitude, 8f));
        }
    }
}
