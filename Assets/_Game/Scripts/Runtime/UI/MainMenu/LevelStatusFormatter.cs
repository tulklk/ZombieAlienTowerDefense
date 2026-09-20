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
        private const string ColorLow = "#FF9B3D";
        private const string ColorMid = "#FFD43B";
        private const string ColorPerfect = "#45E55B";

        /// <param name="maxBaseHealth">Fallback when BestRemainingHpPercent is unset: turn absolute
        /// BestRemainingBaseHealth into a percentage.</param>
        public static string Format(LevelProgressSnapshot progress, int maxBaseHealth)
        {
            if (!progress.IsCompleted)
            {
                return NotStartedText;
            }

            int percent = ResolveBestPercent(progress, maxBaseHealth);
            string color = PercentColorHex(percent);
            return $"Remaining HP: <color={color}>{percent}%</color>";
        }

        public static int ResolveBestPercent(LevelProgressSnapshot progress, int maxBaseHealth)
        {
            if (progress.BestRemainingHpPercent > 0)
            {
                return Mathf.Clamp(progress.BestRemainingHpPercent, 0, 100);
            }

            if (maxBaseHealth > 0)
            {
                return Mathf.Clamp(
                    Mathf.RoundToInt(100f * progress.BestRemainingBaseHealth / maxBaseHealth),
                    0,
                    100);
            }

            return 0;
        }

        public static string PercentColorHex(int percent)
        {
            if (percent >= 100)
            {
                return ColorPerfect;
            }

            if (percent >= 50)
            {
                return ColorMid;
            }

            return ColorLow;
        }

        public static string FormatLockedRequirement(string previousLevelTitle)
        {
            return string.IsNullOrEmpty(previousLevelTitle)
                ? "Locked"
                : $"Complete {previousLevelTitle} to unlock.";
        }
    }
}
