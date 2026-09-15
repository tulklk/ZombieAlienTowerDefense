namespace AlienDefense.Combat
{
    /// <summary>Behavioral category of a status effect, used to route it to the right EnemyStatusController logic.</summary>
    public enum StatusEffectType
    {
        Slow = 0,
        Burn = 1,

        /// <summary>Movement fully stopped while active (on top of, not replacing, any Slow).</summary>
        Stun = 2
    }
}
