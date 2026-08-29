namespace AlienDefense.Core
{
    /// <summary>Configures the next Bootstrap loading pass before LoadScene(Bootstrap) is called from a content scene.</summary>
    public static class BootstrapLoadContext
    {
        private static string _targetSceneName = SceneNames.MainMenu;
        private static bool _shouldInitializeApplication = true;

        public static string TargetSceneName => _targetSceneName;

        public static bool ShouldInitializeApplication => _shouldInitializeApplication;

        public static void RequestLoad(string targetSceneName, bool initializeApplication = false)
        {
            _targetSceneName = targetSceneName;
            _shouldInitializeApplication = initializeApplication;
        }

        public static void ResetToColdBootDefaults()
        {
            _targetSceneName = SceneNames.MainMenu;
            _shouldInitializeApplication = true;
        }
    }
}
