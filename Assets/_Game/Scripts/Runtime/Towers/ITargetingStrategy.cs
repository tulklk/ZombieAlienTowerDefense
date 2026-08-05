using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Selects one in-range enemy from the registry for a tower to attack.</summary>
    public interface ITargetingStrategy
    {
        EnemyController SelectTarget(EnemyRegistry registry, Vector3 origin, float range);
    }
}
