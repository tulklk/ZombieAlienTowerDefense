namespace AlienDefense.Waves
{
    /// <summary>Lifecycle phase of the wave currently owned by WaveController.</summary>
    public enum WaveState
    {
        Idle = 0,
        Preparing = 1,
        Spawning = 2,
        WaitingForRemainingEnemies = 3,
        Completed = 4,
        Stopped = 5
    }
}
