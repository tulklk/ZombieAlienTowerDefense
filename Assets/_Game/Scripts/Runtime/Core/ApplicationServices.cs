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

        public ApplicationServices(
            SceneTransitionService sceneTransition,
            LevelLaunchContext levelLaunchContext,
            LevelCatalog levelCatalog,
            PlayerProfileService playerProfileService,
            SettingsService settingsService,
            TowerCatalog towerCatalog = null)
        {
            SceneTransition = sceneTransition;
            LevelLaunchContext = levelLaunchContext;
            LevelCatalog = levelCatalog;
            PlayerProfileService = playerProfileService;
            SettingsService = settingsService;
            TowerCatalog = towerCatalog;
        }
    }
}
