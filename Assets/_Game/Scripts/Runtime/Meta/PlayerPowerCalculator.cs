using AlienDefense.Save;
using AlienDefense.Towers;

namespace AlienDefense.Meta
{
    /// <summary>First real Power source for Profile / HUD: unlocked towers + permanent upgrade levels.
    /// Pure calculation — does not mutate save data.</summary>
    public static class PlayerPowerCalculator
    {
        private const int BasePowerPerUnlockedTower = 120;
        private const int PowerPerUpgradeLevel = 85;

        /// <param name="baseProgression">Optional. When given, Force earned from base buildings is added. The base is a
        /// second Power source folded in here rather than kept as its own total, so the HUD, the Profile screen and
        /// the base's completion banner can never show three different numbers.</param>
        public static int Compute(PlayerProfileService profile, TowerCatalog catalog = null,
            AlienDefense.Base.BaseProgressionService baseProgression = null)
        {
            if (profile == null)
            {
                return 0;
            }

            int unlocked = profile.GetUnlockedTowerCount();
            int power = unlocked * BasePowerPerUnlockedTower;

            if (catalog != null)
            {
                for (int i = 0; i < catalog.Count; i++)
                {
                    TowerDefinition definition = catalog.GetTower(i);
                    if (definition == null || string.IsNullOrEmpty(definition.Id))
                    {
                        continue;
                    }

                    if (!profile.IsTowerUnlocked(definition.Id))
                    {
                        continue;
                    }

                    power += profile.GetTowerUpgradeLevel(definition.Id) * PowerPerUpgradeLevel;
                }
            }
            else
            {
                // Catalog unavailable (tests): approximate from completed levels + unlocks already counted.
                power += profile.GetCompletedLevelCount() * 40;
            }

            if (baseProgression != null)
            {
                power += baseProgression.ComputeBaseForce();
            }

            return power;
        }
    }
}
