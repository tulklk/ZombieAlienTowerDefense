namespace AlienDefense.Enemies
{
    /// <summary>Per-spawn multipliers applied to an enemy instance (never written back to the prefab/definition).</summary>
    public readonly struct EnemySpawnModifiers
    {
        public readonly float HealthMultiplier;
        public readonly float SpeedMultiplier;
        public readonly float DamageMultiplier;

        public EnemySpawnModifiers(float healthMultiplier, float speedMultiplier, float damageMultiplier)
        {
            HealthMultiplier = healthMultiplier > 0f ? healthMultiplier : 1f;
            SpeedMultiplier = speedMultiplier > 0f ? speedMultiplier : 1f;
            DamageMultiplier = damageMultiplier > 0f ? damageMultiplier : 1f;
        }

        public static EnemySpawnModifiers Identity { get; } = new EnemySpawnModifiers(1f, 1f, 1f);
    }
}
