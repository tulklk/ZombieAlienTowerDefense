using System;
using AlienDefense.Combat;
using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Owns an enemy's current/maximum health and death notification.</summary>
    public sealed class EnemyHealth : MonoBehaviour, IDamageable
    {
        public float CurrentHealth { get; private set; }
        public float MaximumHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0f;
        public bool IsDamageable => !IsDead && !_isCaptureImmune;

        public event Action<float, float> HealthChanged;
        public event Action Died;

        private bool _diedFired;
        private bool _isCaptureImmune;
        private EnemyDefense _defense;
        private EnemyShield _shield;

        public void Initialize(float maximumHealth)
        {
            MaximumHealth = Mathf.Max(1f, maximumHealth);
            ResetState();
        }

        /// <summary>Optional. Wires the sibling defense/shield components consulted by the DamageInfo overload.</summary>
        public void SetDefenseAndShield(EnemyDefense defense, EnemyShield shield)
        {
            _defense = defense;
            _shield = shield;
        }

        /// <summary>Driven by EnemyController while a tractor beam capture is in progress. No damage source may
        /// bypass this — the raw float overload consults it too.</summary>
        public void SetCaptureImmune(bool value)
        {
            _isCaptureImmune = value;
        }

        public void ResetState()
        {
            CurrentHealth = MaximumHealth;
            _diedFired = false;
            _isCaptureImmune = false;
            HealthChanged?.Invoke(CurrentHealth, MaximumHealth);
        }

        public bool TryApplyDamage(float amount)
        {
            if (amount <= 0f)
            {
                return false;
            }

            if (!IsDamageable)
            {
                return false;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            HealthChanged?.Invoke(CurrentHealth, MaximumHealth);

            if (CurrentHealth <= 0f && !_diedFired)
            {
                _diedFired = true;
                Died?.Invoke();
            }

            return true;
        }

        public bool TryApplyDamage(in DamageInfo damageInfo)
        {
            float amount = damageInfo.Amount;

            if (_defense != null)
            {
                amount = _defense.ModifyIncomingDamage(amount, damageInfo.DamageType);
            }

            if (_shield != null && damageInfo.DamageType != DamageType.True)
            {
                amount = _shield.Absorb(amount);
            }

            return TryApplyDamage(amount);
        }
    }
}
