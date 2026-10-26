using UnityEngine;

namespace Stopklatka
{
    public enum GunKind { Pistol, Shotgun, Smg }

    /// <summary>
    /// Pistol (4 rounds), shotgun (2 shells, 6 pellets) or SMG (short bursts, 15 rounds). No reload:
    /// an empty gun clicks (sound plus a double haptic tick on every pull) and is still good for
    /// throwing. All numbers are in GameConfig > weapons; cooldowns and burst timing run in game time.
    /// </summary>
    public class Gun : Grabbable
    {
        public GunKind Kind;
        public Transform Muzzle;
        public int Ammo;

        float _nextShot;            // game time
        int _burstLeft;
        float _nextRound;           // game time
        ControllerHand _burstHand;

        static WeaponSettings W => GameConfig.Instance.weapons;

        public int MaxAmmo => Kind switch
        {
            GunKind.Shotgun => W.shotgunShells,
            GunKind.Smg => W.smgRounds,
            _ => W.pistolRounds,
        };

        int Pellets => Kind == GunKind.Shotgun ? Mathf.Max(1, W.shotgunPellets) : 1;

        float SpreadDegrees => Kind switch
        {
            GunKind.Shotgun => W.shotgunSpread,
            GunKind.Smg => W.smgSpread,
            _ => W.pistolSpread,
        };

        float Cooldown => Kind switch
        {
            GunKind.Shotgun => W.shotgunCooldown,
            GunKind.Smg => W.smgBurstCooldown,
            _ => W.pistolCooldown,
        };

        Sfx Sound => Kind switch
        {
            GunKind.Shotgun => Sfx.Shotgun,
            GunKind.Smg => Sfx.Smg,
            _ => Sfx.Pistol,
        };

        HapticPulse ShotHaptic => Kind switch
        {
            GunKind.Shotgun => GameConfig.Instance.haptics.shotgunShot,
            GunKind.Smg => GameConfig.Instance.haptics.smgRound,
            _ => GameConfig.Instance.haptics.pistolShot,
        };

        protected override void Awake()
        {
            base.Awake();
            Ammo = MaxAmmo;
        }

        /// <summary>A pooled gun coming back (<see cref="GunPool"/>): full, no burst, no cooldown.</summary>
        public override void ResetForReuse()
        {
            base.ResetForReuse();
            Ammo = MaxAmmo;
            _nextShot = 0f;
            _burstLeft = 0;
            _nextRound = 0f;
            _burstHand = null;
        }

        public override void OnTriggerDown(ControllerHand hand)
        {
            if (Ammo <= 0)
            {
                // every pull on an empty gun clicks, even right after the last shot
                var h = GameConfig.Instance.haptics;
                SoundBank.Instance?.Play(Sfx.Empty, Muzzle.position, 0.8f, 1f, false);
                hand.HapticTicks(h.emptyClick, h.emptyClickTicks, h.emptyClickGap);
                return;
            }
            if (_burstLeft > 0 || Time.time < _nextShot) return;

            if (Kind == GunKind.Smg)
            {
                // the burst continues in Update, one round every smgRoundInterval of game time
                _burstLeft = Mathf.Clamp(W.smgRoundsPerBurst, 1, Ammo);
                _burstHand = hand;
                _nextRound = Time.time;
                FireRound(hand);
            }
            else
            {
                FireRound(hand);
                _nextShot = Time.time + Cooldown;
            }
            TimeController.Instance?.ActionPulse();   // one pulse per shot or burst
        }

        protected override void Update()
        {
            base.Update();
            if (_burstLeft <= 0) return;
            if (Holder == null || Holder != _burstHand || Ammo <= 0)
            {
                EndBurst();   // dropped or thrown mid-burst
                return;
            }
            if (Time.time >= _nextRound) FireRound(_burstHand);
        }

        void FireRound(ControllerHand hand)
        {
            Ammo--;
            for (int i = 0; i < Pellets; i++)
                BulletSystem.Fire(Muzzle.position, Spread(Muzzle.forward, SpreadDegrees) * Tuning.PlayerBulletSpeed, BulletOwner.Player, null);
            Effects.MuzzleFlash(Muzzle.position, Muzzle.rotation);
            SoundBank.Instance?.Play(Sound, Muzzle.position, 1f, Random.Range(0.95f, 1.05f));
            hand.Haptic(ShotHaptic);

            if (Kind != GunKind.Smg) return;
            _burstLeft--;
            _nextRound += Mathf.Max(0.01f, W.smgRoundInterval);
            if (_burstLeft <= 0 || Ammo <= 0) EndBurst();
        }

        void EndBurst()
        {
            _burstLeft = 0;
            _burstHand = null;
            _nextShot = Time.time + Cooldown;
        }

        /// <summary>
        /// One enemy shot (a shotgun fires all its pellets). Enemies have unlimited ammo but slower
        /// bullets and a wobbly aim. SMG bursts are timed by <see cref="Enemy"/>, one call per round.
        /// </summary>
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
