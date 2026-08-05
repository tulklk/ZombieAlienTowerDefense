namespace AlienDefense.Combat
{
    /// <summary>Anything that can receive damage.</summary>
    public interface IDamageable
    {
        bool IsDamageable { get; }
        bool TryApplyDamage(in DamageInfo damageInfo);
    }
}
