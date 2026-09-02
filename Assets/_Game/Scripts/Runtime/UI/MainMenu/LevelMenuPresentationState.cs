using UnityEngine;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Pure display data for the currently selected campaign level on MainMenu. Built by
    /// MainMenuLevelSelectionPresenter from LevelCatalog/ILevelAccessProvider/PlayerProfileService — never
    /// exposes mutable save data, never computed inside a View.</summary>
    public sealed class LevelMenuPresentationState
    {
        public string LevelId { get; }
        public int DisplayIndex { get; }
        public string Title { get; }
        public string StatusText { get; }
        public bool IsUnlocked { get; }
        public bool IsCompleted { get; }
        public int BestStars { get; }
        public bool CanPlay { get; }
        public string LockedRequirementText { get; }
        public GameObject PreviewPrefab { get; }
        public LevelObjectivePresentation[] Objectives { get; }
        public bool HasPrevious { get; }
        public bool HasNext { get; }

        public LevelMenuPresentationState(
            string levelId,
            int displayIndex,
            string title,
            string statusText,
            bool isUnlocked,
            bool isCompleted,
            int bestStars,
            bool canPlay,
            string lockedRequirementText,
            GameObject previewPrefab,
            LevelObjectivePresentation[] objectives,
            bool hasPrevious,
            bool hasNext)
        {
            LevelId = levelId;
            DisplayIndex = displayIndex;
            Title = title;
            StatusText = statusText;
            IsUnlocked = isUnlocked;
            IsCompleted = isCompleted;
            BestStars = bestStars;
            CanPlay = canPlay;
            LockedRequirementText = lockedRequirementText;
            PreviewPrefab = previewPrefab;
            Objectives = objectives;
            HasPrevious = hasPrevious;
            HasNext = hasNext;
        }
    }
}
