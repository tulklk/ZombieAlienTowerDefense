using UnityEngine;

namespace AlienDefense.Vfx
{
    /// <summary>Plays one-shot pooled VFX at a world position/rotation. Holds no gameplay state or logic.</summary>
    public sealed class VfxService
    {
        private readonly VfxPoolRegistry _poolRegistry;

        public VfxService(VfxPoolRegistry poolRegistry)
        {
            _poolRegistry = poolRegistry;
        }

        public void Play(VfxDefinition definition, Vector3 position, Quaternion rotation)
        {
            if (definition == null || _poolRegistry == null)
            {
                return;
            }

            VfxPool pool = _poolRegistry.GetOrCreatePool(definition);
            if (pool == null)
            {
                return;
            }

            PooledVfx vfx = pool.Get();
            if (vfx == null)
            {
                Debug.LogError("[VfxService] Pool returned a null PooledVfx.");
                return;
            }

            vfx.transform.SetPositionAndRotation(position, rotation);
            vfx.gameObject.SetActive(true);
            vfx.Play(definition.Lifetime, pool.Release);
        }
    }
}
