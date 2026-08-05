namespace AlienDefense.Towers
{
    /// <summary>Immutable snapshot of one tower level's applied combat stats.</summary>
    public readonly struct TowerStats
    {
        public readonly float Damage;
        public readonly float Range;
        public readonly float AttacksPerSecond;
        public readonly float TurretRotationSpeed;

        public TowerStats(float damage, float range, float attacksPerSecond, float turretRotationSpeed)
        {
            Damage = damage;
            Range = range;
            AttacksPerSecond = attacksPerSecond;
            TurretRotationSpeed = turretRotationSpeed;
        }

        public static TowerStats FromLevel(TowerLevelData level)
        {
            return new TowerStats(level.Damage, level.Range, level.AttacksPerSecond, level.TurretRotationSpeed);
        }
    }
}
