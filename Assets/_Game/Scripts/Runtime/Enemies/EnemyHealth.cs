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
        public bool IsDamageable => !IsDead;

        public event Action<float, float> HealthChanged;
        public event Action Died;

        private bool _diedFired;

        public void Initialize(float maximumHealth)
        {
            MaximumHealth = Mathf.Max(1f, maximumHealth);
            ResetState();
        }

        public void ResetState()
        {
            CurrentHealth = MaximumHealth;
            _diedFired = false;
            HealthChanged?.Invoke(CurrentHealth, MaximumHealth);
        }

        public bool TryApplyDamage(float amount)
        {
            if (amount <= 0f)
            {
                return false;
            }

            if (IsDead)
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
            return TryApplyDamage(damageInfo.Amount);
        }
    }
}
