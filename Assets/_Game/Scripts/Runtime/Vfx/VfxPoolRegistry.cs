using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Vfx
{
    /// <summary>Maps each VfxDefinition to its own VfxPool, creating and prewarming lazily.</summary>
    public sealed class VfxPoolRegistry
    {
        private readonly Transform _runtimeParent;
        private readonly Dictionary<VfxDefinition, VfxPool> _pools = new Dictionary<VfxDefinition, VfxPool>();

        public VfxPoolRegistry(Transform runtimeParent)
        {
            _runtimeParent = runtimeParent;
        }

        public VfxPool GetOrCreatePool(VfxDefinition definition)
        {
            if (definition == null)
            {
                Debug.LogError("[VfxPoolRegistry] Cannot create a pool for a null VfxDefinition.");
                return null;
            }

            if (_pools.TryGetValue(definition, out VfxPool existing))
            {
                return existing;
            }

            if (definition.Prefab == null)
            {
                Debug.LogError($"[VfxPoolRegistry] VfxDefinition '{definition.name}' has no Prefab.", definition);
                return null;
            }

            var pool = new VfxPool(
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
            foreach (VfxPool pool in _pools.Values)
            {
                pool.Clear();
            }

            _pools.Clear();
        }
    }
}
