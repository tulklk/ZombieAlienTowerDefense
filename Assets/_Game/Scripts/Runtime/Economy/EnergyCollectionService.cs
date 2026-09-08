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

        /// <summary>Call exactly when an EnergyPickup finishes its Lift phase and is absorbed into the UFO.
        /// value (Energy currency, uncapped) and experienceValue (XP, deliberately capped low - see
        /// EnemyDefinition.ExperienceReward / TractorAbsorbableProp's own experience field) are intentionally
        /// separate numbers now: a big kill should still be worth a lot of Energy without also rushing a level-up
        /// in one absorb.</summary>
        public void Collect(int value, int experienceValue)
        {
            if (value <= 0)
            {
                return;
            }

            _wallet?.Add(value);

            if (experienceValue > 0)
            {
                _progression?.AddExperience(experienceValue);
            }
        }
    }
}
