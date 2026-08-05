using System;
using AlienDefense.Core;
using AlienDefense.Economy;

namespace AlienDefense.Towers
{
    /// <summary>Owns the upgrade transaction: validates, charges Economy, applies the next level, and refunds on failure.</summary>
    public sealed class TowerUpgradeService
    {
        private readonly EconomyService _economy;
        private readonly GameFlowController _gameFlow;

        public event Action<TowerController> TowerUpgraded;

        public TowerUpgradeService(EconomyService economy, GameFlowController gameFlow)
        {
            _economy = economy;
            _gameFlow = gameFlow;
        }

        public UpgradeOperationResult TryUpgrade(TowerController tower)
        {
            if (tower == null || tower.IsSold)
            {
                return Fail(UpgradeResult.InvalidTower);
            }

            if (_gameFlow == null || !IsUpgradableState(_gameFlow.CurrentState))
            {
                return Fail(UpgradeResult.GameNotPlaying);
            }

            if (tower.IsMaxLevel)
            {
                return Fail(UpgradeResult.AlreadyMaxLevel);
            }

            TowerDefinition definition = tower.Definition;
            int nextLevelIndex = tower.CurrentLevelIndex + 1;
            if (definition == null || nextLevelIndex >= definition.LevelCount)
            {
                return Fail(UpgradeResult.InvalidConfiguration);
            }

            int cost = definition.GetLevel(nextLevelIndex).UpgradeCost;

            if (_economy == null || !_economy.CanAfford(cost))
            {
                return Fail(UpgradeResult.NotEnoughResource);
            }

            if (!_economy.TrySpend(cost))
            {
                return Fail(UpgradeResult.PaymentFailed);
            }

            if (!tower.TryApplyNextLevel())
            {
                _economy.Add(cost);
                return Fail(UpgradeResult.ApplyFailed);
            }

            tower.RegisterInvestment(cost);

            var result = new UpgradeOperationResult(UpgradeResult.Success, tower.CurrentLevelIndex, _economy.CurrentResource);
            TowerUpgraded?.Invoke(tower);
            return result;
        }

        private static bool IsUpgradableState(GameState state)
        {
            return state == GameState.PreparingWave || state == GameState.PlayingWave;
        }

        private UpgradeOperationResult Fail(UpgradeResult status)
        {
            int remaining = _economy != null ? _economy.CurrentResource : 0;
            return new UpgradeOperationResult(status, -1, remaining);
        }
    }
}
