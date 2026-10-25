using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Stopklatka
{
    /// <summary>
    /// A red crystal enemy. Armed ones keep their distance and shoot when they can see you
    /// (real walls block their view and their bullets). Unarmed ones walk up and punch.
    /// Everything runs in game time, so they freeze when you stand still.
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        public static readonly List<Enemy> Alive = new();

        enum State { Appearing, Moving, Punching, Stunned, Dead }

        public Gun Gun { get; private set; }

        NavMeshAgent _agent;
        EnemyBody _body;
        State _state;
        float _stateTime, _reaction, _nextFire, _repathAt, _reactionTime, _fireInterval;
        bool _useAgent, _revealing;
        Vector3 _lastPos, _velocity, _manualTarget;
        Renderer[] _gunRenderers;

        const float EyeHeight = 1.6f;
        const float AppearSeconds = 0.9f;
        const float RevealHeight = 1.9f;

        public void Init(EnemyBody body, NavMeshAgent agent, Gun gun, int wave, int agentTypeId, bool navReady)
        {
            _body = body;
            _agent = agent;
            _reactionTime = Tuning.ReactionTime(wave) * Random.Range(0.85f, 1.25f);
            _fireInterval = Tuning.FireInterval(wave);
            _reaction = _reactionTime;
            _nextFire = Time.time + 1.5f;
            if (gun != null)
            {
                Gun = gun;
                gun.AttachToEnemy(this, body.HandSocketR);
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

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _state == State.Dead) return;
            _stateTime += dt;

            var rig = PlayerRig.Instance;
            var head = rig.Head.position;
            var playerFloor = rig.FloorPosition;
            var toPlayer = Vector3.ProjectOnPlane(head - transform.position, Vector3.up);
            float dist = toPlayer.magnitude;

            switch (_state)
            {
                case State.Appearing:
                    _body.Appear = Mathf.Clamp01(_stateTime / AppearSeconds);
                    UpdateReveal();
                    FaceToward(toPlayer, dt, 4f);
                    if (_stateTime >= AppearSeconds) { EndReveal(); Enter(State.Moving); }
                    break;

                case State.Moving:
                    UpdateMovement(playerFloor, toPlayer, dist, dt);
                    if (Gun != null) UpdateShooting(head, dist, dt);
                    else if (dist < Tuning.EnemyPunchRange) Enter(State.Punching);
                    break;

                case State.Punching:
                    Stop();
                    FaceToward(toPlayer, dt, 10f);
                    _body.PunchTarget = head;
                    _body.Punch = _stateTime < 0.55f ? Mathf.SmoothStep(0f, 0.3f, _stateTime / 0.55f) : Mathf.Clamp01((_stateTime - 0.55f) / 0.12f);
                    if (_stateTime >= 0.67f && _stateTime - dt < 0.67f && dist < Tuning.EnemyPunchRange + 0.2f)
                        PlayerHealth.Instance?.TakeHit(head);
                    if (_stateTime >= 1.1f) { _body.Punch = 0f; Enter(State.Moving); }
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

        void UpdateMovement(Vector3 playerFloor, Vector3 toPlayer, float dist, float dt)
        {
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

            bool seesPlayer = Gun != null && _reaction < _reactionTime;
            var look = seesPlayer || dist < 3f ? toPlayer : Vector3.ProjectOnPlane(_velocity, Vector3.up);
            FaceToward(look.sqrMagnitude > 1e-4f ? look : toPlayer, dt, 6f);
        }

        void UpdateShooting(Vector3 head, float dist, float dt)
        {
            var eye = transform.position + Vector3.up * EyeHeight;
            bool canSee = dist < Tuning.EnemySightRange &&
                          !Physics.Linecast(eye, head, Layers.RoomMask, QueryTriggerInteraction.Ignore);
            if (!canSee)
            {
                _reaction = _reactionTime;
                _body.AimTarget = null;
                return;
            }
            _body.AimTarget = head;
            _reaction -= dt;
            if (_reaction > 0f || Time.time < _nextFire) return;
            // the arm needs a moment to line up before the shot
            if (Vector3.Angle(Gun.Muzzle.forward, head - Gun.Muzzle.position) > 12f) return;
            Gun.FireAsEnemy(this, head + Random.insideUnitSphere * 0.05f, 1.6f);
            _nextFire = Time.time + _fireInterval * Random.Range(0.8f, 1.25f);
        }

        void FaceToward(Vector3 dir, float dt, float rate)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            var target = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-rate * dt));
        }

        void Stop()
        {
            if (_useAgent && _agent.isOnNavMesh) _agent.isStopped = true;
        }

        void DropGun(Vector3 velocity)
        {
            if (Gun == null) return;
            SetGunVisible(true);
            _gunRenderers = null;
            Gun.DropFromEnemy(velocity);
            Gun = null;
        }

        /// <summary>Knocked by a thrown object: drops the weapon and staggers.</summary>
        public void Stun(Vector3 point)
        {
            if (_state == State.Dead) return;
            EndReveal();   // hit while still forming: finish appearing at once
            DropGun(Vector3.up * 1.5f + Random.insideUnitSphere * 0.5f);
            _body.AimTarget = null;
            _body.Punch = 0f;
            Enter(State.Stunned);
            SoundBank.Instance?.Play(Sfx.Thud, point, 1f, 0.8f);
        }

        public void Kill(Vector3 point, Vector3 direction)
        {
            if (_state == State.Dead) return;
            _state = State.Dead;
            DropGun(Vector3.up * 1.8f + direction * 0.6f);
            Effects.KillFlash(point, direction);
            ShardSystem.Shatter(_body.Parts, Mats.Get(Mats.Enemy), Mats.Get(Mats.EnemyDark), point, direction, 6);
            SoundBank.Instance?.Play(Sfx.Shatter, point, 1f, Random.Range(0.9f, 1.1f));
            Alive.Remove(this);
            GameManager.Instance?.EnemyKilled(this);
            Destroy(gameObject);
        }

        /// <summary>Removes the enemy without effects (restarting a wave).</summary>
        public void Despawn()
        {
            _state = State.Dead;
            if (Gun != null) Destroy(Gun.gameObject);
            Alive.Remove(this);
            Destroy(gameObject);
        }
    }
}
