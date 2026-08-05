namespace AlienDefense.Core
{
    /// <summary>High-level phases of a single level playthrough.</summary>
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
