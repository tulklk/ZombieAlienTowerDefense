namespace AlienDefense.Enemies
{
    /// <summary>Per-spawn multipliers applied to an enemy instance (never written back to the prefab/definition).</summary>
    public readonly struct EnemySpawnModifiers
    {
        public readonly float HealthMultiplier;
        public readonly float SpeedMultiplier;
        public readonly float DamageMultiplier;

        /// <summary>Uniform scale on top of the prefab's own scale (mini bosses). Applied by EnemyController on every
        /// spawn, so a pooled instance never keeps a previous life's size. 0 or less is treated as 1.</summary>
        public readonly float ScaleMultiplier;

        public EnemySpawnModifiers(float healthMultiplier, float speedMultiplier, float damageMultiplier)
            : this(healthMultiplier, speedMultiplier, damageMultiplier, 1f)
        {
        }

        public EnemySpawnModifiers(float healthMultiplier, float speedMultiplier, float damageMultiplier, float scaleMultiplier)
        {
            HealthMultiplier = healthMultiplier > 0f ? healthMultiplier : 1f;
            SpeedMultiplier = speedMultiplier > 0f ? speedMultiplier : 1f;
            DamageMultiplier = damageMultiplier > 0f ? damageMultiplier : 1f;
            ScaleMultiplier = scaleMultiplier > 0f ? scaleMultiplier : 1f;
        }

        public static EnemySpawnModifiers Identity { get; } = new EnemySpawnModifiers(1f, 1f, 1f, 1f);
    }
}
