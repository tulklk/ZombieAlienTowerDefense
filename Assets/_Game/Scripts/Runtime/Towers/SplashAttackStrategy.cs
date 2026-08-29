using AlienDefense.Combat;
using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Attacks like ProjectileAttackStrategy, but the projectile deals radius damage on impact instead of single-target.</summary>
    public sealed class SplashAttackStrategy : IAttackStrategy
    {
        private readonly ProjectileFactory _projectileFactory;
        private readonly ProjectileDefinition _projectileDefinition;
        private readonly AreaDamageResolver _areaDamageResolver;
        private readonly float _splashRadius;

        public SplashAttackStrategy(ProjectileFactory projectileFactory, ProjectileDefinition projectileDefinition, AreaDamageResolver areaDamageResolver, float splashRadius)
        {
            _projectileFactory = projectileFactory;
            _projectileDefinition = projectileDefinition;
            _areaDamageResolver = areaDamageResolver;
            _splashRadius = splashRadius;
        }

        public bool TryAttack(EnemyController target, DamageInfo damage, Transform firePoint)
        {
            if (target == null || !target.IsTargetable || firePoint == null || _projectileFactory == null)
            {
                return false;
            }

            var targetHandle = new CombatTargetHandle(target);
            var request = new ProjectileSpawnRequest(firePoint.position, firePoint.rotation, targetHandle, damage, areaDamageResolver: _areaDamageResolver, splashRadius: _splashRadius);
            ProjectileController projectile = _projectileFactory.Spawn(_projectileDefinition, request);

            return projectile != null;
        }
    }
}
