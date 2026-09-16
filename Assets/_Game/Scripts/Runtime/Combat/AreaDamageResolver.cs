using UnityEngine;

namespace AlienDefense.Combat
{
    /// <summary>Applies damage to every targetable ICombatTarget within a radius via a linear provider scan. No per-call allocation.</summary>
    public sealed class AreaDamageResolver
    {
        private readonly ISplashTargetProvider _targetProvider;

        public AreaDamageResolver(ISplashTargetProvider targetProvider)
        {
            _targetProvider = targetProvider;
        }

        /// <summary>Damages every targetable enemy within radius of center exactly once. Returns how many were hit.</summary>
        public int ResolveSplash(Vector3 center, float radius, DamageInfo damageTemplate)
        {
            if (_targetProvider == null || radius <= 0f)
            {
                return 0;
            }

            float radiusSquared = radius * radius;
            int hitCount = 0;

            for (int i = 0; i < _targetProvider.Count; i++)
            {
                ICombatTarget target = _targetProvider.GetAt(i);
                if (target == null || !target.IsTargetable)
                {
                    continue;
                }

                Vector3 position = target.AimPoint.position;
                float dx = position.x - center.x;
                float dz = position.z - center.z;
                if (dx * dx + dz * dz > radiusSquared)
                {
                    continue;
                }

                var info = new DamageInfo(damageTemplate.Amount, damageTemplate.Source, position, damageTemplate.DamageType,
                    damageTemplate.KillVfx, damageTemplate.PopupStyle);
                if (target.Damageable != null && target.Damageable.TryApplyDamage(info))
                {
                    hitCount++;
                }
            }

            return hitCount;
        }
    }
}
