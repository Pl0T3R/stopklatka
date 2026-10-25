using System.Collections.Generic;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// Anything you can pick up with the grip button and throw: guns, the katana, bottles, mugs...
    /// A thrown object that hits an enemy fast enough stuns it and knocks its weapon out; with
    /// GameConfig combat.killOnThrow it kills instead (see <see cref="KillsOnImpact"/>).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grabbable : MonoBehaviour
    {
        public static readonly List<Grabbable> All = new();

        [Tooltip("Pose relative to the controller anchor while held.")]
        public Vector3 HoldPosition;
        public Vector3 HoldEuler;
        [Tooltip("Breaks into shards on a hard impact (bottles, mugs, ashtrays).")]
        public bool Breakable;

        public Rigidbody Body { get; private set; }
        public ControllerHand Holder { get; private set; }
        public Enemy EnemyOwner { get; private set; }
        public bool IsHeld => Holder != null || EnemyOwner != null;

        /// <summary>Floats in place until grabbed (the pistol in the menu).</summary>
        public bool Hovering { get; private set; }

        /// <summary>Real time of the last release by a hand (so the other hand doesn't catch it at once).</summary>
        public float ReleasedAt { get; private set; } = -10f;

        Collider[] _colliders;
        readonly Collider[] _ignored = new Collider[4];
        int _ignoredCount;
        float _restoreCollisionsAt;
        float _thrownUntil;     // game time
        float _lastThud;
        Vector3 _hoverBase;

        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Body.interpolation = RigidbodyInterpolation.None;
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Grabbable;
            _colliders = GetComponentsInChildren<Collider>(true);
        }

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        /// <summary>The point of this item's colliders nearest to p (for grabbing and catching).</summary>
        public Vector3 ClosestPoint(Vector3 p)
        {
            var best = transform.position;
            float bestD = float.MaxValue;
            foreach (var c in _colliders)
            {
                if (c == null || !c.enabled) continue;
                var q = c.ClosestPoint(p);
                float d = (q - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = q; }
            }
            return best;
        }

        /// <summary>
        /// Passes through [other] for a moment (a weapon dropped from inside an enemy's body),
        /// then collides with it again, so you can still throw it back at that enemy.
        /// </summary>
        public void IgnoreCollisionsBriefly(Collider other, float gameSeconds = 0.6f)
        {
            if (other == null || _ignoredCount >= _ignored.Length) return;
            SetIgnore(other, true);
            _ignored[_ignoredCount++] = other;
            _restoreCollisionsAt = Time.time + gameSeconds;
        }

        void SetIgnore(Collider other, bool ignore)
        {
            foreach (var c in _colliders)
                if (c != null) Physics.IgnoreCollision(c, other, ignore);
        }

        void RestoreCollisions()
        {
            for (int i = 0; i < _ignoredCount; i++)
            {
                if (_ignored[i] != null) SetIgnore(_ignored[i], false);
                _ignored[i] = null;
            }
            _ignoredCount = 0;
        }

        public void SetHovering(bool hover)
        {
            Hovering = hover;
            Body.isKinematic = hover || IsHeld;
            _hoverBase = transform.position;
        }

        public virtual void OnGrab(ControllerHand hand)
        {
            Holder = hand;
            Hovering = false;
            Body.isKinematic = true;
            _thrownUntil = 0f;
        }

        public void FollowHand(Transform anchor)
        {
            transform.SetPositionAndRotation(anchor.TransformPoint(HoldPosition), anchor.rotation * Quaternion.Euler(HoldEuler));
        }

        public virtual void OnRelease(Vector3 velocity, Vector3 angularVelocity)
        {
            Holder = null;
            ReleasedAt = Time.unscaledTime;
            Body.isKinematic = false;
            Body.linearVelocity = velocity;
            Body.angularVelocity = angularVelocity;
            if (velocity.magnitude > Tuning.ThrowStunSpeed) _thrownUntil = Time.time + 3f;
        }

        public virtual void OnTriggerDown(ControllerHand hand) { }
        public virtual void OnTriggerUp(ControllerHand hand) { }

        public void AttachToEnemy(Enemy enemy, Transform socket)
        {
            EnemyOwner = enemy;
            Hovering = false;
            Body.isKinematic = true;
            transform.SetParent(socket, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>The enemy died or was stunned: the weapon flies free and can be caught.</summary>
        public void DropFromEnemy(Vector3 velocity)
        {
            EnemyOwner = null;
            transform.SetParent(null, true);
            Body.isKinematic = false;
            Body.linearVelocity = velocity;
            Body.angularVelocity = Random.insideUnitSphere * 4f;
        }

        protected virtual void Update()
        {
            if (_ignoredCount > 0 && Time.time >= _restoreCollisionsAt) RestoreCollisions();
            if (Hovering && !IsHeld)
            {
                float t = Time.unscaledTime;
                transform.position = _hoverBase + Vector3.up * (Mathf.Sin(t * 2f) * 0.015f);
                transform.Rotate(Vector3.up, 25f * Time.unscaledDeltaTime, Space.World);
            }
        }

        /// <summary>
        /// Does a throw that hits an enemy at this speed kill it? Otherwise it stuns (and the enemy
        /// drops its weapon). Kills only with combat.killOnThrow on: breakables always, others
        /// above the kill speed. The katana overrides this (a blade).
        /// </summary>
        protected virtual bool KillsOnImpact(float speed) =>
            Tuning.KillOnThrow && (Breakable || speed >= Tuning.ThrowKillSpeed);

        void OnCollisionEnter(Collision c)
        {
            if (IsHeld) return;
            float speed = c.relativeVelocity.magnitude;
            var point = c.contactCount > 0 ? c.GetContact(0).point : transform.position;

            var enemy = c.collider.GetComponentInParent<Enemy>();
            if (enemy != null && Time.time <= _thrownUntil && speed >= Tuning.ThrowStunSpeed)
            {
                if (KillsOnImpact(speed)) enemy.Kill(point, Body.linearVelocity.normalized, Enemy.KillKind.Thrown);
                else enemy.Stun(point);
                _thrownUntil = 0f;
            }

            if (Breakable && speed > Tuning.BreakSpeed)
            {
                Break(point);
                return;
            }

            if (speed > 1.2f && Time.time - _lastThud > 0.1f)
            {
                _lastThud = Time.time;
                SoundBank.Instance?.Play(Sfx.Thud, point, Mathf.Clamp01(speed / 6f), Random.Range(0.9f, 1.2f));
            }
        }

        protected virtual void Break(Vector3 point)
        {
            ShardSystem.Burst(transform.position, Mats.Get(Mats.Glass), 14, Body.linearVelocity * 0.3f, 1.6f, 0.025f, 0.05f);
            SoundBank.Instance?.Play(Sfx.GlassBreak, point, 0.9f, Random.Range(0.9f, 1.1f));
            if (Holder != null) Holder.ForceDrop();
            Destroy(gameObject);
        }
    }
}
