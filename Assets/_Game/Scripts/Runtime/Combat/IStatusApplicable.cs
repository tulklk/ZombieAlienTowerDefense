using UnityEngine;

namespace AlienDefense.Combat
{
    /// <summary>Anything that can receive a status effect. Kept in Combat (not Enemies) so ICombatTarget never
    /// creates a Combat-to-Enemies dependency; EnemyStatusController is the only implementer.</summary>
    public interface IStatusApplicable
    {
        void ApplyStatus(StatusEffectDefinition definition, GameObject source = null);
    }
}
