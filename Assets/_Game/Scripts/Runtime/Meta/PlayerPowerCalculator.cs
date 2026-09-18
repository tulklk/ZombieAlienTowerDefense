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

        public static int Compute(PlayerProfileService profile, TowerCatalog catalog = null)
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

            return power;
        }
    }
}
