using AlienDefense.Building;
using AlienDefense.Economy;
using AlienDefense.Towers;
using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Wires TowerDetailsView to TowerSelectionService, TowerUpgradeService, and TowerSellService.
    /// Never mutates a tower's level or economy directly; only calls the owning services.</summary>
    public sealed class TowerDetailsPresenter : MonoBehaviour
    {
        [SerializeField]
        private TowerDetailsView _view;

        private TowerSelectionService _selectionService;
        private EconomyService _economy;
        private TowerUpgradeService _upgradeService;
        private TowerSellService _sellService;
        private TowerController _currentTower;

        public void Initialize(TowerSelectionService selectionService, EconomyService economy, TowerUpgradeService upgradeService, TowerSellService sellService)
        {
            Unsubscribe();

            _selectionService = selectionService;
            _economy = economy;
            _upgradeService = upgradeService;
            _sellService = sellService;

            if (_selectionService != null)
            {
                _selectionService.SelectionChanged += HandleSelectionChanged;
            }

            if (_economy != null)
            {
                _economy.ResourceChanged += HandleResourceChanged;
            }

            if (_view != null)
            {
                _view.UpgradeClicked += HandleUpgradeClicked;
                _view.SellClicked += HandleSellClicked;
                _view.CloseClicked += HandleCloseClicked;
            }

            HandleSelectionChanged(_selectionService != null ? _selectionService.SelectedTower : null);
        }

        private void HandleSelectionChanged(TowerController tower)
        {
            if (_currentTower != null)
            {
                _currentTower.HideRangeIndicator();
                _currentTower.LevelChanged -= HandleTowerLevelChanged;
            }

            _currentTower = tower;

            if (_currentTower == null)
            {
                _view?.Hide();
                return;
            }

            _currentTower.LevelChanged += HandleTowerLevelChanged;
            _currentTower.ShowRangeIndicator();
            _view?.Show();
            Refresh();
        }

        private void HandleTowerLevelChanged(int levelIndex)
        {
            Refresh();
        }

        private void HandleResourceChanged(int currentResource)
        {
            if (_currentTower != null)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            if (_currentTower == null || _view == null)
            {
                return;
            }

            TowerDefinition definition = _currentTower.Definition;
            TowerStats stats = _currentTower.CurrentStats;

            _view.SetIdentity(definition.Icon, definition.DisplayName);
            _view.SetLevel(_currentTower.CurrentLevelNumber);
            _view.SetStats(stats.Damage, stats.Range, stats.AttacksPerSecond, definition.DefaultTargetingMode.ToString());

            bool isMaxLevel = _currentTower.IsMaxLevel;
            int upgradeCost = isMaxLevel ? 0 : definition.GetLevel(_currentTower.CurrentLevelIndex + 1).UpgradeCost;
            bool canAffordUpgrade = _economy != null && _economy.CanAfford(upgradeCost);
            _view.SetUpgradeState(isMaxLevel, upgradeCost, canAffordUpgrade);

            int sellValue = SellValueCalculator.Calculate(_currentTower.TotalInvestedResource, definition.SellPercentage);
            _view.SetSellValue(sellValue);
        }

        private void HandleUpgradeClicked()
        {
            if (_currentTower == null || _upgradeService == null)
            {
                return;
            }

            _upgradeService.TryUpgrade(_currentTower);
        }

        private void HandleSellClicked()
        {
            if (_currentTower == null || _sellService == null)
            {
                return;
            }

            _sellService.TrySell(_currentTower);
        }

        private void HandleCloseClicked()
        {
            _selectionService?.Clear();
        }

        private void Unsubscribe()
        {
            if (_currentTower != null)
            {
                _currentTower.HideRangeIndicator();
                _currentTower.LevelChanged -= HandleTowerLevelChanged;
                _currentTower = null;
            }

            if (_selectionService != null)
            {
                _selectionService.SelectionChanged -= HandleSelectionChanged;
            }

            if (_economy != null)
            {
                _economy.ResourceChanged -= HandleResourceChanged;
            }

            if (_view != null)
            {
                _view.UpgradeClicked -= HandleUpgradeClicked;
                _view.SellClicked -= HandleSellClicked;
                _view.CloseClicked -= HandleCloseClicked;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
