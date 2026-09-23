using AlienDefense.Economy;
using AlienDefense.Progression;
using AlienDefense.Save;
using AlienDefense.Settings;
using AlienDefense.Towers;

namespace AlienDefense.Core
{
    /// <summary>Explicit bundle of the application-scope services a scene may need. Passed by reference
    /// from ApplicationCompositionRoot to each scene's entry point.</summary>
    public sealed class ApplicationServices
    {
        public SceneTransitionService SceneTransition { get; }
        public LevelLaunchContext LevelLaunchContext { get; }
        public LevelCatalog LevelCatalog { get; }
        public PlayerProfileService PlayerProfileService { get; }
        public SettingsService SettingsService { get; }
        public TowerCatalog TowerCatalog { get; }

        /// <summary>Rewards a finished level already granted, waiting for MainMenu to animate them into the HUD.
        /// Never null, so callers do not have to guard it.</summary>
        public PendingRewardPresentation PendingRewards { get; }

        /// <summary>Lobby energy spent to start a level. Null only when a caller built services without it (tests).</summary>
        public PlayEnergyService PlayEnergy { get; }

        /// <summary>Player level from saved lifetime XP. Never null (defaults when not supplied).</summary>
        public PlayerLevelCurve PlayerLevels { get; }

        public ApplicationServices(
            SceneTransitionService sceneTransition,
            LevelLaunchContext levelLaunchContext,
            LevelCatalog levelCatalog,
            PlayerProfileService playerProfileService,
            SettingsService settingsService,
            TowerCatalog towerCatalog = null,
            PendingRewardPresentation pendingRewards = null,
            PlayEnergyService playEnergy = null,
            PlayerLevelCurve playerLevels = null)
        {
            SceneTransition = sceneTransition;
            LevelLaunchContext = levelLaunchContext;
            LevelCatalog = levelCatalog;
            PlayerProfileService = playerProfileService;
            SettingsService = settingsService;
            TowerCatalog = towerCatalog;
            PendingRewards = pendingRewards ?? new PendingRewardPresentation();
            PlayEnergy = playEnergy;
            PlayerLevels = playerLevels ?? new PlayerLevelCurve();
        }
    }
}
