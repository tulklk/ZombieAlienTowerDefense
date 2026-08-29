using AlienDefense.Combat;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Creates the IAttackStrategy instance matching a TowerDefinition's AttackBehavior.</summary>
    public static class AttackStrategyFactory
    {
        public static IAttackStrategy Create(TowerDefinition definition, ProjectileFactory projectileFactory, AreaDamageResolver areaDamageResolver)
        {
            switch (definition.AttackBehavior)
            {
                case TowerAttackBehavior.Standard:
                    return new ProjectileAttackStrategy(projectileFactory, definition.ProjectileDefinition);
                case TowerAttackBehavior.Status:
                    return new StatusProjectileAttackStrategy(projectileFactory, definition.ProjectileDefinition, definition.StatusEffectOnHit);
                case TowerAttackBehavior.Splash:
                    return new SplashAttackStrategy(projectileFactory, definition.ProjectileDefinition, areaDamageResolver, definition.SplashRadius);
                default:
                    Debug.LogError($"[AttackStrategyFactory] Unhandled TowerAttackBehavior '{definition.AttackBehavior}'; defaulting to ProjectileAttackStrategy.");
                    return new ProjectileAttackStrategy(projectileFactory, definition.ProjectileDefinition);
            }
        }
    }
}
