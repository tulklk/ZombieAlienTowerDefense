using AlienDefense.Save;
using UnityEngine;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Turns a level's saved progress into the one-line status shown under its title on MainMenu.
    /// Pure formatting — never decides unlock/lock (that is LevelSelectionPresenter/ILevelAccessProvider's job),
    /// and never touches save data itself, only the already-fetched LevelProgressSnapshot.</summary>
    public static class LevelStatusFormatter
    {
        private const string NotStartedText = "Not completed";
        private const string PerfectText = "Perfect";
        private const int PerfectStars = 3;

        /// <param name="maxBaseHealth">The level's LevelDefinition.BaseMaxHealth, needed to turn the saved
        /// absolute BestRemainingBaseHealth into the displayed percentage.</param>
        public static string Format(LevelProgressSnapshot progress, int maxBaseHealth)
        {
            if (!progress.IsCompleted)
            {
                return NotStartedText;
            }

            if (progress.BestStars >= PerfectStars)
            {
                return PerfectText;
            }

            int percent = maxBaseHealth > 0
                ? Mathf.Clamp(Mathf.RoundToInt(100f * progress.BestRemainingBaseHealth / maxBaseHealth), 0, 100)
                : 0;

            return $"HP remaining: {percent}%";
        }

        public static string FormatLockedRequirement(string previousLevelTitle)
        {
            return string.IsNullOrEmpty(previousLevelTitle)
                ? "Locked"
                : $"Complete {previousLevelTitle} to unlock.";
        }
    }
}
