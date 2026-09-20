using System;
using AlienDefense.Meta;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Legacy completion tint for connectors. Prefer <see cref="ObjectiveRewardUiState"/> for chests.</summary>
    public enum LevelObjectiveState
    {
        Locked,
        Incomplete,
        Completed
    }

    public readonly struct ObjectiveRewardPreviewEntry
    {
        public string ItemId { get; }
        public int Amount { get; }
        public bool IsMystery { get; }

        public ObjectiveRewardPreviewEntry(string itemId, int amount, bool isMystery)
        {
            ItemId = itemId;
            Amount = amount;
            IsMystery = isMystery;
        }
    }

    public readonly struct LevelObjectivePresentation
    {
        public LevelObjectiveKind Kind { get; }
        public string RequirementText { get; }
        public LevelObjectiveState State { get; }
        public ObjectiveRewardUiState RewardState { get; }
        public ObjectiveRewardPreviewEntry[] PreviewRewards { get; }

        public LevelObjectivePresentation(
            LevelObjectiveKind kind,
            string requirementText,
            LevelObjectiveState state,
            ObjectiveRewardUiState rewardState,
            ObjectiveRewardPreviewEntry[] previewRewards = null)
        {
            Kind = kind;
            RequirementText = requirementText;
            State = state;
            RewardState = rewardState;
            PreviewRewards = previewRewards ?? Array.Empty<ObjectiveRewardPreviewEntry>();
        }
    }
}
