using System;
using AlienDefense.Core;
using AlienDefense.Economy;
using AlienDefense.Towers;

namespace AlienDefense.Building
{
    /// <summary>Owns the sell transaction: pays out sell value, releases the BuildNode, and destroys the tower.
    /// Lives in Building (not Towers) because it needs BuildNode, which depends on Towers, not the other way around.</summary>
    public sealed class TowerSellService
    {
        private readonly EconomyService _economy;
        private readonly GameFlowController _gameFlow;
        private readonly TowerFactory _towerFactory;
        private readonly TowerSelectionService _selectionService;

        public event Action<TowerController> TowerSold;

        public TowerSellService(EconomyService economy, GameFlowController gameFlow, TowerFactory towerFactory, TowerSelectionService selectionService)
        {
            _economy = economy;
            _gameFlow = gameFlow;
            _towerFactory = towerFactory;
            _selectionService = selectionService;
        }

        public SellOperationResult TrySell(TowerController tower)
        {
            if (tower == null || tower.IsSold)
            {
                return Fail(SellResult.InvalidTower);
            }

            if (_gameFlow == null || !IsSellableState(_gameFlow.CurrentState))
            {
                return Fail(SellResult.GameNotPlaying);
            }

            var node = tower.BuildNodeOwner as BuildNode;
            if (node == null)
            {
                return Fail(SellResult.InvalidNode);
            }

            if (_towerFactory == null)
            {
                return Fail(SellResult.FactoryUnavailable);
            }

            int sellValue = SellValueCalculator.Calculate(tower.TotalInvestedResource, tower.Definition.SellPercentage);

            tower.MarkSold();
            _selectionService?.ClearIfSelected(tower);
            node.ReleaseTower();

            if (sellValue > 0 && _economy != null)
            {
                _economy.Add(sellValue);
            }

            TowerSold?.Invoke(tower);
            _towerFactory.Destroy(tower);

            return new SellOperationResult(SellResult.Success, sellValue, _economy != null ? _economy.CurrentResource : 0);
        }

        private static bool IsSellableState(GameState state)
        {
            return state == GameState.PreparingWave || state == GameState.PlayingWave;
        }

        private SellOperationResult Fail(SellResult status)
        {
            int remaining = _economy != null ? _economy.CurrentResource : 0;
            return new SellOperationResult(status, 0, remaining);
        }
    }
}
