using UnityEngine;

namespace Stopklatka
{
    public enum GunKind { Pistol, Shotgun }

    /// <summary>Pistol (4 rounds) or shotgun (2 shells, 6 pellets). Empty guns are still good for throwing.</summary>
    public class Gun : Grabbable
    {
        public GunKind Kind;
        public Transform Muzzle;
        public int Ammo;

        float _nextShot;    // game time

        public int MaxAmmo => Kind == GunKind.Shotgun ? 2 : 4;
        int Pellets => Kind == GunKind.Shotgun ? 6 : 1;
        float SpreadDegrees => Kind == GunKind.Shotgun ? 5.5f : 0.25f;
        float Cooldown => Kind == GunKind.Shotgun ? 0.9f : 0.3f;
        Sfx Sound => Kind == GunKind.Shotgun ? Sfx.Shotgun : Sfx.Pistol;

        protected override void Awake()
        {
            base.Awake();
            Ammo = MaxAmmo;
        }

        public override void OnTriggerDown(ControllerHand hand)
        {
            if (Time.time < _nextShot) return;
            if (Ammo <= 0)
            {
                SoundBank.Instance?.Play(Sfx.Empty, Muzzle.position, 0.7f, 1f, false);
                hand.Haptic(0.15f, 0.04f);
                return;
            }
            Ammo--;
            _nextShot = Time.time + Cooldown;
            for (int i = 0; i < Pellets; i++)
                BulletSystem.Fire(Muzzle.position, Spread(Muzzle.forward, SpreadDegrees) * Tuning.PlayerBulletSpeed, BulletOwner.Player, null);
            Effects.MuzzleFlash(Muzzle.position, Muzzle.rotation);
            SoundBank.Instance?.Play(Sound, Muzzle.position, 1f, Random.Range(0.95f, 1.05f));
            hand.Haptic(Kind == GunKind.Shotgun ? 1f : 0.75f, Kind == GunKind.Shotgun ? 0.18f : 0.1f);
            TimeController.Instance?.Kick(0.7f, 0.18f);
        }

        /// <summary>Enemies have unlimited ammo but slower bullets and a wobbly aim.</summary>
        public void FireAsEnemy(Enemy shooter, Vector3 target, float inaccuracyDegrees)
        {
            var dir = (target - Muzzle.position).normalized;
            for (int i = 0; i < Pellets; i++)
                BulletSystem.Fire(Muzzle.position, Spread(dir, SpreadDegrees + inaccuracyDegrees) * Tuning.EnemyBulletSpeed, BulletOwner.Enemy, shooter);
            Effects.MuzzleFlash(Muzzle.position, Quaternion.LookRotation(dir));
            SoundBank.Instance?.Play(Sound, Muzzle.position, 0.9f, Random.Range(0.85f, 0.95f));
        }

        static Vector3 Spread(Vector3 dir, float degrees)
        {
            if (degrees <= 0f) return dir;
            var r = Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
            var basis = Quaternion.LookRotation(dir);
            return (basis * new Vector3(r.x, r.y, 1f)).normalized;
        }
    }
}
