using AlienDefense.Save;
using AlienDefense.Settings;

namespace AlienDefense.Core
{
    /// <summary>Explicit bundle of the application-scope services a scene may need. Passed by reference
    /// from ApplicationRuntime to each scene's entry point.</summary>
    public sealed class ApplicationServices
    {
        public SceneTransitionService SceneTransition { get; }
        public LevelLaunchContext LevelLaunchContext { get; }
        public LevelCatalog LevelCatalog { get; }
        public PlayerProfileService PlayerProfileService { get; }
        public SettingsService SettingsService { get; }

        public ApplicationServices(
            SceneTransitionService sceneTransition,
            LevelLaunchContext levelLaunchContext,
            LevelCatalog levelCatalog,
            PlayerProfileService playerProfileService,
            SettingsService settingsService)
        {
            SceneTransition = sceneTransition;
            LevelLaunchContext = levelLaunchContext;
            LevelCatalog = levelCatalog;
            PlayerProfileService = playerProfileService;
            SettingsService = settingsService;
        }
    }
}
