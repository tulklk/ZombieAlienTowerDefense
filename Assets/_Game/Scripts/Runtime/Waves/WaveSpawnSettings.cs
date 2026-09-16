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

        /// <summary>Flat per-level scale on every enemy's health / move speed / base damage (boss and escorts
        /// included), applied on top of the per-wave growth. 0 or less is treated as 1.</summary>
        public float LevelHealthMultiplier;
        public float LevelSpeedMultiplier;
        public float LevelDamageMultiplier;

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
                LevelHealthMultiplier = 1f,
                LevelSpeedMultiplier = 1f,
                LevelDamageMultiplier = 1f,
            };
        }
    }
}
