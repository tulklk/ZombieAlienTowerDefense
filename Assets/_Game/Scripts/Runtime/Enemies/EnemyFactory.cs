using AlienDefense.Base;
using AlienDefense.Economy;
using AlienDefense.Pickups;
using AlienDefense.Vfx;
using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Spawns enemies through the pool, injecting runtime dependencies and registering them.</summary>
    public sealed class EnemyFactory
    {
        private readonly EnemyPoolRegistry _poolRegistry;
        private readonly EnemyRegistry _enemyRegistry;
        private readonly EconomyService _economy;
        private readonly BaseHealthService _baseHealth;
        private readonly Transform _cameraTransform;
        private readonly VfxService _vfxService;
        private readonly EnergyDropService _energyDrop;

        public EnemyFactory(
            EnemyPoolRegistry poolRegistry,
            EnemyRegistry enemyRegistry,
            EconomyService economy,
            BaseHealthService baseHealth,
            Transform cameraTransform,
            VfxService vfxService = null,
            EnergyDropService energyDrop = null)
        {
            _poolRegistry = poolRegistry;
            _enemyRegistry = enemyRegistry;
            _economy = economy;
            _baseHealth = baseHealth;
            _cameraTransform = cameraTransform;
            _vfxService = vfxService;
            _energyDrop = energyDrop;
        }

        public EnemyController Spawn(EnemyDefinition definition, EnemyPath3D path, Vector3 position, Quaternion rotation)
        {
            if (definition == null)
            {
                Debug.LogError("[EnemyFactory] Cannot spawn with a null EnemyDefinition.");
                return null;
            }

            if (path == null || path.Count < 2)
            {
                Debug.LogError("[EnemyFactory] Cannot spawn without a valid EnemyPath3D (at least two waypoints).");
                return null;
            }

            EnemyPool pool = _poolRegistry.GetOrCreatePool(definition);
            if (pool == null)
            {
                return null;
            }

            EnemyController enemy = pool.Get();
            if (enemy == null)
            {
                Debug.LogError("[EnemyFactory] Pool returned a null EnemyController.");
                return null;
            }

            enemy.transform.SetPositionAndRotation(position, rotation);
            enemy.Initialize(definition, path, _economy, _baseHealth, _enemyRegistry, pool.Release, _cameraTransform);
            enemy.gameObject.SetActive(true);

            enemy.Resolved -= HandleEnemyResolved;
            enemy.Resolved += HandleEnemyResolved;

            return enemy;
        }

        private void HandleEnemyResolved(EnemyController enemy, EnemyResolveReason reason)
        {
            if (reason == EnemyResolveReason.Defeated && _vfxService != null)
            {
                _vfxService.Play(enemy.Definition.DefeatedVfxDefinition, enemy.transform.position, Quaternion.identity);
            }

            if (_energyDrop != null && EnemyResolutionPolicy.ShouldDropEnergy(reason))
            {
                _energyDrop.Spawn(enemy.transform.position, enemy.Definition.RewardResource);
            }
        }
    }
}
