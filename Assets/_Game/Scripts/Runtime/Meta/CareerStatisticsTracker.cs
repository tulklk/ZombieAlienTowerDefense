using System;
using AlienDefense.Enemies;
using AlienDefense.Player;
using AlienDefense.Save;
using AlienDefense.Towers;
using UnityEngine;

namespace AlienDefense.Meta
{
    /// <summary>Pushes career stats into PlayerProfileService during a match. Matches CombatStatsService's
    /// source rules (tower or UFO only) and counts Defeated resolves once per enemy Resolve call.</summary>
    public sealed class CareerStatisticsTracker : IDisposable
    {
        private readonly PlayerProfileService _profile;
        private bool _disposed;

        public CareerStatisticsTracker(PlayerProfileService profile)
        {
            _profile = profile;
            if (_profile == null)
            {
                return;
            }

            EnemyHealth.DamageApplied += HandleDamageApplied;
            EnemyController.AnyResolved += HandleEnemyResolved;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            EnemyHealth.DamageApplied -= HandleDamageApplied;
            EnemyController.AnyResolved -= HandleEnemyResolved;
        }

        private void HandleDamageApplied(GameObject source, float amount)
        {
            if (_profile == null || source == null || amount <= 0f)
            {
                return;
            }

            if (source.GetComponentInParent<TowerController>() == null &&
                source.GetComponentInParent<PlayerController>() == null)
            {
                return;
            }

            long applied = Mathf.RoundToInt(amount);
            if (applied > 0)
            {
                _profile.AddTowerDamage(applied);
            }
        }

        private void HandleEnemyResolved(EnemyController enemy, EnemyResolveReason reason)
        {
            if (_profile == null || enemy == null || reason != EnemyResolveReason.Defeated)
            {
                return;
            }

            bool isBoss = enemy.BossController != null;
            _profile.RegisterZombieKill(isBoss);
        }
    }
}
