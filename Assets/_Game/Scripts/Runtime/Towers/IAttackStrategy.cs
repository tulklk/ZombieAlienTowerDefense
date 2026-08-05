using AlienDefense.Combat;
using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Executes one attack against a target from a fire point. Owns no cooldown or targeting logic.</summary>
    public interface IAttackStrategy
    {
        bool TryAttack(EnemyController target, DamageInfo damage, Transform firePoint);
    }
}
