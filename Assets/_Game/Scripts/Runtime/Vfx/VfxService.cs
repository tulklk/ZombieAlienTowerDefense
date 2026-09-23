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
            Play(definition, position, rotation, 1f);
        }

        /// <summary>scaleMultiplier sizes this one playback against the prefab (the victory cinematic sizes the
        /// boss explosion from the boss's own bounds). The pool resets it on release.</summary>
        public void Play(VfxDefinition definition, Vector3 position, Quaternion rotation, float scaleMultiplier)
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
            vfx.SetScaleMultiplier(scaleMultiplier);
            vfx.gameObject.SetActive(true);
            vfx.Play(definition.Lifetime, pool.Release);
        }
    }
}
