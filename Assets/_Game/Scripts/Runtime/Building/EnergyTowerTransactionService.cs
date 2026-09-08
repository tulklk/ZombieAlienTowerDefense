using System;
using AlienDefense.Core;
using AlienDefense.Economy;
using AlienDefense.Towers;

namespace AlienDefense.Building
{
    /// <summary>The Energy-Ball-funded counterpart to BuildService/TowerUpgradeService - same transaction shape
    /// (validate, spend, spawn/apply, roll back on partial failure) but charges EnergyWalletService instead of
    /// EconomyService, and reads TowerLevelData.EnergyCost instead of .UpgradeCost. Intended caller: the
    /// proximity-channel flow only (see PlayerBuildNodeProximityController) - never the tap/BuildBar path, which
    /// stays on Economy exactly as before.</summary>
    public sealed class EnergyTowerTransactionService
    {
        private readonly EnergyWalletService _energyWallet;
        private readonly TowerFactory _towerFactory;
        private readonly GameFlowController _gameFlow;

        public event Action<BuildNode, TowerController> BuildCompleted;
        public event Action<TowerController> TowerUpgraded;

        public EnergyTowerTransactionService(EnergyWalletService energyWallet, TowerFactory towerFactory, GameFlowController gameFlow)
        {
            _energyWallet = energyWallet;
            _towerFactory = towerFactory;
            _gameFlow = gameFlow;
        }

        public int CurrentWalletEnergy => _energyWallet != null ? _energyWallet.CurrentEnergy : 0;

        /// <summary>The Energy cost to build a fresh tower of this definition - always its level-1 cost.</summary>
        public int GetBuildCost(TowerDefinition definition)
        {
            return definition != null && definition.LevelCount > 0 ? definition.GetLevel(0).EnergyCost : 0;
        }

        /// <summary>The Energy cost for this tower's NEXT level, or 0 if already at max level.</summary>
        public int GetUpgradeCost(TowerController tower)
        {
            if (tower == null || tower.IsMaxLevel || tower.Definition == null)
            {
                return 0;
            }

            int nextLevelIndex = tower.CurrentLevelIndex + 1;
            return nextLevelIndex < tower.Definition.LevelCount ? tower.Definition.GetLevel(nextLevelIndex).EnergyCost : 0;
        }

        public bool CanAffordBuild(TowerDefinition definition)
        {
            return _energyWallet != null && _energyWallet.CurrentEnergy >= GetBuildCost(definition);
        }

        public bool CanAffordUpgrade(TowerController tower)
        {
            int cost = GetUpgradeCost(tower);
            return cost > 0 && _energyWallet != null && _energyWallet.CurrentEnergy >= cost;
        }

        public bool TryBuild(BuildNode node, TowerDefinition definition)
        {
            if (_gameFlow == null || !IsBuildableState(_gameFlow.CurrentState))
            {
                return false;
            }

            if (node == null || node.State != BuildNodeState.Available || definition == null || _towerFactory == null)
            {
                return false;
            }

            int cost = GetBuildCost(definition);
            if (_energyWallet == null || _energyWallet.CurrentEnergy < cost)
            {
                return false;
            }

            TowerController tower = _towerFactory.Create(definition, node.BuildPoint.position, node.BuildPoint.rotation);
            if (tower == null)
            {
                return false;
            }

            if (!_energyWallet.TrySpend(cost))
            {
                _towerFactory.Destroy(tower);
                return false;
            }

            node.AssignTower(tower);
            tower.SetBuildNodeOwner(node);

            BuildCompleted?.Invoke(node, tower);
            return true;
        }

        public bool TryUpgrade(TowerController tower)
        {
            if (_gameFlow == null || !IsBuildableState(_gameFlow.CurrentState))
            {
                return false;
            }

            if (tower == null || tower.IsSold || tower.IsMaxLevel)
            {
                return false;
            }

            int cost = GetUpgradeCost(tower);
            if (cost <= 0 || _energyWallet == null || _energyWallet.CurrentEnergy < cost)
            {
                return false;
            }

            if (!_energyWallet.TrySpend(cost))
            {
                return false;
            }

            if (!tower.TryApplyNextLevel())
            {
                _energyWallet.Add(cost);
                return false;
            }

            TowerUpgraded?.Invoke(tower);
            return true;
        }

        private static bool IsBuildableState(GameState state)
        {
            return state == GameState.PreparingWave || state == GameState.PlayingWave;
        }
    }
}
