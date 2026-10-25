using System;
using System.Collections.Generic;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// A floating menu object with a label: no laser pointers, no flat panels (GAME_SPEC 8).
    ///   cubes (default): black cubes you grab with the grip (<see cref="ControllerHand"/> asks
    ///                    <see cref="FindGrabbable"/>); a fast punch, a bullet or a katana cut work too,
    ///   crystals (GameConfig menu.useCubes off): shoot, cut or touch them with a controller.
    /// Either way it breaks into shards of its own material and runs its action.
    /// </summary>
    public class MenuTarget : MonoBehaviour
    {
        public static readonly List<MenuTarget> All = new();

        public Action OnActivated;
        public Renderer Visual;
        /// <summary>Touching it with a controller activates it (the old crystals).</summary>
        public bool TouchActivates = true;
        /// <summary>The grip activates it (the cubes).</summary>
        public bool Grabbable;
        /// <summary>Half size (m) of the cube, for grabbing.</summary>
        public float Size = 0.1f;

        WorldText _label;
        Vector3 _base;
        bool _done;

        public void Setup(string label, Renderer visual, Action onActivated, float labelHeight, float labelEm)
        {
            Visual = visual;
            OnActivated = onActivated;
            _base = transform.position;
            _label = WorldText.Create("Label", transform, labelEm, Color.white, WorldText.Align.Bottom);
            _label.Transform.localPosition = new Vector3(0f, labelHeight, 0f);
            _label.Text = label;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void SetLabel(string text)
        {
            if (_label != null) _label.Text = text;
        }

        /// <summary>The grabbable menu cube nearest to a hand point, if one is within reach.</summary>
        public static MenuTarget FindGrabbable(Vector3 point, float extraRadius)
        {
            MenuTarget best = null;
            float bestD = float.MaxValue;
            foreach (var t in All)
            {
                if (t == null || t._done || !t.Grabbable || t.Visual == null) continue;
                float d = Vector3.Distance(t.Visual.transform.position, point);
                if (d < t.Size + extraRadius && d < bestD) { bestD = d; best = t; }
            }
            return best;
        }

        void Update()
        {
            float t = Time.unscaledTime;
            transform.position = _base + Vector3.up * Mathf.Sin(t * 1.6f) * 0.02f;
            Visual.transform.Rotate(Vector3.up, (Grabbable ? 25f : 40f) * Time.unscaledDeltaTime, Space.World);

            var rig = PlayerRig.Instance;
            if (rig == null || _done) return;
            _label.Transform.rotation = Quaternion.LookRotation(_label.Transform.position - rig.Head.position, Vector3.up);
            if (!TouchActivates) return;
            var c = Visual.transform.position;
            if (Vector3.Distance(rig.LeftController.position, c) < 0.16f || Vector3.Distance(rig.RightController.position, c) < 0.16f)
                Activate(Vector3.up);
        }

        public void Activate(Vector3 direction)
        {
            if (_done) return;
            _done = true;
            ShardSystem.Burst(Visual.transform.position, Visual.sharedMaterial, 22, direction * 1.5f, 1.8f, 0.03f, 0.06f);
            var sound = SoundBank.Instance;
            if (sound != null) sound.Play(Sfx.Shatter, Visual.transform.position, 0.9f, 1.2f, false);
            var callback = OnActivated;
            Destroy(gameObject);
            callback?.Invoke();
        }
    }
}
