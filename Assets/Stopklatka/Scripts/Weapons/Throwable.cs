using UnityEngine;

namespace Stopklatka
{
    public enum ThrowableKind { Bottle, Mug, Ashtray, Block, Ball }

    /// <summary>
    /// Things that are only good for throwing. Bottles (dark glass), mugs and ashtrays (black) break
    /// into shards on a hard impact; blocks and balls just bounce. All of them stun the enemy they hit
    /// (kill only with GameConfig combat.killOnThrow).
    /// </summary>
    public class Throwable : Grabbable
    {
        public ThrowableKind Kind;

        /// <summary>Set by the factory right after AddComponent.</summary>
        public void Setup(ThrowableKind kind)
        {
            Kind = kind;
            Breakable = kind is ThrowableKind.Bottle or ThrowableKind.Mug or ThrowableKind.Ashtray;
        }

        public override void OnRelease(Vector3 velocity, Vector3 angularVelocity)
        {
            // a little extra spin reads better in slow motion
            base.OnRelease(velocity, angularVelocity + Random.onUnitSphere * Mathf.Min(velocity.magnitude, 8f));
        }

        protected override void Break(Vector3 point)
        {
            if (Kind == ThrowableKind.Bottle)
            {
                // dark glass: small sharp shards and a bright glass sound
                ShardSystem.Burst(transform.position, Mats.Get(Mats.DarkGlass), 14, Body.linearVelocity * 0.3f, 1.6f, 0.025f, 0.05f);
                SoundBank.Instance?.Play(Sfx.GlassBreak, point, 0.9f, Random.Range(0.9f, 1.1f));
                if (Holder != null) Holder.ForceDrop();
                Destroy(gameObject);
                return;
            }
            // ceramic: dark chunks, a duller sound than glass
            ShardSystem.Burst(transform.position, Mats.Get(Mats.Weapon), Kind == ThrowableKind.Ashtray ? 10 : 12,
                Body.linearVelocity * 0.3f, 1.3f, 0.02f, 0.045f);
            SoundBank.Instance?.Play(Sfx.GlassBreak, point, 0.9f, Random.Range(0.6f, 0.7f));
            if (Holder != null) Holder.ForceDrop();
            Destroy(gameObject);
        }
    }
}
