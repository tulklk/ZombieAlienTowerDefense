namespace AlienDefense.Waves
{
    /// <summary>Immutable snapshot of one wave's runtime progress, for UI/event consumers.</summary>
    public readonly struct WaveProgressSnapshot
    {
        public readonly int WaveNumber;
        public readonly int TotalWaves;
        public readonly int PlannedEnemyCount;
        public readonly int SuccessfulSpawnCount;
        public readonly int FailedSpawnCount;
        public readonly int ActiveEnemyCount;
        public readonly int ResolvedEnemyCount;
        public readonly float NormalizedProgress;

        public WaveProgressSnapshot(
            int waveNumber,
            int totalWaves,
            int plannedEnemyCount,
            int successfulSpawnCount,
            int failedSpawnCount,
            int activeEnemyCount,
            int resolvedEnemyCount,
            float normalizedProgress)
        {
            WaveNumber = waveNumber;
            TotalWaves = totalWaves;
            PlannedEnemyCount = plannedEnemyCount;
            SuccessfulSpawnCount = successfulSpawnCount;
            FailedSpawnCount = failedSpawnCount;
            ActiveEnemyCount = activeEnemyCount;
            ResolvedEnemyCount = resolvedEnemyCount;
            NormalizedProgress = normalizedProgress;
        }
    }
}
