using AlienDefense.Building;
using AlienDefense.Economy;
using AlienDefense.Towers;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Wires a fixed set of tower build buttons to BuildSelectionService and Economy. Never spawns towers, never spends resource.</summary>
    public sealed class BuildBarPresenter : MonoBehaviour
    {
        [SerializeField]
        private TowerDefinition[] _towerDefinitions;

        [SerializeField]
        private TowerBuildButtonView[] _buttonViews;

        [SerializeField]
        [Tooltip("Optional.")]
        private Button _cancelButton;

        private EconomyService _economy;
        private BuildSelectionService _selectionService;

        public void Initialize(EconomyService economy, BuildSelectionService selectionService)
        {
            Unsubscribe();

            _economy = economy;
            _selectionService = selectionService;

            for (int i = 0; i < _buttonViews.Length && i < _towerDefinitions.Length; i++)
            {
                _buttonViews[i].SetCost(_towerDefinitions[i].BuildCost);
                _buttonViews[i].SetIcon(_towerDefinitions[i].Icon);
                _buttonViews[i].Clicked += HandleButtonClicked;
            }

            if (_cancelButton != null)
            {
                _cancelButton.onClick.AddListener(HandleCancelClicked);
            }

            if (_economy != null)
            {
                _economy.ResourceChanged += HandleResourceChanged;
            }

            if (_selectionService != null)
            {
                _selectionService.SelectedTowerChanged += HandleSelectedTowerChanged;
            }

            RefreshAffordability();
            RefreshSelection(_selectionService != null ? _selectionService.SelectedTowerDefinition : null);
        }

        private void HandleButtonClicked(TowerBuildButtonView sender)
        {
            int index = System.Array.IndexOf(_buttonViews, sender);
            if (index < 0 || index >= _towerDefinitions.Length)
            {
                return;
            }

            _selectionService?.SelectTower(_towerDefinitions[index]);
        }

        private void HandleCancelClicked()
        {
            _selectionService?.ClearSelection();
        }

        private void HandleResourceChanged(int currentResource)
        {
            RefreshAffordability();
        }

        private void HandleSelectedTowerChanged(TowerDefinition selected)
        {
            RefreshSelection(selected);
        }

        private void RefreshAffordability()
        {
            if (_economy == null)
            {
                return;
            }

            for (int i = 0; i < _buttonViews.Length && i < _towerDefinitions.Length; i++)
            {
                _buttonViews[i].SetAffordable(_economy.CanAfford(_towerDefinitions[i].BuildCost));
            }
        }

        private void RefreshSelection(TowerDefinition selected)
        {
            for (int i = 0; i < _buttonViews.Length && i < _towerDefinitions.Length; i++)
            {
                _buttonViews[i].SetSelected(_towerDefinitions[i] == selected);
            }
        }

        private void Unsubscribe()
        {
            for (int i = 0; i < _buttonViews.Length; i++)
            {
                if (_buttonViews[i] != null)
                {
                    _buttonViews[i].Clicked -= HandleButtonClicked;
                }
            }

            if (_cancelButton != null)
            {
                _cancelButton.onClick.RemoveListener(HandleCancelClicked);
            }

            if (_economy != null)
            {
                _economy.ResourceChanged -= HandleResourceChanged;
            }

            if (_selectionService != null)
            {
                _selectionService.SelectedTowerChanged -= HandleSelectedTowerChanged;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
