namespace AlienDefense.Progression
{
    /// <summary>Answers whether a campaign level is playable yet. Phase 14 will replace the default
    /// implementation with one backed by real save progression; Level Selection only depends on this interface.</summary>
    public interface ILevelAccessProvider
    {
        bool IsUnlocked(string levelId);
    }
}
