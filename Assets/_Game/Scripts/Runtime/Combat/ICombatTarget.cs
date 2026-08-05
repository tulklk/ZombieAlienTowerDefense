using UnityEngine;

namespace AlienDefense.Combat
{
    /// <summary>Anything projectiles/attackers can aim at and damage, independent of concrete type.</summary>
    public interface ICombatTarget
    {
        bool IsTargetable { get; }

        /// <summary>Bumped every time this instance is (re)initialized, to detect pooled reuse.</summary>
        int Generation { get; }

        Transform AimPoint { get; }
        IDamageable Damageable { get; }
    }
}
