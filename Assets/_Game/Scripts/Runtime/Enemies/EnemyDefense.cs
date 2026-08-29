using AlienDefense.Combat;
using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Optional armor component: reduces incoming Physical damage only. Energy/Explosive/True bypass it.</summary>
    public sealed class EnemyDefense : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.95f)]
        private float _physicalDamageReductionPercent = 0.5f;

        private float _currentPhysicalDamageReductionPercent;

        private void Awake()
        {
            _currentPhysicalDamageReductionPercent = _physicalDamageReductionPercent;
        }

        /// <summary>Overrides the active reduction at runtime (e.g. Boss phase-two temporary resistance).</summary>
        public void SetPhysicalDamageReductionPercent(float value)
        {
            _currentPhysicalDamageReductionPercent = Mathf.Clamp01(value);
        }

        /// <summary>Called by EnemyController on pool reuse to discard any runtime override.</summary>
        public void ResetState()
        {
            _currentPhysicalDamageReductionPercent = _physicalDamageReductionPercent;
        }

        public float ModifyIncomingDamage(float amount, DamageType damageType)
        {
            if (damageType != DamageType.Physical)
            {
                return amount;
            }

            return amount * (1f - _currentPhysicalDamageReductionPercent);
        }
    }
}
