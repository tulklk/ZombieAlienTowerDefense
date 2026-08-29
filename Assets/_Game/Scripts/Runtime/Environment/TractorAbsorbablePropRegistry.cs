using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Environment
{
    /// <summary>Tracks currently active (not-yet-absorbed) TractorAbsorbableProps for the tractor beam's scan,
    /// without exposing a mutable list. Mirrors AlienDefense.Enemies.EnemyRegistry.
    ///
    /// Populated once by LevelCompositionRoot's environment-prop bootstrap (a single FindObjectsByType at level
    /// start — acceptable because it runs once, not per scan/frame) rather than by each prop self-registering in
    /// OnEnable/OnDisable: scene-authored props never enable/disable during play except when this same tractor
    /// beam absorbs them (a one-way Idle→Absorbed transition that unregisters itself), so there is no recurring
    /// enable/disable lifecycle here to react to — see TractorAbsorbableProp.Register.</summary>
    public sealed class TractorAbsorbablePropRegistry
    {
        private readonly List<TractorAbsorbableProp> _active = new List<TractorAbsorbableProp>();

        public int Count => _active.Count;

        public TractorAbsorbableProp GetAt(int index)
        {
            return _active[index];
        }

        public bool Register(TractorAbsorbableProp prop)
        {
            if (prop == null)
            {
                return false;
            }

            if (_active.Contains(prop))
            {
                Debug.LogWarning("[TractorAbsorbablePropRegistry] Prop already registered; ignoring duplicate.", prop);
                return false;
            }

            _active.Add(prop);
            return true;
        }

        public bool Unregister(TractorAbsorbableProp prop)
        {
            if (prop == null)
            {
                return false;
            }

            return _active.Remove(prop);
        }

        public void Clear()
        {
            _active.Clear();
        }
    }
}
