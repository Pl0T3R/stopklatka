using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Stopklatka
{
    /// <summary>
    /// A red crystal enemy (GAME_SPEC 4). Armed ones approach to a comfortable range and shoot when
    /// they can see you (real walls block their view and their bullets): the arm visibly rises while
    /// they take aim, then a short tell (they stop, the arm locks, the muzzle glints) before every
    /// shot, with a slight spread so you can dodge. SMG enemies fire short bursts at where your head
    /// was when the burst started. Unarmed ones walk up and punch after a visible wind-up.
    /// A bullet passing close or a neighbour dying makes them guard for a moment.
    /// Everything runs in game time, so they freeze when you stand still. Walking speeds stay at the
    /// per-mode values (no running enemies in a real room).
    /// One hit kills: bullets and throws shatter them, the katana cuts them in half, the fist cracks
    /// them open first (see <see cref="Shatter"/> and <see cref="Cracks"/>).
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        public static readonly List<Enemy> Alive = new();

        public enum KillKind { Bullet, Punch, Blade, Thrown }

        enum State { Appearing, Moving, Punching, Stunned, Dying, Dead }
        /// <summary>An armed enemy's shot cycle.</summary>
        enum Aim { Idle, Raising, Tell, Firing }

        public Gun Gun { get; private set; }

        NavMeshAgent _agent;
        EnemyBody _body;
        Collider[] _colliders;
        State _state;
        Aim _aim;
        float _stateTime, _aimTime, _reaction, _reactionTime, _fireInterval, _nextFire, _repathAt, _nextRound;
        float _guardUntil = -1f;
        int _burstLeft;
        bool _useAgent, _revealing, _punchLanded;
        Vector3 _lastPos, _velocity, _manualTarget, _burstTarget, _deathPoint, _deathDir;
        Renderer[] _gunRenderers;

        const float EyeHeight = 1.62f;
        const float ChestHeight = 1.3f;
        const float AppearSeconds = 0.9f;
        const float RevealHeight = 1.9f;

        /// <summary>Height of the floor the enemy stands on (where its debris lands).</summary>
        float FloorY => transform.position.y;

        /// <summary>The body and agent this enemy drives (once, when it is built; a pooled enemy keeps them).</summary>
        public void Setup(EnemyBody body, NavMeshAgent agent)
        {
            _body = body;
            _agent = agent;
            _colliders = GetComponents<Collider>();
        }

        /// <summary>
        /// Starts the enemy where it stands: forming from the floor up, then walking, aiming or punching.
        /// Resets everything, so a pooled enemy (see <see cref="EnemyPool"/>) starts exactly like a new one.
        /// </summary>
        public void Init(Gun gun, int wave, int agentTypeId, bool navReady)
        {
            // a fresh start (pooled enemies come back with whatever their last life left)
            _aim = Aim.Idle;
            _aimTime = 0f;
            _repathAt = 0f;
            _nextRound = 0f;
            _guardUntil = -1f;
            _burstLeft = 0;
            _punchLanded = false;
            _revealing = false;
            _velocity = Vector3.zero;
            _manualTarget = _burstTarget = _deathPoint = _deathDir = Vector3.zero;
            _gunRenderers = null;
            Gun = null;
            _body.ResetPose();
            foreach (var c in _colliders) if (c != null) c.enabled = true;

            _reactionTime = Tuning.ReactionTime(wave) * Random.Range(0.85f, 1.25f);
            _fireInterval = Tuning.FireInterval(wave);
            _reaction = _reactionTime;
            _nextFire = Time.time + 1.5f;
            _body.AimBlend = 0f;
            if (gun != null)
            {
                Gun = gun;
                gun.AttachToEnemy(this, _body.HandSocketR);
                _gunRenderers = gun.GetComponentsInChildren<Renderer>();
                SetGunVisible(false);
            }

            _useAgent = false;
            if (navReady)
            {
                _agent.agentTypeID = agentTypeId;
                var filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas };
                if (NavMesh.SamplePosition(transform.position, out var hit, 0.8f, filter))
                {
                    // move onto the NavMesh before enabling, so the agent doesn't complain
                    transform.position = hit.position;
                    _agent.enabled = true;
                    _useAgent = _agent.Warp(hit.position) && _agent.isOnNavMesh;
                    if (_useAgent)
                    {
                        _agent.ResetPath();
                        _agent.isStopped = false;
                    }
                }
            }
            if (!_useAgent) _agent.enabled = false;

            _lastPos = transform.position;
            _state = State.Appearing;
            _stateTime = 0f;
            _body.Appear = 0f;
            _body.BeginReveal(Mats.Get(Mats.EnemyReveal), transform.position.y - 0.02f);
            _revealing = true;
            Effects.SpawnRing(transform.position, AppearSeconds * 1.3f);
            Alive.Add(this);
            SoundBank.Instance?.Play(Sfx.Spawn, transform.position + Vector3.up, 0.8f);
        }

        void OnDestroy() => Alive.Remove(this);

        // a pooled enemy is switched off instead of destroyed
        void OnDisable() => Alive.Remove(this);

        void Update()
        {
            using var perfScope = PerfMarkers.Enemies.Auto();
            float dt = Time.deltaTime;
            if (dt <= 0f || _state == State.Dead) return;
            _stateTime += dt;

            if (_state == State.Dying)
            {
                // cracked by a punch: trembles while the cracks spread, then shatters
                _body.Wobble = 1f;
                _body.Animate(dt, Vector3.zero);
                if (_stateTime >= Tuning.Shatter.crackSeconds)
                {
                    _state = State.Dead;
                    Shatter.Break(_body, _deathPoint, _deathDir, FloorY);
                    Retire();
                }
                return;
            }

            var rig = PlayerRig.Instance;
            var head = rig.Head.position;
            var playerFloor = rig.FloorPosition;
            var toPlayer = Vector3.ProjectOnPlane(head - transform.position, Vector3.up);
            float dist = toPlayer.magnitude;
            bool guarding = Time.time < _guardUntil;
            _body.Guard = Mathf.MoveTowards(_body.Guard, guarding ? 1f : 0f, dt / 0.15f);

            switch (_state)
            {
                case State.Appearing:
                    _body.Appear = Mathf.Clamp01(_stateTime / AppearSeconds);
                    UpdateReveal();
                    FaceToward(toPlayer, dt, 4f);
                    if (_stateTime >= AppearSeconds) { EndReveal(); Enter(State.Moving); }
                    break;

                case State.Moving:
                    bool planted = guarding || _aim == Aim.Tell || _aim == Aim.Firing;
                    UpdateMovement(playerFloor, toPlayer, dist, dt, planted);
                    if (Gun != null) UpdateShooting(head, dist, dt, guarding);
                    else if (!guarding && dist < Tuning.EnemyPunchRange) { Enter(State.Punching); _punchLanded = false; }
                    break;

                case State.Punching:
                    UpdatePunch(head, toPlayer, dist, dt);
                    break;

                case State.Stunned:
                    Stop();
                    _body.Wobble = Mathf.Clamp01(1f - _stateTime / 1.4f);
                    if (_stateTime >= 1.4f) { _body.Wobble = 0f; Enter(State.Moving); }
                    break;
            }

            _velocity = (transform.position - _lastPos) / dt;
            _lastPos = transform.position;
            _body.Animate(dt, _velocity);
        }

        void UpdateReveal()
        {
            // materializes from the feet up; the weapon pops in once the edge reaches the hand
            float y = transform.position.y - 0.02f + Mathf.SmoothStep(0f, 1f, _body.Appear) * RevealHeight;
            _body.SetReveal(y);
            if (Gun != null && y > Gun.transform.position.y) SetGunVisible(true);
        }

        void EndReveal()
        {
            if (!_revealing) return;
            _revealing = false;
            _body.Appear = 1f;
            _body.EndReveal(Mats.Get(Mats.Enemy));
            SetGunVisible(true);
        }

        void SetGunVisible(bool visible)
        {
            if (_gunRenderers == null) return;
            foreach (var r in _gunRenderers) if (r != null) r.enabled = visible;
        }

        void Enter(State s)
        {
            _state = s;
            _stateTime = 0f;
        }

        // --- movement -----------------------------------------------------------------

        void UpdateMovement(Vector3 playerFloor, Vector3 toPlayer, float dist, float dt, bool planted)
        {
            if (planted)
            {
                // taking the shot or guarding: stand still, keep facing you
                Stop();
                _repathAt = 0f;
                FaceToward(toPlayer, dt, 8f);
                return;
            }

            Vector3 target;
            if (Gun != null)
            {
                // hold a comfortable range instead of rushing in
                var away = dist > 0.01f ? -toPlayer / dist : transform.forward;
                if (dist > Tuning.EnemyPreferredRange + 1.2f || dist < Tuning.EnemyPreferredRange - 1.6f)
                    target = playerFloor + away * Tuning.EnemyPreferredRange;
                else
                    target = transform.position;
            }
            else target = playerFloor;

            if (_useAgent)
            {
                if (Time.time >= _repathAt)
                {
                    _repathAt = Time.time + 0.4f;
                    _agent.speed = Gun != null ? Tuning.EnemyWalkSpeed : Tuning.EnemyWalkSpeed * 1.15f;
                    _agent.isStopped = false;
                    _agent.SetDestination(target);
                }
            }
            else
            {
                // No NavMesh (no room scan): walk straight, stop at walls.
                _manualTarget = target;
                var step = Vector3.ProjectOnPlane(_manualTarget - transform.position, Vector3.up);
                if (step.magnitude > 0.15f)
                {
                    var move = step.normalized * Tuning.EnemyWalkSpeed * dt;
                    if (!Physics.Raycast(transform.position + Vector3.up * 0.5f, move.normalized, 0.35f, Layers.RoomMask))
                        transform.position += move;
                }
            }

            bool seesPlayer = Gun != null && _aim != Aim.Idle;
            var look = seesPlayer || dist < 3f ? toPlayer : Vector3.ProjectOnPlane(_velocity, Vector3.up);
            FaceToward(look.sqrMagnitude > 1e-4f ? look : toPlayer, dt, 6f);
        }

        // --- shooting ------------------------------------------------------------------

        /// <summary>
        /// See you -> the arm rises over the reaction time -> it lines up -> tell (stop, glint) ->
        /// shot or burst -> wait for the next shot (each one gets its own tell).
        /// </summary>
        void UpdateShooting(Vector3 head, float dist, float dt, bool guarding)
        {
            if (guarding)
            {
                if (_aim == Aim.Tell || _aim == Aim.Firing) LoseAim();
                _body.AimBlend = Mathf.MoveTowards(_body.AimBlend, 0f, dt * 3f);
                return;
            }
            if (_aim == Aim.Firing)
            {
                UpdateBurst();
                return;
            }

            var eye = transform.position + Vector3.up * EyeHeight;
            bool canSee = dist < Tuning.EnemySightRange &&
                          !Physics.Linecast(eye, head, Layers.RoomMask, QueryTriggerInteraction.Ignore);
            if (!canSee)
            {
                _aim = Aim.Idle;
                _reaction = _reactionTime;
                _body.AimTarget = null;
                _body.AimBlend = 0f;
                return;
            }

            _body.AimTarget = head;
            if (_aim == Aim.Idle) _aim = Aim.Raising;

            if (_aim == Aim.Raising)
            {
                _reaction -= dt;
                _body.AimBlend = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(_reaction / Mathf.Max(0.01f, _reactionTime)));
                if (_reaction > 0f || Time.time < _nextFire) return;
                if (Vector3.Angle(Gun.Muzzle.forward, head - Gun.Muzzle.position) > Tuning.AimLockAngle) return;
                // the tell: a glint and a click, then the enemy holds still for a moment
                _aim = Aim.Tell;
                _aimTime = 0f;
                Effects.AimGlint(Gun.Muzzle.position, Gun.Muzzle.forward);
                SoundBank.Instance?.Play(Sfx.Empty, Gun.Muzzle.position, 0.5f, 0.7f);
                return;
            }

            // Tell
            _body.AimBlend = 1f;
            _aimTime += dt;
            if (_aimTime < Tuning.AimTellSeconds) return;
            if (Gun.Kind == GunKind.Smg)
            {
                _aim = Aim.Firing;
                _burstLeft = Tuning.EnemySmgBurstRounds;
                _burstTarget = head;
                _nextRound = Time.time;
                UpdateBurst();
            }
            else
            {
                Gun.FireAsEnemy(this, head + Random.insideUnitSphere * 0.05f, Tuning.AimInaccuracy);
                EndShot();
            }
        }

        /// <summary>One round of an SMG burst per interval, all at where your head was when it started.</summary>
        void UpdateBurst()
        {
            _body.AimTarget = _burstTarget;
            _body.AimBlend = 1f;
            if (_burstLeft > 0 && Time.time >= _nextRound)
            {
                Gun.FireAsEnemy(this, _burstTarget, Tuning.AimInaccuracy + Tuning.EnemySmgBurstSpread);
                _burstLeft--;
                _nextRound += Tuning.EnemySmgBurstInterval;
            }
            if (_burstLeft <= 0) EndShot();
        }

        void EndShot()
        {
            _aim = Aim.Raising;   // the arm stays up; the next shot needs its own tell
            _burstLeft = 0;
            _nextFire = Time.time + _fireInterval * Random.Range(0.8f, 1.25f);
        }

        /// <summary>Flinched mid-shot: the arm drops a little and the aim takes a while again.</summary>
        void LoseAim()
        {
            _aim = Aim.Raising;
            _burstLeft = 0;
            _reaction = Mathf.Max(_reaction, _reactionTime * 0.5f);
        }

        // --- punching ------------------------------------------------------------------

        void UpdatePunch(Vector3 head, Vector3 toPlayer, float dist, float dt)
        {
            Stop();
            FaceToward(toPlayer, dt, 10f);
            _body.PunchTarget = head;
            float w = Tuning.PunchWindupSeconds, s = Tuning.PunchStrikeSeconds, r = Tuning.PunchRecoverSeconds;
            float t = _stateTime;
            if (t < w)
            {
                _body.Windup = Mathf.SmoothStep(0f, 1f, t / w);
                _body.Punch = 0f;
            }
            else if (t < w + s)
            {
                float k = (t - w) / s;
                _body.Windup = 1f - k;
                _body.Punch = k;
            }
            else
            {
                _body.Windup = 0f;
                _body.Punch = 1f - Mathf.SmoothStep(0f, 1f, (t - w - s) / r);
            }

            if (!_punchLanded && t >= w + s)
            {
                _punchLanded = true;
                if (dist < Tuning.EnemyPunchRange + 0.2f) PlayerHealth.Instance?.TakeHit(head);
            }
            if (t >= w + s + r)
            {
                _body.Punch = 0f;
                _body.Windup = 0f;
                Enter(State.Moving);
            }
        }

        // --- helpers -------------------------------------------------------------------

        void FaceToward(Vector3 dir, float dt, float rate)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            var target = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-rate * dt));
        }

        void Stop()
        {
            if (_useAgent && _agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
        }

        void DropGun(Vector3 velocity)
        {
            if (Gun == null) return;
            SetGunVisible(true);
            _gunRenderers = null;
            // the weapon starts inside this body: don't let it get shoved out sideways, so it flies up and can be caught
            foreach (var c in _colliders) Gun.IgnoreCollisionsBriefly(c);
            Gun.DropFromEnemy(velocity);
            Gun = null;
        }

        // --- reactions -----------------------------------------------------------------

        /// <summary>A short guard pose (GameConfig enemies.defensivePose). Ignored mid-punch, while stunned or forming.</summary>
        public void Flinch()
        {
            if (!Tuning.DefensivePose || _state != State.Moving) return;
            _guardUntil = Time.time + Tuning.GuardSeconds;
            if (_aim == Aim.Tell || _aim == Aim.Firing) LoseAim();
        }

        /// <summary>Your bullet moved from a to b this frame: enemies it passed close to flinch.</summary>
        public static void NotifyBulletPass(Vector3 a, Vector3 b)
        {
            if (Alive.Count == 0 || !Tuning.DefensivePose) return;
            float r = Tuning.GuardBulletDistance;
            float r2 = r * r;
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            for (int i = 0; i < Alive.Count; i++)
            {
                var e = Alive[i];
                if (e._state != State.Moving) continue;
                var chest = e.transform.position + Vector3.up * ChestHeight;
                float t = len2 > 1e-10f ? Mathf.Clamp01(Vector3.Dot(chest - a, ab) / len2) : 0f;
                if ((a + ab * t - chest).sqrMagnitude <= r2) e.Flinch();
            }
        }

        void AlarmNeighbours()
        {
            if (!Tuning.DefensivePose) return;
            float r2 = Tuning.GuardNeighbourDistance * Tuning.GuardNeighbourDistance;
            for (int i = 0; i < Alive.Count; i++)
                if ((Alive[i].transform.position - transform.position).sqrMagnitude <= r2) Alive[i].Flinch();
        }

        /// <summary>Knocked by a thrown object or a slow punch: drops the weapon and staggers.</summary>
        public void Stun(Vector3 point)
        {
            if (_state >= State.Dying) return;
            EndReveal();   // hit while still forming: finish appearing at once
            DropGun(Vector3.up * 1.5f + Random.insideUnitSphere * 0.5f);
            _aim = Aim.Idle;
            _burstLeft = 0;
            _body.AimTarget = null;
            _body.AimBlend = 0f;
            _body.Punch = 0f;
            _body.Windup = 0f;
            Enter(State.Stunned);
            SoundBank.Instance?.Play(Sfx.Thud, point, 1f, 0.8f);
        }

        /// <summary>
        /// One hit kills. Bullets, throws and blades shatter the body at once; a punch first shows
        /// glowing cracks spreading from the fist for GameConfig shatter.crackSeconds.
        /// </summary>
        public void Kill(Vector3 point, Vector3 direction, KillKind kind = KillKind.Bullet)
        {
            if (!BeginDeath(point, direction)) return;
            var cfg = Tuning.Shatter;
            if (kind == KillKind.Punch && cfg.punchCracks && cfg.crackSeconds > 0f)
            {
                _state = State.Dying;
                _stateTime = 0f;
                _deathPoint = point;
                _deathDir = direction;
                Cracks.Spread(_body, point, direction, cfg.crackSeconds);
                return;
            }
            Shatter.Break(_body, point, direction, FloorY);
            Retire();
        }

        /// <summary>
        /// The katana: cut in half along the plane through point with planeNormal (the plane of the
        /// blade's swing). Falls back to a shatter if the cut is off (GameConfig) or misses the body.
        /// </summary>
        public void Cut(Vector3 point, Vector3 planeNormal, Vector3 swingDir)
        {
            if (!BeginDeath(point, swingDir)) return;
            if (!Tuning.Shatter.katanaCutsInHalf || !Shatter.Cut(_body, point, planeNormal, swingDir, FloorY))
                Shatter.Break(_body, point, swingDir, FloorY);
            Retire();
        }

        /// <summary>Everything every death does: stop, drop the gun, flash, sound, count the kill.</summary>
        bool BeginDeath(Vector3 point, Vector3 direction)
        {
            if (_state >= State.Dying) return false;
            _state = State.Dead;
            EndReveal();   // the pieces break off a whole body
            foreach (var c in _colliders) if (c != null) c.enabled = false;
            if (_agent != null && _agent.enabled) _agent.enabled = false;
            DropGun(Vector3.up * 1.8f + direction * 0.6f);
            Effects.KillFlash(point, direction);
            SoundBank.Instance?.Play(Sfx.Shatter, point, 1f, Random.Range(0.9f, 1.1f));
            Alive.Remove(this);
            if (GameManager.Instance != null) GameManager.Instance.EnemyKilled(this);
            AlarmNeighbours();
            return true;
        }

        /// <summary>Removes the enemy without effects (restarting a level or wave).</summary>
        public void Despawn()
        {
            _state = State.Dead;
            if (Gun != null) Factory.Recycle(Gun);   // its weapon goes too (back to the gun pool)
            Gun = null;
            Alive.Remove(this);
            Retire();
        }

        /// <summary>Gone for good: back to the enemy pool (GameConfig performance.poolEnemies) or destroyed.</summary>
        void Retire()
        {
            _state = State.Dead;
            if (_agent != null && _agent.enabled) _agent.enabled = false;
            if (_revealing)
            {
                // still forming: back to the shared material, so the pooled body starts clean
                _revealing = false;
                _body.EndReveal(Mats.Get(Mats.Enemy));
            }
            Alive.Remove(this);
            if (!EnemyPool.Release(this)) Destroy(gameObject);
        }
    }
}
