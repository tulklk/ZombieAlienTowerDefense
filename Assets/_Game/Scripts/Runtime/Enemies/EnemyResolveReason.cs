namespace AlienDefense.Enemies
{
    /// <summary>Why an enemy left play; determines what side effects Resolve applies.</summary>
    public enum EnemyResolveReason
    {
        Defeated = 0,
        ReachedBase = 1,
        Removed = 2,
        LevelEnded = 3
    }
}
