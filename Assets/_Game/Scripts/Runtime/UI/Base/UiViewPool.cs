using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.UI.Base
{
    /// <summary>Grows a list of prefab instances under one parent and reuses them across refreshes, hiding the
    /// surplus instead of destroying it.
    ///
    /// This is the same activate/deactivate idiom MinimapController already uses for its enemy markers, lifted
    /// into one place because the inventory has four grids that all need it. It matters on mobile: an inventory
    /// refresh fires on every craft, merge and tab change, and rebuilding a few dozen cards each time would
    /// churn GC and rebuild the canvas for no reason.
    ///
    /// Not a general-purpose pool - instances are never returned individually, only trimmed en masse by Begin/End.</summary>
    public sealed class UiViewPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly List<T> _instances = new List<T>();
        private int _activeCount;

        public UiViewPool(T prefab, Transform parent)
        {
            _prefab = prefab;
            _parent = parent;
        }

        public IReadOnlyList<T> Instances => _instances;
        public int ActiveCount => _activeCount;

        /// <summary>Starts a refresh pass. Call Take() once per row, then End().</summary>
        public void Begin()
        {
            _activeCount = 0;
        }

        /// <summary>The next instance, created only if the pool has never grown this large before.</summary>
        public T Take()
        {
            if (_prefab == null || _parent == null)
            {
                return null;
            }

            T instance;
            if (_activeCount < _instances.Count)
            {
                instance = _instances[_activeCount];
            }
            else
            {
                instance = Object.Instantiate(_prefab, _parent);
                _instances.Add(instance);
            }

            _activeCount++;

            if (instance != null && !instance.gameObject.activeSelf)
            {
                instance.gameObject.SetActive(true);
            }

            return instance;
        }

        /// <summary>Hides whatever the pass did not use. Leftovers stay alive for the next refresh.</summary>
        public void End()
        {
            for (int i = _activeCount; i < _instances.Count; i++)
            {
                T instance = _instances[i];
                if (instance != null && instance.gameObject.activeSelf)
                {
                    instance.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>Destroys every instance. Only for teardown - a normal refresh uses Begin/Take/End.</summary>
        public void Clear()
        {
            for (int i = 0; i < _instances.Count; i++)
            {
                if (_instances[i] != null)
                {
                    Object.Destroy(_instances[i].gameObject);
                }
            }

            _instances.Clear();
            _activeCount = 0;
        }
    }
}
