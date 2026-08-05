namespace AlienDefense.Core
{
    /// <summary>
    /// High-level phases of a single level playthrough.
    /// Owned exclusively by <see cref="GameFlowController"/>; no other system should
    /// maintain its own copy of "what phase are we in".
    /// </summary>
    public enum GameState
    {
        Initializing = 0,
        PreparingWave = 1,
        PlayingWave = 2,
        Paused = 3,
        Victory = 4,
        Defeat = 5
    }
}
