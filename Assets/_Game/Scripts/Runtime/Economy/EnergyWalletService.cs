using System;
using UnityEngine;

namespace AlienDefense.Economy
{
    /// <summary>Owns the player's Energy balance — separate currency from EconomyService (which still funds
    /// Tower build/upgrade). Energy increases ONLY through EnergyCollectionService, i.e. only when an
    /// EnergyPickup is actually tractor-beamed into the UFO. Nothing else may call Add: not Enemy capture,
    /// not Environment Prop absorption, not Tower kills directly.
    ///
    /// Capped by MaxEnergy ("Tải" - the Capacity skill's current rank; see PlayerSkillEffectApplier, which is
    /// the only intended caller of SetMaxEnergy). Collecting past the cap simply doesn't add the excess - there
    /// is no separate "overflow" concept.</summary>
    public sealed class EnergyWalletService
    {
        public int CurrentEnergy { get; private set; }
        public int MaxEnergy { get; private set; }

        /// <summary>True when CurrentEnergy has reached MaxEnergy — UFO must not admit new EnergyPickups.</summary>
        public bool IsFull => CurrentEnergy >= MaxEnergy;

        /// <summary>True when at least <paramref name="amount"/> more Energy can fit under the cap.</summary>
        public bool HasRoomFor(int amount) => amount > 0 && CurrentEnergy + amount <= MaxEnergy;

        public event Action<int> EnergyChanged;
        public event Action<int> MaxEnergyChanged;

        public EnergyWalletService(int startingEnergy = 0, int maxEnergy = 10)
        {
            if (startingEnergy < 0)
            {
                Debug.LogWarning($"[EnergyWalletService] Starting energy {startingEnergy} is negative, clamped to 0.");
                startingEnergy = 0;
            }

            MaxEnergy = Mathf.Max(1, maxEnergy);
            CurrentEnergy = Mathf.Min(startingEnergy, MaxEnergy);
        }

        public bool CanAfford(int amount)
        {
            return amount >= 0 && CurrentEnergy >= amount;
        }

        /// <summary>Spends Energy if, and only if, the full amount can be afforded. Intended caller:
        /// EnergyTowerTransactionService only.</summary>
        public bool TrySpend(int amount)
        {
            if (amount <= 0 || CurrentEnergy < amount)
            {
                return false;
            }

            CurrentEnergy -= amount;
            EnergyChanged?.Invoke(CurrentEnergy);
            return true;
        }

        /// <summary>Adds Energy, clamped to MaxEnergy. Intended caller: EnergyCollectionService only.</summary>
        public void Add(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[EnergyWalletService] Ignored Add({amount}); amount must be positive.");
                return;
            }

            int clamped = Mathf.Min(CurrentEnergy + amount, MaxEnergy);
            if (clamped == CurrentEnergy)
            {
                return; // already at/over cap - nothing changed, no event
            }

            CurrentEnergy = clamped;
            EnergyChanged?.Invoke(CurrentEnergy);
        }

        /// <summary>Raises (or lowers) the cap - e.g. the Capacity ("Tải") skill's new rank. If CurrentEnergy now
        /// exceeds the new cap it's clamped down and EnergyChanged also fires. Intended caller:
        /// PlayerSkillEffectApplier only.</summary>
        public void SetMaxEnergy(int maxEnergy)
        {
            MaxEnergy = Mathf.Max(1, maxEnergy);
            MaxEnergyChanged?.Invoke(MaxEnergy);

            if (CurrentEnergy > MaxEnergy)
            {
                CurrentEnergy = MaxEnergy;
                EnergyChanged?.Invoke(CurrentEnergy);
            }
        }
    }
}
