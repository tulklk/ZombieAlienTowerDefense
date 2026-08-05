using System;
using AlienDefense.Core;
using AlienDefense.Economy;
using AlienDefense.Towers;

namespace AlienDefense.Building
{
    /// <summary>Owns the build transaction: validates, spawns via TowerFactory, charges Economy, and rolls back on partial failure.</summary>
    public sealed class BuildService
    {
        private readonly BuildSelectionService _selectionService;
        private readonly EconomyService _economy;
        private readonly TowerFactory _towerFactory;
        private readonly GameFlowController _gameFlow;

        public event Action<BuildNode, TowerController> BuildCompleted;

        public BuildService(BuildSelectionService selectionService, EconomyService economy, TowerFactory towerFactory, GameFlowController gameFlow)
        {
            _selectionService = selectionService;
            _economy = economy;
            _towerFactory = towerFactory;
            _gameFlow = gameFlow;
        }

        public BuildOperationResult TryBuild(BuildNode node)
        {
            if (_gameFlow == null || !IsBuildableState(_gameFlow.CurrentState))
            {
                return Fail(BuildResult.GameNotPlaying);
            }

            TowerDefinition definition = _selectionService != null ? _selectionService.SelectedTowerDefinition : null;
            if (definition == null)
            {
                return Fail(BuildResult.NoTowerSelected);
            }

            if (node == null)
            {
                return Fail(BuildResult.InvalidNode);
            }

            if (node.State != BuildNodeState.Available)
            {
                return Fail(BuildResult.NodeUnavailable);
            }

            if (_towerFactory == null)
            {
                return Fail(BuildResult.FactoryUnavailable);
            }

            if (_economy == null || !_economy.CanAfford(definition.BuildCost))
            {
                return Fail(BuildResult.NotEnoughResource);
            }

            TowerController tower = _towerFactory.Create(definition, node.BuildPoint.position, node.BuildPoint.rotation);
            if (tower == null)
            {
                return Fail(BuildResult.SpawnFailed);
            }

            if (!_economy.TrySpend(definition.BuildCost))
            {
                _towerFactory.Destroy(tower);
                return Fail(BuildResult.PaymentFailed);
            }

            node.AssignTower(tower);
            tower.SetBuildNodeOwner(node);
            tower.RegisterInvestment(definition.BuildCost);

            var result = new BuildOperationResult(BuildResult.Success, tower, _economy.CurrentResource);
            BuildCompleted?.Invoke(node, tower);
            return result;
        }

        private static bool IsBuildableState(GameState state)
        {
            return state == GameState.PreparingWave || state == GameState.PlayingWave;
        }

        private BuildOperationResult Fail(BuildResult status)
        {
            int remaining = _economy != null ? _economy.CurrentResource : 0;
            return new BuildOperationResult(status, null, remaining);
        }
    }
}
