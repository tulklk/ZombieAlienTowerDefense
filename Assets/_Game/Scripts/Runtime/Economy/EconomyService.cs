using System;
using UnityEngine;

namespace AlienDefense.Economy
{
    /// <summary>
    /// Owns the player's spendable resource (gold/energy) for the current level.
    /// This is the only place resource may change — BuildService, tower upgrade and
    /// enemy reward payout must all go through this service instead of touching a number themselves.
    /// </summary>
    public sealed class EconomyService
    {
        public int CurrentResource { get; private set; }

        public event Action<int> ResourceChanged;

        public EconomyService(int startingResource)
        {
            if (startingResource < 0)
            {
                Debug.LogWarning($"[EconomyService] Starting resource {startingResource} is negative, clamped to 0.");
                startingResource = 0;
            }

            CurrentResource = startingResource;
        }

        public bool CanAfford(int amount)
        {
            return amount >= 0 && CurrentResource >= amount;
        }

        /// <summary>Spends resource if, and only if, the full amount can be afforded. Never overdraws.</summary>
        public bool TrySpend(int amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            if (CurrentResource < amount)
            {
                return false;
            }

            CurrentResource -= amount;
            ResourceChanged?.Invoke(CurrentResource);
            return true;
        }

        /// <summary>Adds resource, e.g. enemy kill reward. Rejects non-positive amounts.</summary>
        public void Add(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[EconomyService] Ignored Add({amount}); amount must be positive.");
                return;
            }

            CurrentResource += amount;
            ResourceChanged?.Invoke(CurrentResource);
        }
    }
}
