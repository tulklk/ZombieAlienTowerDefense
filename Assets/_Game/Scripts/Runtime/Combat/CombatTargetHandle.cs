using UnityEngine;

namespace AlienDefense.Combat
{
    /// <summary>Safe handle to an ICombatTarget that detects pooled-instance reuse via generation.</summary>
    public readonly struct CombatTargetHandle
    {
        private readonly ICombatTarget _target;
        private readonly int _generation;

        public CombatTargetHandle(ICombatTarget target)
        {
            _target = target;
            _generation = target?.Generation ?? -1;
        }

        public bool IsValid => _target != null && _target.IsTargetable && _target.Generation == _generation;
        public Transform AimPoint => _target?.AimPoint;
        public IDamageable Damageable => _target?.Damageable;
    }
}
