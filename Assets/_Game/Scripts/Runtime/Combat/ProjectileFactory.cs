using AlienDefense.Vfx;
using UnityEngine;

namespace AlienDefense.Combat
{
    /// <summary>Spawns projectiles through the pool, injecting target/damage/movement data.</summary>
    public sealed class ProjectileFactory
    {
        private readonly ProjectilePoolRegistry _poolRegistry;
        private readonly VfxService _vfxService;

        public ProjectileFactory(ProjectilePoolRegistry poolRegistry, VfxService vfxService = null)
        {
            _poolRegistry = poolRegistry;
            _vfxService = vfxService;
        }

        public ProjectileController Spawn(ProjectileDefinition definition, ProjectileSpawnRequest request)
        {
            if (definition == null)
            {
                Debug.LogError("[ProjectileFactory] Cannot spawn with a null ProjectileDefinition.");
                return null;
            }

            if (!request.Target.IsValid)
            {
                Debug.LogWarning("[ProjectileFactory] Spawn requested with an already-invalid target; skipping.");
                return null;
            }

            ProjectilePool pool = _poolRegistry.GetOrCreatePool(definition);
            if (pool == null)
            {
                return null;
            }

            ProjectileController projectile = pool.Get();
            if (projectile == null)
            {
                Debug.LogError("[ProjectileFactory] Pool returned a null ProjectileController.");
                return null;
            }

            projectile.transform.SetPositionAndRotation(request.SpawnPosition, request.SpawnRotation);
            projectile.Initialize(
                request.Target,
                request.Damage,
                definition.Speed,
                definition.MaximumLifetime,
                definition.HitDistance,
                pool.Release,
                _vfxService,
                definition.HitVfxDefinition,
                request.StatusEffectOnHit,
                request.AreaDamageResolver,
                request.SplashRadius,
                request.SplashDamage,
                definition.KillVfxDefinition);
            projectile.gameObject.SetActive(true);

            return projectile;
        }
    }
}
