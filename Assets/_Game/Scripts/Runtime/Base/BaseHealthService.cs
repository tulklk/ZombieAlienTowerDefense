using System;
using UnityEngine;

namespace AlienDefense.Base
{
    /// <summary>Owns the defended base's health for the current level.</summary>
    public sealed class BaseHealthService
    {
        public int MaxHealth { get; }
        public int CurrentHealth { get; private set; }
        public bool IsDestroyed => CurrentHealth <= 0;

        /// <summary>Fired with (currentHealth, maxHealth) whenever health changes.</summary>
        public event Action<int, int> HealthChanged;

        /// <summary>Fired exactly once, the first time health reaches zero.</summary>
        public event Action Destroyed;

        private bool _destroyedFired;

        public BaseHealthService(int maxHealth)
        {
            if (maxHealth <= 0)
            {
                Debug.LogWarning($"[BaseHealthService] maxHealth {maxHealth} must be > 0, clamped to 1.");
                maxHealth = 1;
            }

            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[BaseHealthService] Ignored TakeDamage({amount}); amount must be positive.");
                return;
            }

            if (IsDestroyed)
            {
                return;
            }

            CurrentHealth -= amount;
            if (CurrentHealth < 0)
            {
                CurrentHealth = 0;
            }

            HealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (CurrentHealth == 0 && !_destroyedFired)
            {
                _destroyedFired = true;
                Destroyed?.Invoke();
            }
        }
    }
}
