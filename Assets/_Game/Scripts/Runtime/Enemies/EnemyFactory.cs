using AlienDefense.Base;
using AlienDefense.Economy;
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

        public EnemyFactory(
            EnemyPoolRegistry poolRegistry,
            EnemyRegistry enemyRegistry,
            EconomyService economy,
            BaseHealthService baseHealth,
            Transform cameraTransform)
        {
            _poolRegistry = poolRegistry;
            _enemyRegistry = enemyRegistry;
            _economy = economy;
            _baseHealth = baseHealth;
            _cameraTransform = cameraTransform;
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

            return enemy;
        }
    }
}
