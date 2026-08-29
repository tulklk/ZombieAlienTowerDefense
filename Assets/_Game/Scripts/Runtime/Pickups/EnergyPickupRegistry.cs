using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Pickups
{
    /// <summary>Tracks currently active (spawned, not-yet-collected) EnergyPickups for the tractor beam's scan,
    /// without exposing a mutable list. Mirrors AlienDefense.Enemies.EnemyRegistry.</summary>
    public sealed class EnergyPickupRegistry
    {
        private readonly List<EnergyPickupController> _active = new List<EnergyPickupController>();

        public int Count => _active.Count;

        public EnergyPickupController GetAt(int index)
        {
            return _active[index];
        }

        public bool Register(EnergyPickupController pickup)
        {
            if (pickup == null)
            {
                return false;
            }

            if (_active.Contains(pickup))
            {
                Debug.LogWarning("[EnergyPickupRegistry] Pickup already registered; ignoring duplicate.", pickup);
                return false;
            }

            _active.Add(pickup);
            return true;
        }

        public bool Unregister(EnergyPickupController pickup)
        {
            if (pickup == null)
            {
                return false;
            }

            return _active.Remove(pickup);
        }

        public void Clear()
        {
            _active.Clear();
        }
    }
}
