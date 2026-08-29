namespace AlienDefense.Enemies
{
    /// <summary>Single source of truth for what each EnemyResolveReason grants. Tractor Beam refactor rule:
    /// UFO capture only removes the enemy from the battlefield — it must never grant reward. Only a Tower/Combat
    /// kill (Defeated) drops an EnergyPickup, and even then only Energy actually collected by the UFO (see
    /// AlienDefense.Pickups.EnergyCollectionService) grants Wallet/XP.
    ///
    /// Defeated     — Tower/Combat killed. Drops an EnergyPickup. No direct Energy/XP.
    /// Captured     — UFO tractor-beamed. No reward of any kind.
    /// ReachedBase  — damages the base. No reward.
    /// Removed      — cleanup (e.g. level teardown). No reward.
    /// LevelEnded   — cleanup. No reward.</summary>
    public static class EnemyResolutionPolicy
    {
        public static bool ShouldDropEnergy(EnemyResolveReason reason) => reason == EnemyResolveReason.Defeated;
    }
}
