using UnityEngine;

namespace AlienDefense.Pickups
{
    /// <summary>Thin, stable seam between "something died" and "an EnergyPickup appears on the ground". Knows
    /// nothing about EnemyResolveReason/EnemyController — the "only Defeated drops Energy" policy lives in
    /// AlienDefense.Enemies.EnemyResolutionPolicy (in the Enemies namespace, which is allowed to depend on
    /// Pickups; Pickups must never depend back on Enemies).</summary>
    public sealed class EnergyDropService
    {
        private readonly EnergyPickupFactory _factory;

        public EnergyDropService(EnergyPickupFactory factory)
        {
            _factory = factory;
        }

        public void Spawn(Vector3 position, int value)
        {
            _factory?.Spawn(position, value);
        }
    }
}
