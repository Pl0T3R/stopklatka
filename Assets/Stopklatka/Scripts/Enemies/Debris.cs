using System.Collections.Generic;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>One pooled mesh of debris: a copy of an enemy body part, or a glowing cap.</summary>
    public class DebrisPiece : MonoBehaviour
    {
        public MeshFilter Filter { get; private set; }
        public MeshRenderer Renderer { get; private set; }
        /// <summary>Caps glow and are left out of the floor test and the final shards.</summary>
        public bool IsCap { get; set; }

        public void Setup(MeshFilter filter, MeshRenderer renderer)
        {
            Filter = filter;
            Renderer = renderer;
        }
    }

    /// <summary>
    /// A falling piece of a dead enemy: one big chunk (a limb with its glowing break) or half of a
    /// body cut by the katana. Moves itself in game time (gravity, a bounce and a slide on the floor),
    /// without a Rigidbody, so it hangs in the air in slow motion and costs almost nothing.
    /// At the end it shrinks away, or (cut halves) bursts into shards.
    /// </summary>
    public class DebrisBody : MonoBehaviour
    {
        public readonly List<DebrisPiece> Pieces = new();

        Vector3 _velocity, _angular;
        float _floorY, _age, _lifetime, _shrinkSeconds;
        bool _burstAtEnd, _settle, _resting;
        int _burstShards;

        const float Gravity = 9.81f;

        public void Launch(Vector3 velocity, Vector3 angularVelocity, float floorY, float lifetime, bool settleFlat,
                           bool burstAtEnd, int burstShards)
        {
            _velocity = velocity;
            _angular = angularVelocity;
            _floorY = floorY;
            _lifetime = lifetime;
            _age = 0f;
            _settle = settleFlat;
            _resting = false;
            _burstAtEnd = burstAtEnd;
            _burstShards = burstShards;
            _shrinkSeconds = burstAtEnd ? 0f : 0.6f;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _age += dt;
            if (_age >= _lifetime)
            {
                if (_burstAtEnd) Burst();
                DebrisSystem.Release(this);
                return;
            }

            if (!_resting)
            {
                _velocity += Vector3.down * (Gravity * dt);
                transform.position += _velocity * dt;
                float w = _angular.magnitude;
                if (w > 1e-4f) transform.rotation = Quaternion.AngleAxis(w * Mathf.Rad2Deg * dt, _angular / w) * transform.rotation;
                KeepAboveFloor();
            }
            else if (_settle) LieDown(dt);

            float left = _lifetime - _age;
            if (_shrinkSeconds > 0f && left < _shrinkSeconds) transform.localScale = Vector3.one * (left / _shrinkSeconds);
        }

        /// <summary>Bounces off the floor of the enemy's room, then slides to a stop.</summary>
        void KeepAboveFloor()
        {
            float minY = float.MaxValue;
            foreach (var p in Pieces)
                if (!p.IsCap) minY = Mathf.Min(minY, p.Renderer.bounds.min.y);
            if (minY == float.MaxValue || minY >= _floorY) return;

            transform.position += Vector3.up * (_floorY - minY);
            if (_velocity.y < 0f) _velocity.y = -_velocity.y * 0.2f;
            _velocity.x *= 0.55f;
            _velocity.z *= 0.55f;
            _angular *= 0.5f;
            if (_velocity.sqrMagnitude < 0.04f) _resting = true;
        }

        /// <summary>A limb that came to rest on its end tips over until it lies along the floor.</summary>
        void LieDown(float dt)
        {
            var axis = transform.up;
            var flat = Vector3.ProjectOnPlane(axis, Vector3.up);
            if (flat.sqrMagnitude < 1e-4f) flat = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (flat.sqrMagnitude < 1e-4f) return;
            var target = Quaternion.FromToRotation(axis, flat.normalized) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-6f * dt));
            KeepAboveFloorResting();
        }

        void KeepAboveFloorResting()
        {
            float minY = float.MaxValue;
            foreach (var p in Pieces)
                if (!p.IsCap) minY = Mathf.Min(minY, p.Renderer.bounds.min.y);
            if (minY != float.MaxValue && minY < _floorY) transform.position += Vector3.up * (_floorY - minY);
            else if (minY != float.MaxValue && minY > _floorY + 0.005f) transform.position -= Vector3.up * Mathf.Min(minY - _floorY, 0.01f);
        }

        /// <summary>The end of a cut half: it breaks into small red shards that keep its motion.</summary>
        void Burst()
        {
            int parts = 0;
            foreach (var p in Pieces) if (!p.IsCap) parts++;
            if (parts == 0 || _burstShards <= 0) return;
            var cfg = Tuning.Shatter;
            var red = Mats.Get(Mats.Enemy);
            var dark = Mats.Get(Mats.EnemyDark);
            var glow = Mats.Get(Mats.GlowShard);
            int perPart = Mathf.Max(1, _burstShards / parts);
            int n = 0;
            foreach (var p in Pieces)
            {
                if (p.IsCap) continue;
                var b = p.Renderer.bounds;
                for (int i = 0; i < perPart; i++, n++)
                {
                    var pos = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y), Random.Range(b.min.z, b.max.z));
                    var v = _velocity * 0.6f + (pos - b.center).normalized * Random.Range(0.3f, 1.1f) + Vector3.up * 0.4f;
                    var mat = Random.value < cfg.glowShardShare ? glow : n % 3 == 0 ? dark : red;
                    ShardSystem.Emit(mat, pos, v, Random.Range(cfg.shardSizeMin, cfg.shardSizeMax), ShardSystem.RandomLifetime());
                }
            }
            SoundBank.Instance?.Play(Sfx.Shatter, transform.position, 0.6f, Random.Range(1.1f, 1.25f));
        }
    }

    /// <summary>
    /// Pools for enemy debris: <see cref="DebrisBody"/> roots and the <see cref="DebrisPiece"/> meshes
    /// under them. Copies of the dead enemy's part meshes, never cut at runtime. Over the piece
    /// budget (GameConfig shatter.maxPieces) the oldest debris disappears first.
    /// </summary>
    public static class DebrisSystem
    {
        static Pool<DebrisBody> _bodies;
        static Pool<DebrisPiece> _pieces;

        static Pool<DebrisBody> Bodies => _bodies ??= new Pool<DebrisBody>("Debris", CreateBody, 16);
        static Pool<DebrisPiece> Pieces => _pieces ??= new Pool<DebrisPiece>("Debris Pieces", CreatePiece, 48);

        /// <summary>Creates the pools now (at load time) instead of at the first kill.</summary>
        public static void Prewarm()
        {
            _ = Bodies;
            _ = Pieces;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _bodies = null; _pieces = null; }

        /// <summary>A new debris root at this pose. Fill it with <see cref="AddPiece"/>, then call Launch.</summary>
        public static DebrisBody Begin(Vector3 position, Quaternion rotation, int piecesNeeded)
        {
            int budget = Mathf.Max(8, Tuning.Shatter.maxPieces);
            var active = Bodies.Active;
            while (active.Count > 0 && Pieces.Active.Count + piecesNeeded > budget) Release(active[0]);
            var body = Bodies.Get(position, rotation);
            body.transform.localScale = Vector3.one;   // it may have shrunk away last time; pieces are placed in world space next
            return body;
        }

        /// <summary>A mesh under the debris root, at a world pose (and a local scale, for caps).</summary>
        public static DebrisPiece AddPiece(DebrisBody body, Mesh mesh, Material mat, Vector3 position, Quaternion rotation, Vector3 scale, bool cap)
        {
            var piece = Pieces.Get(position, rotation);
            var t = piece.transform;
            t.SetParent(body.transform, true);
            t.localScale = scale;
            piece.Filter.sharedMesh = mesh;
            piece.Renderer.sharedMaterial = mat;
            piece.IsCap = cap;
            body.Pieces.Add(piece);
            return piece;
        }

        public static void Release(DebrisBody body)
        {
            if (body == null) return;
            var pool = Pieces;
            foreach (var p in body.Pieces)
            {
                if (p == null) continue;
                pool.Release(p);
                p.transform.SetParent(pool.Root, false);
            }
            body.Pieces.Clear();
            Bodies.Release(body);
        }

        public static void ClearAll()
        {
            if (_bodies == null) return;
            var active = _bodies.Active;
            for (int i = active.Count - 1; i >= 0; i--) Release(active[i]);
        }

        static DebrisBody CreateBody() => new GameObject("Debris").AddComponent<DebrisBody>();

        static DebrisPiece CreatePiece()
        {
            var go = MeshKit.Part("Debris Piece", null, null, Mats.Get(Mats.Enemy));
            var piece = go.AddComponent<DebrisPiece>();
            piece.Setup(go.GetComponent<MeshFilter>(), go.GetComponent<MeshRenderer>());
            return piece;
        }
    }
}
