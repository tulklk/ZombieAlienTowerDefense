namespace AlienDefense.Combat
{
    /// <summary>Which floating damage number (if any) a hit shows over the enemy it lands on. Carried on DamageInfo,
    /// separate from DamageType so choosing a look never changes armour/shield rules.</summary>
    public enum DamagePopupStyle
    {
        None = 0,
        Fire = 1,
    }
}
