namespace AlienDefense.Towers
{
    /// <summary>Which IAttackStrategy a TowerDefinition resolves to via AttackStrategyFactory.</summary>
    public enum TowerAttackBehavior
    {
        Standard = 0,
        Status = 1,
        Splash = 2
    }
}
