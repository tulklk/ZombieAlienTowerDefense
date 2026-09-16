using AlienDefense.Combat;
using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Executes one attack against a target from a fire point. Owns no cooldown or targeting logic.</summary>
    public interface IAttackStrategy
    {
        /// <param name="isFollowUpShot">True for the extra barrels of a multi-muzzle tower firing in the same attack:
        /// the shot still deals its damage, but on-hit effects (e.g. a slow) are left to the first shot so a twin gun
        /// does not apply them twice as often.</param>
        bool TryAttack(EnemyController target, DamageInfo damage, Transform firePoint, bool isFollowUpShot = false);
    }
}
