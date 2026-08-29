using System;
using UnityEngine;

namespace AlienDefense.Economy
{
    /// <summary>Owns the player's Energy balance — separate currency from EconomyService (which still funds
    /// Tower build/upgrade). Energy increases ONLY through EnergyCollectionService, i.e. only when an
    /// EnergyPickup is actually tractor-beamed into the UFO. Nothing else may call Add: not Enemy capture,
    /// not Environment Prop absorption, not Tower kills directly.</summary>
    public sealed class EnergyWalletService
    {
        public int CurrentEnergy { get; private set; }

        public event Action<int> EnergyChanged;

        public EnergyWalletService(int startingEnergy = 0)
        {
            if (startingEnergy < 0)
            {
                Debug.LogWarning($"[EnergyWalletService] Starting energy {startingEnergy} is negative, clamped to 0.");
                startingEnergy = 0;
            }

            CurrentEnergy = startingEnergy;
        }

        /// <summary>Adds Energy. Intended caller: EnergyCollectionService only.</summary>
        public void Add(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[EnergyWalletService] Ignored Add({amount}); amount must be positive.");
                return;
            }

            CurrentEnergy += amount;
            EnergyChanged?.Invoke(CurrentEnergy);
        }
    }
}
