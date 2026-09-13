namespace AlienDefense.Waves
{
    /// <summary>Level-scoped spawn pacing / difficulty knobs passed into WaveController.</summary>
    public struct WaveSpawnSettings
    {
        public int MaxAliveEnemies;
        public float HealthPerWaveStep;
        public float SpeedPerWaveStep;
        public float DamagePerWaveStep;
        public float MaxSpeedMultiplier;
        public bool DebugWaveLogs;

        public static WaveSpawnSettings CreateDefault()
        {
            return new WaveSpawnSettings
            {
                MaxAliveEnemies = 0,
                HealthPerWaveStep = 0.10f,
                SpeedPerWaveStep = 0.015f,
                DamagePerWaveStep = 0.07f,
                MaxSpeedMultiplier = 1.22f,
                DebugWaveLogs = false,
            };
        }
    }
}
