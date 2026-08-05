using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Targets the in-range enemy with the smallest XZ distance to the tower.</summary>
    public sealed class ClosestTargetStrategy : ITargetingStrategy
    {
        public EnemyController SelectTarget(EnemyRegistry registry, Vector3 origin, float range)
        {
            if (registry == null)
            {
                return null;
            }

            float rangeSq = range * range;
            EnemyController best = null;
            float bestDistanceSq = float.PositiveInfinity;

            for (int i = 0; i < registry.Count; i++)
            {
                EnemyController candidate = registry.GetAt(i);
                if (candidate == null || !candidate.IsTargetable)
                {
                    continue;
                }

                Vector3 position = candidate.AimPoint.position;
                float dx = position.x - origin.x;
                float dz = position.z - origin.z;
                float distanceSq = dx * dx + dz * dz;

                if (distanceSq <= rangeSq && distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
