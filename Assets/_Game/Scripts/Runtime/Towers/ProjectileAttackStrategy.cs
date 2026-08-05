using AlienDefense.Combat;
using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Attacks by spawning a projectile through ProjectileFactory toward the target.</summary>
    public sealed class ProjectileAttackStrategy : IAttackStrategy
    {
        private readonly ProjectileFactory _projectileFactory;
        private readonly ProjectileDefinition _projectileDefinition;

        public ProjectileAttackStrategy(ProjectileFactory projectileFactory, ProjectileDefinition projectileDefinition)
        {
            _projectileFactory = projectileFactory;
            _projectileDefinition = projectileDefinition;
        }

        public bool TryAttack(EnemyController target, DamageInfo damage, Transform firePoint)
        {
            if (target == null || !target.IsTargetable || firePoint == null || _projectileFactory == null)
            {
                return false;
            }

            var targetHandle = new CombatTargetHandle(target);
            var request = new ProjectileSpawnRequest(firePoint.position, firePoint.rotation, targetHandle, damage);
            ProjectileController projectile = _projectileFactory.Spawn(_projectileDefinition, request);

            return projectile != null;
        }
    }
}
