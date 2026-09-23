namespace AlienDefense.Core
{
    /// <summary>How SceneTransitionService covers the screen while a scene loads.</summary>
    public enum SceneTransitionStyle
    {
        /// <summary>The full branded loading screen (splash art, logo, progress bar) held for
        /// <c>_minimumLoadDurationSeconds</c>. Used when entering a level, where the load is genuinely long.</summary>
        BrandedSplash = 0,

        /// <summary>A short black fade with no artwork and no progress bar. Used for level -> menu returns: the
        /// target scene loads almost instantly, and showing the splash there makes players think the game bounced
        /// through the Bootstrap scene.</summary>
        QuickFade = 1
    }
}
