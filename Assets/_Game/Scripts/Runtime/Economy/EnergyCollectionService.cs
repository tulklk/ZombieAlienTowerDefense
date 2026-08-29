namespace AlienDefense.Economy
{
    /// <summary>The single, explicit funnel from "an EnergyPickup was collected" to both reward systems it
    /// feeds. This is the ONLY class that is allowed to touch both EnergyWalletService and
    /// PlayerLevelProgressionService — keeping that fan-out in one small, named place (instead of duplicating
    /// "Wallet.Add + Progression.AddExperience" at every call site) is what makes it safe to reason about who
    /// can grant Energy/XP.</summary>
    public sealed class EnergyCollectionService
    {
        private readonly EnergyWalletService _wallet;
        private readonly PlayerLevelProgressionService _progression;

        public EnergyCollectionService(EnergyWalletService wallet, PlayerLevelProgressionService progression)
        {
            _wallet = wallet;
            _progression = progression;
        }

        /// <summary>Call exactly when an EnergyPickup finishes its Lift phase and is absorbed into the UFO.</summary>
        public void Collect(int value)
        {
            if (value <= 0)
            {
                return;
            }

            _wallet?.Add(value);
            _progression?.AddExperience(value);
        }
    }
}
