namespace AlienDefense.Enemies
{
    /// <summary>How important a spawned enemy is to the level, independent of its EnemyDefinition (a mini boss is
    /// a normal zombie type spawned bigger and stronger). Set by WaveController on spawn; read by UI such as the
    /// minimap. Reset to Normal on every spawn.</summary>
    public enum EnemyRank
    {
        Normal = 0,
        MiniBoss = 1,
        FinalBoss = 2
    }
}
