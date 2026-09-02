namespace AlienDefense.UI.MainMenu
{
    /// <summary>Display-only state for one objective/chest slot. Distinct from any reward-claim state — this
    /// project has no reward/chest system yet, so objectives only ever show completion, never a Claim button.</summary>
    public enum LevelObjectiveState
    {
        Locked,
        Incomplete,
        Completed
    }

    public readonly struct LevelObjectivePresentation
    {
        public string RequirementText { get; }
        public LevelObjectiveState State { get; }

        public LevelObjectivePresentation(string requirementText, LevelObjectiveState state)
        {
            RequirementText = requirementText;
            State = state;
        }
    }
}
