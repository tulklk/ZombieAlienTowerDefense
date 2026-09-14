using System;
using System.Collections.Generic;
using AlienDefense.Core;
using AlienDefense.Economy;
using AlienDefense.Towers;

namespace AlienDefense.Building
{
    /// <summary>The Energy-Ball-funded counterpart to BuildService/TowerUpgradeService - same transaction shape
    /// (validate, spend, spawn/apply, roll back on partial failure) but charges EnergyWalletService instead of
    /// EconomyService, and reads TowerLevelData.EnergyCost instead of .UpgradeCost. Intended caller: the
    /// proximity-channel flow only (see PlayerBuildNodeProximityController) - never the tap/BuildBar path, which
    /// stays on Economy exactly as before.
    ///
    /// Energy can be paid into a node a little at a time: every channel pours whatever the player is carrying
    /// into the node (TryDeposit), and the node keeps it until its build/upgrade is paid in full. A build or
    /// upgrade spends the node's deposit first and only takes the rest from the wallet. A deposit is tied to the
    /// exact action it was paid toward (the node's tower and its level); if that action goes away some other way
    /// (tower sold, upgraded through the Economy path) the deposit is handed back to the wallet.</summary>
    public sealed class EnergyTowerTransactionService
    {
        private readonly EnergyWalletService _energyWallet;
        private readonly TowerFactory _towerFactory;
        private readonly GameFlowController _gameFlow;
        private readonly Dictionary<BuildNode, NodeDeposit> _deposits = new Dictionary<BuildNode, NodeDeposit>();

        private struct NodeDeposit
        {
            public int Amount;
            public TowerController Tower; // null while the node is Available (paying toward a build)
            public int LevelIndex;
        }

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

        /// <summary>Energy already paid into this node toward its current action (0 if none).</summary>
        public int GetDeposit(BuildNode node)
        {
            if (node == null || !_deposits.TryGetValue(node, out NodeDeposit deposit))
            {
                return 0;
            }

            if (IsDepositStale(node, deposit))
            {
                RefundDeposit(node);
                return 0;
            }

            return deposit.Amount;
        }

        /// <summary>Moves up to <paramref name="amount"/> Energy from the wallet into this node's deposit. Only
        /// for a node that has something to pay toward (Available, or Occupied below max level).</summary>
        public bool TryDeposit(BuildNode node, int amount)
        {
            if (node == null || amount <= 0 || _energyWallet == null || !_energyWallet.CanAfford(amount))
            {
                return false;
            }

            if (_gameFlow == null || !IsBuildableState(_gameFlow.CurrentState))
            {
                return false;
            }

            TowerController tower;
            if (node.State == BuildNodeState.Available)
            {
                tower = null;
            }
            else if (node.State == BuildNodeState.Occupied && node.CurrentTower != null && !node.CurrentTower.IsMaxLevel)
            {
                tower = node.CurrentTower;
            }
            else
            {
                return false;
            }

            int previous = GetDeposit(node);

            // Recorded before TrySpend so the EnergyChanged it raises already sees the new deposit on the badge.
            _deposits[node] = new NodeDeposit
            {
                Amount = previous + amount,
                Tower = tower,
                LevelIndex = tower != null ? tower.CurrentLevelIndex : -1,
            };

            if (!_energyWallet.TrySpend(amount))
            {
                RestoreDeposit(node, previous, tower);
                return false;
            }

            return true;
        }

        public bool CanAffordBuild(TowerDefinition definition)
        {
            return _energyWallet != null && _energyWallet.CurrentEnergy >= GetBuildCost(definition);
        }

        /// <summary>Whether this node's deposit plus the wallet covers building this tower here.</summary>
        public bool CanAffordBuild(BuildNode node, TowerDefinition definition)
        {
            return _energyWallet != null && GetDeposit(node) + _energyWallet.CurrentEnergy >= GetBuildCost(definition);
        }

