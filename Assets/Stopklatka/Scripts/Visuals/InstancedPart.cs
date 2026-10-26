using UnityEngine;

namespace Stopklatka
{
    /// <summary>Which GameConfig performance switch an instanced part follows.</summary>
    public enum InstanceGroup { Effects, Enemies, Items, Props }

    /// <summary>
    /// Marks a MeshFilter + MeshRenderer as one of many identical copies (a shard, a bullet streak,
    /// an enemy's thigh...). While it is active, <see cref="InstanceDrawer"/> draws it together with
    /// every other copy of the same mesh and material in one instanced draw call, and keeps its own
    /// renderer quiet with <see cref="Renderer.forceRenderingOff"/> (the renderer stays enabled, so
    /// its bounds stay valid for the shatter code). Nothing about the object's simulation changes.
    /// Suspended parts draw with their own renderer again: things in your hands (they follow the
    /// controller after LateUpdate), an enemy while it forms (its reveal uses a per-renderer value).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InstancedPart : MonoBehaviour
    {
        public InstanceGroup Group;

        internal MeshFilter Filter;
        internal MeshRenderer Renderer;
        internal Transform Cached;
        internal int Layer;
        /// <summary>Position in the drawer's list (-1 when not registered).</summary>
        internal int Index = -1;
        /// <summary>The renderer is switched off because the drawer draws this part.</summary>
        internal bool Forced;

        bool _suspended;

        /// <summary>Draw with the own renderer instead (see the class summary). Takes effect at once.</summary>
        public bool Suspended
        {
            get => _suspended;
            set
            {
                _suspended = value;
                if (value) InstanceDrawer.Release(this);
            }
        }

        /// <summary>Adds the marker to a renderer object (MeshKit.Part) and returns it.</summary>
        public static InstancedPart Attach(GameObject go, InstanceGroup group)
        {
            if (!go.TryGetComponent(out InstancedPart part)) part = go.AddComponent<InstancedPart>();
            part.Group = group;
            return part;
        }

        /// <summary>Suspends or resumes every part in a list (an item grabbed or let go).</summary>
        public static void SetSuspended(InstancedPart[] parts, bool suspended)
        {
            if (parts == null) return;
            foreach (var p in parts)
                if (p != null) p.Suspended = suspended;
        }

        void Awake()
        {
            Filter = GetComponent<MeshFilter>();
            Renderer = GetComponent<MeshRenderer>();
            Cached = transform;
        }

        void OnEnable()
        {
            if (Filter == null || Renderer == null) return;
            Layer = gameObject.layer;
            InstanceDrawer.Add(this);
        }

        void OnDisable() => InstanceDrawer.Remove(this);
    }
}
