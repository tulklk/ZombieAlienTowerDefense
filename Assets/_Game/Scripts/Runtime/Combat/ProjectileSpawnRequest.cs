using UnityEngine;

namespace AlienDefense.Combat
{
    /// <summary>Everything ProjectileFactory needs to spawn and initialize one projectile.</summary>
    public readonly struct ProjectileSpawnRequest
    {
        public readonly Vector3 SpawnPosition;
        public readonly Quaternion SpawnRotation;
        public readonly CombatTargetHandle Target;
        public readonly DamageInfo Damage;
        public readonly StatusEffectDefinition StatusEffectOnHit;
        public readonly AreaDamageResolver AreaDamageResolver;
        public readonly float SplashRadius;

        public ProjectileSpawnRequest(
            Vector3 spawnPosition,
            Quaternion spawnRotation,
            CombatTargetHandle target,
            DamageInfo damage,
            StatusEffectDefinition statusEffectOnHit = null,
            AreaDamageResolver areaDamageResolver = null,
            float splashRadius = 0f)
        {
            SpawnPosition = spawnPosition;
            SpawnRotation = spawnRotation;
            Target = target;
            Damage = damage;
            StatusEffectOnHit = statusEffectOnHit;
            AreaDamageResolver = areaDamageResolver;
            SplashRadius = splashRadius;
        }
    }
}
