using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Targets the in-range enemy with the highest current health.</summary>
    public sealed class StrongestTargetStrategy : ITargetingStrategy
    {
        public EnemyController SelectTarget(EnemyRegistry registry, Vector3 origin, float range)
        {
            if (registry == null)
            {
                return null;
            }

            float rangeSq = range * range;
            EnemyController best = null;
            float bestHealth = float.NegativeInfinity;

            for (int i = 0; i < registry.Count; i++)
            {
                EnemyController candidate = registry.GetAt(i);
                if (candidate == null || !candidate.IsTargetable || candidate.Health == null)
                {
                    continue;
                }

                Vector3 position = candidate.AimPoint.position;
                float dx = position.x - origin.x;
                float dz = position.z - origin.z;
                if (dx * dx + dz * dz > rangeSq)
                {
                    continue;
                }

                float currentHealth = candidate.Health.CurrentHealth;
                if (currentHealth > bestHealth)
                {
                    bestHealth = currentHealth;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
