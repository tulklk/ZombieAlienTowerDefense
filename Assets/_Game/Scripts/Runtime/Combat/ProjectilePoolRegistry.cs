using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Combat
{
    /// <summary>Maps each ProjectileDefinition to its own ProjectilePool, creating and prewarming lazily.</summary>
    public sealed class ProjectilePoolRegistry
    {
        private readonly Transform _runtimeParent;
        private readonly Dictionary<ProjectileDefinition, ProjectilePool> _pools = new Dictionary<ProjectileDefinition, ProjectilePool>();

        public ProjectilePoolRegistry(Transform runtimeParent)
        {
            _runtimeParent = runtimeParent;
        }

        public ProjectilePool GetOrCreatePool(ProjectileDefinition definition)
        {
            if (definition == null)
            {
                Debug.LogError("[ProjectilePoolRegistry] Cannot create a pool for a null ProjectileDefinition.");
                return null;
            }

            if (_pools.TryGetValue(definition, out ProjectilePool existing))
            {
                return existing;
            }

            if (definition.Prefab == null)
            {
                Debug.LogError($"[ProjectilePoolRegistry] ProjectileDefinition '{definition.name}' has no Prefab.", definition);
                return null;
            }

            var pool = new ProjectilePool(
                definition.Prefab,
                _runtimeParent,
                definition.PoolDefaultCapacity,
                definition.PoolMaximumSize,
                collectionChecks: Debug.isDebugBuild);

            pool.Prewarm(definition.PoolPrewarmCount);
            _pools.Add(definition, pool);
            return pool;
        }

        /// <summary>Projectiles alive across every pool; read-only, for the development performance monitor.</summary>
        public int TotalActive
        {
            get
            {
                int total = 0;
                foreach (ProjectilePool pool in _pools.Values)
                {
                    total += pool.ActiveCount;
                }

                return total;
            }
        }

        public void Clear()
        {
            foreach (ProjectilePool pool in _pools.Values)
            {
                pool.Clear();
            }

            _pools.Clear();
        }
    }
}
