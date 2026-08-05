using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Maps each EnemyDefinition to its own EnemyPool, creating and prewarming lazily.</summary>
    public sealed class EnemyPoolRegistry
    {
        private readonly Transform _runtimeParent;
        private readonly Dictionary<EnemyDefinition, EnemyPool> _pools = new Dictionary<EnemyDefinition, EnemyPool>();

        public EnemyPoolRegistry(Transform runtimeParent)
        {
            _runtimeParent = runtimeParent;
        }

        public EnemyPool GetOrCreatePool(EnemyDefinition definition)
        {
            if (definition == null)
            {
                Debug.LogError("[EnemyPoolRegistry] Cannot create a pool for a null EnemyDefinition.");
                return null;
            }

            if (_pools.TryGetValue(definition, out EnemyPool existing))
            {
                return existing;
            }

            if (definition.Prefab == null)
            {
                Debug.LogError($"[EnemyPoolRegistry] EnemyDefinition '{definition.name}' has no Prefab.", definition);
                return null;
            }

            var pool = new EnemyPool(
                definition.Prefab,
                _runtimeParent,
                definition.PoolDefaultCapacity,
                definition.PoolMaximumSize,
                collectionChecks: Debug.isDebugBuild);

            pool.Prewarm(definition.PoolPrewarmCount);
            _pools.Add(definition, pool);
            return pool;
        }

        public void Clear()
        {
            foreach (EnemyPool pool in _pools.Values)
            {
                pool.Clear();
            }

            _pools.Clear();
        }
    }
}
