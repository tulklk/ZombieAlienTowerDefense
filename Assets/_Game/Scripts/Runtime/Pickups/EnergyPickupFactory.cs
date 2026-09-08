using AlienDefense.Economy;
using UnityEngine;

namespace AlienDefense.Pickups
{
    /// <summary>Spawns EnergyPickups through the pool, registers them, and owns the one place that turns
    /// "a pickup was Collected" into an actual reward: EnergyCollectionService.Collect(value) + pool release.
    /// Mirrors AlienDefense.Enemies.EnemyFactory's role/shape (spawn + reward-adjacent side effect + release).</summary>
    public sealed class EnergyPickupFactory
    {
        private readonly EnergyPickupPool _pool;
        private readonly EnergyPickupRegistry _registry;
        private readonly EnergyCollectionService _energyCollection;

        public EnergyPickupFactory(EnergyPickupPool pool, EnergyPickupRegistry registry, EnergyCollectionService energyCollection)
        {
            _pool = pool;
            _registry = registry;
            _energyCollection = energyCollection;
        }

        public EnergyPickupController Spawn(Vector3 position, int value, int experienceValue = 0)
        {
            if (_pool == null || value <= 0)
            {
                return null;
            }

            EnergyPickupController pickup = _pool.Get();
            if (pickup == null)
            {
                Debug.LogError("[EnergyPickupFactory] Pool returned a null EnergyPickupController.");
                return null;
            }

            pickup.transform.SetPositionAndRotation(position, Quaternion.identity);
            pickup.Initialize(value, experienceValue);
            pickup.gameObject.SetActive(true);

            pickup.Collected -= HandlePickupCollected;
            pickup.Collected += HandlePickupCollected;

            _registry?.Register(pickup);
            return pickup;
        }

        private void HandlePickupCollected(EnergyPickupController pickup)
        {
            pickup.Collected -= HandlePickupCollected;
            _registry?.Unregister(pickup);
            _energyCollection?.Collect(pickup.Value, pickup.ExperienceValue);
            _pool.Release(pickup);
        }
    }
}
