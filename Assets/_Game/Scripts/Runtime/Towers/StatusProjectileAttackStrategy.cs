using AlienDefense.Combat;
using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Attacks like ProjectileAttackStrategy, additionally applying a status effect to the target on hit.</summary>
    public sealed class StatusProjectileAttackStrategy : IAttackStrategy
    {
        private readonly ProjectileFactory _projectileFactory;
        private readonly ProjectileDefinition _projectileDefinition;
        private readonly StatusEffectDefinition _statusEffectOnHit;

        public StatusProjectileAttackStrategy(ProjectileFactory projectileFactory, ProjectileDefinition projectileDefinition, StatusEffectDefinition statusEffectOnHit)
        {
            _projectileFactory = projectileFactory;
            _projectileDefinition = projectileDefinition;
            _statusEffectOnHit = statusEffectOnHit;
        }

        public bool TryAttack(EnemyController target, DamageInfo damage, Transform firePoint, bool isFollowUpShot = false)
        {
            if (target == null || !target.IsTargetable || firePoint == null || _projectileFactory == null)
            {
                return false;
            }

            var targetHandle = new CombatTargetHandle(target);
            var request = new ProjectileSpawnRequest(firePoint.position, firePoint.rotation, targetHandle, damage, statusEffectOnHit: isFollowUpShot ? null : _statusEffectOnHit);
            ProjectileController projectile = _projectileFactory.Spawn(_projectileDefinition, request);

            return projectile != null;
        }
    }
}
