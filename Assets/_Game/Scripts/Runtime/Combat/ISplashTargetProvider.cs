namespace AlienDefense.Combat
{
    /// <summary>Abstracts "every currently active combat target" for AreaDamageResolver, keeping Combat independent of Enemies.</summary>
    public interface ISplashTargetProvider
    {
        int Count { get; }
        ICombatTarget GetAt(int index);
    }
}
