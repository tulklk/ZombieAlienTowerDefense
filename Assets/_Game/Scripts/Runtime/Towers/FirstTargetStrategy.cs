using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Targets the in-range enemy that has traveled furthest along its path.</summary>
    public sealed class FirstTargetStrategy : ITargetingStrategy
    {
        public EnemyController SelectTarget(EnemyRegistry registry, Vector3 origin, float range)
        {
            if (registry == null)
            {
                return null;
            }

            float rangeSq = range * range;
            EnemyController best = null;
            float bestPathProgress = float.NegativeInfinity;

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
                if (dx * dx + dz * dz > rangeSq)
                {
                    continue;
                }

                float pathProgress = candidate.Movement != null ? candidate.Movement.PathProgress : 0f;
                if (pathProgress > bestPathProgress)
                {
                    bestPathProgress = pathProgress;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
