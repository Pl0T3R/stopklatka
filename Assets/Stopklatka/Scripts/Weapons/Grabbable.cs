using System.Collections.Generic;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// Anything you can pick up with the grip button and throw: guns, the katana, bottles.
    /// Thrown objects stun or kill enemies they hit fast enough.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grabbable : MonoBehaviour
    {
        public static readonly List<Grabbable> All = new();

        [Tooltip("Pose relative to the controller anchor while held.")]
        public Vector3 HoldPosition;
        public Vector3 HoldEuler;
        [Tooltip("Impact speed (m/s) that stuns an enemy.")]
        public float StunSpeed = 2f;
        [Tooltip("Impact speed (m/s) that kills an enemy.")]
        public float KillSpeed = 4.5f;
        [Tooltip("Breaks into shards on a hard impact (bottles).")]
        public bool Breakable;

        public Rigidbody Body { get; private set; }
        public ControllerHand Holder { get; private set; }
        public Enemy EnemyOwner { get; private set; }
        public bool IsHeld => Holder != null || EnemyOwner != null;

        /// <summary>Floats in place until grabbed (the pistol in the menu).</summary>
        public bool Hovering { get; private set; }

        float _thrownUntil;     // game time
        float _lastThud;
        Vector3 _hoverBase;

        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Body.interpolation = RigidbodyInterpolation.None;
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Grabbable;
        }

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

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
        }

        public void FollowHand(Transform anchor)
        {
            transform.SetPositionAndRotation(anchor.TransformPoint(HoldPosition), anchor.rotation * Quaternion.Euler(HoldEuler));
        }

        public virtual void OnRelease(Vector3 velocity, Vector3 angularVelocity)
        {
            Holder = null;
            Body.isKinematic = false;
            Body.linearVelocity = velocity;
            Body.angularVelocity = angularVelocity;
            if (velocity.magnitude > StunSpeed) _thrownUntil = Time.time + 3f;
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
            if (Hovering && !IsHeld)
            {
                float t = Time.unscaledTime;
                transform.position = _hoverBase + Vector3.up * (Mathf.Sin(t * 2f) * 0.015f);
                transform.Rotate(Vector3.up, 25f * Time.unscaledDeltaTime, Space.World);
            }
        }

        void OnCollisionEnter(Collision c)
        {
            if (IsHeld) return;
            float speed = c.relativeVelocity.magnitude;
            var point = c.contactCount > 0 ? c.GetContact(0).point : transform.position;

            var enemy = c.collider.GetComponentInParent<Enemy>();
            if (enemy != null && Time.time <= _thrownUntil && speed >= StunSpeed)
            {
                if (speed >= KillSpeed || Breakable) enemy.Kill(point, Body.linearVelocity.normalized);
                else enemy.Stun(point);
                _thrownUntil = 0f;
            }

            if (Breakable && speed > 2.5f)
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
