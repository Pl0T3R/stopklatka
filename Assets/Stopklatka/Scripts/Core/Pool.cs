using System;
using System.Collections.Generic;
using UnityEngine;

namespace Stopklatka
{
    /// <summary>Reuses GameObjects instead of creating them mid-game (no GC spikes at 120 Hz).</summary>
    public class Pool<T> where T : Component
    {
        readonly Func<T> _create;
        readonly Stack<T> _free = new();
        readonly List<T> _active = new();
        readonly Transform _root;

        public IReadOnlyList<T> Active => _active;

        /// <summary>Parent of every pooled item. Items that were parented elsewhere go back here on release.</summary>
        public Transform Root => _root;

        public Pool(string name, Func<T> create, int prewarm = 0)
        {
            _create = create;
            _root = new GameObject($"Pool: {name}").transform;
            for (int i = 0; i < prewarm; i++)
            {
                var item = Create();
                item.gameObject.SetActive(false);
                _free.Push(item);
            }
        }

        T Create()
        {
            var item = _create();
            item.transform.SetParent(_root, false);
            return item;
        }

        public T Get(Vector3 position, Quaternion rotation)
        {
            var item = _free.Count > 0 ? _free.Pop() : Create();
            item.transform.SetPositionAndRotation(position, rotation);
            item.gameObject.SetActive(true);
            _active.Add(item);
            return item;
        }

        public void Release(T item)
        {
            if (item == null || !item.gameObject.activeSelf) return;
            item.gameObject.SetActive(false);
            _active.Remove(item);
            _free.Push(item);
        }

        public void ReleaseAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var item = _active[i];
                item.gameObject.SetActive(false);
                _free.Push(item);
            }
            _active.Clear();
        }
    }
}