        public bool CanAffordUpgrade(TowerController tower)
        {
            int cost = GetUpgradeCost(tower);
            return cost > 0 && _energyWallet != null && GetDeposit(OwnerNode(tower)) + _energyWallet.CurrentEnergy >= cost;
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
            if (!CanAffordBuild(node, definition))
            {
                return false;
            }

            TowerController tower = _towerFactory.Create(definition, node.BuildPoint.position, node.BuildPoint.rotation);
            if (tower == null)
            {
                return false;
            }

            if (!TryPay(node, null, cost))
            {
                _towerFactory.Destroy(tower);
                return false;
            }

            node.AssignTower(tower);
            tower.SetBuildNodeOwner(node);

            // Visual only - the tower is already fully active.
            if (tower.TryGetComponent(out TowerConstructionVFX construction))
            {
                construction.PlayConstruction();
            }

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
            if (cost <= 0 || !CanAffordUpgrade(tower))
            {
                return false;
            }

            BuildNode node = OwnerNode(tower);
            int depositBefore = GetDeposit(node);
            int walletBefore = _energyWallet.CurrentEnergy;
            if (!TryPay(node, tower, cost))
            {
                return false;
            }

            if (!tower.TryApplyNextLevel())
            {
                // Roll back exactly what TryPay took from each side.
                RestoreDeposit(node, depositBefore, tower);
                int takenFromWallet = walletBefore - _energyWallet.CurrentEnergy;
                if (takenFromWallet > 0)
                {
                    _energyWallet.Add(takenFromWallet);
                }
                else if (takenFromWallet < 0)
                {
                    _energyWallet.TrySpend(-takenFromWallet); // leftover deposit TryPay refunded
                }

                return false;
            }

            TowerUpgraded?.Invoke(tower);
            return true;
        }

        /// <summary>Pays <paramref name="cost"/> from the node's deposit first, the rest from the wallet. Any
        /// deposit left over (paid toward a pricier action than the one chosen) goes back to the wallet.</summary>
        private bool TryPay(BuildNode node, TowerController tower, int cost)
        {
            int deposit = GetDeposit(node);
            int fromWallet = Math.Max(0, cost - deposit);
            if (_energyWallet == null || _energyWallet.CurrentEnergy < fromWallet)
            {
                return false;
            }

            if (node != null)
            {
                _deposits.Remove(node);
            }

            if (fromWallet > 0 && !_energyWallet.TrySpend(fromWallet))
            {
                RestoreDeposit(node, deposit, tower);
                return false;
            }

            int leftover = deposit - (cost - fromWallet);
            if (leftover > 0)
            {
                _energyWallet.Add(leftover);
            }

            return true;
        }

        private void RestoreDeposit(BuildNode node, int amount, TowerController tower)
        {
            if (node == null)
            {
                return;
            }

            if (amount <= 0)
            {
                _deposits.Remove(node);
                return;
            }

            _deposits[node] = new NodeDeposit
            {
                Amount = amount,
                Tower = tower,
                LevelIndex = tower != null ? tower.CurrentLevelIndex : -1,
            };
        }

        private static bool IsDepositStale(BuildNode node, NodeDeposit deposit)
        {
            if (deposit.Tower == null)
            {
                return node.State != BuildNodeState.Available;
            }

            return node.State != BuildNodeState.Occupied
                || node.CurrentTower != deposit.Tower
                || deposit.Tower.IsSold
                || deposit.Tower.CurrentLevelIndex != deposit.LevelIndex;
        }

        private void RefundDeposit(BuildNode node)
        {
            if (!_deposits.TryGetValue(node, out NodeDeposit deposit))
            {
                return;
            }

            // Removed before Add, whose EnergyChanged re-reads every badge.
            _deposits.Remove(node);
            if (deposit.Amount > 0 && _energyWallet != null)
            {
                _energyWallet.Add(deposit.Amount);
            }
        }

        private static BuildNode OwnerNode(TowerController tower)
        {
            return tower != null ? tower.BuildNodeOwner as BuildNode : null;
        }

        private static bool IsBuildableState(GameState state)
        {
            return state == GameState.PreparingWave || state == GameState.PlayingWave;
        }
    }
}
