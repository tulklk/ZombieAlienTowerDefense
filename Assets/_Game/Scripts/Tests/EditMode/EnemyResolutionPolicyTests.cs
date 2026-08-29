using AlienDefense.Enemies;
using NUnit.Framework;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>Locks down the exact policy table from the Tractor Beam refactor: only a Tower/Combat kill
    /// (Defeated) ever drops an EnergyPickup. Every other resolution — most importantly Captured (UFO) — never
    /// does, so this is the single place that regresses loudly if that boundary is ever blurred again.</summary>
    public class EnemyResolutionPolicyTests
    {
        [Test]
        public void ShouldDropEnergy_TrueOnlyForDefeated()
        {
            Assert.IsTrue(EnemyResolutionPolicy.ShouldDropEnergy(EnemyResolveReason.Defeated));
            Assert.IsFalse(EnemyResolutionPolicy.ShouldDropEnergy(EnemyResolveReason.Captured), "UFO capture must never drop Energy.");
            Assert.IsFalse(EnemyResolutionPolicy.ShouldDropEnergy(EnemyResolveReason.ReachedBase));
            Assert.IsFalse(EnemyResolutionPolicy.ShouldDropEnergy(EnemyResolveReason.Removed));
            Assert.IsFalse(EnemyResolutionPolicy.ShouldDropEnergy(EnemyResolveReason.LevelEnded));
        }
    }
}
