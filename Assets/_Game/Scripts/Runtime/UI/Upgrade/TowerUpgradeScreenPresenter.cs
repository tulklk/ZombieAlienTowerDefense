using System.Collections.Generic;
using AlienDefense.Core;
using AlienDefense.Towers;
using AlienDefense.UI.MainMenu;
using UnityEngine;

namespace AlienDefense.UI.Upgrade
{
    // Intentionally NOT IApplicationServicesReceiver — see BaseScreenPresenter's doc comment: only one such
    // receiver per loaded scene gets pushed services, and MainMenuPresenter must be the one that wins that slot.
    // Services instead cascade in explicitly via MenuShellPresenter.Initialize.
    public sealed class TowerUpgradeScreenPresenter : MonoBehaviour
    {
        [SerializeField]
        private TowerUpgradeScreenView _view;

        [SerializeField]
        private TowerUpgradeCardView _cardPrefab;

        [SerializeField]
        private MenuShellPresenter _menuShell;

        private readonly List<TowerUpgradeCardView> _spawnedCards = new List<TowerUpgradeCardView>();
        private readonly List<TowerDefinition> _cardDefinitions = new List<TowerDefinition>();

        private ApplicationServices _services;
        private TowerMetaUpgradeService _upgradeService;
        private bool _isInitialized;

        public void ReceiveApplicationServices(ApplicationServices services)
        {
            _services = services;
            _upgradeService = services.PlayerProfileService != null
                ? new TowerMetaUpgradeService(services.PlayerProfileService)
                : null;

            if (!_isInitialized)
            {
                BuildCards();
                _isInitialized = true;
            }

            Refresh();
        }

        public void Refresh()
        {
            if (!_isInitialized)
            {
                return;
            }

            for (int i = 0; i < _cardDefinitions.Count; i++)
            {
                RefreshCard(i);
            }
        }

        private void BuildCards()
        {
            ClearCards();

            if (_services?.TowerCatalog == null || _view == null || _cardPrefab == null)
            {
                Debug.LogError("[TowerUpgradeScreenPresenter] Missing TowerCatalog, view, or card prefab; no cards built.", this);
                return;
            }

            int count = _services.TowerCatalog.Count;
            for (int i = 0; i < count; i++)
            {
                TowerDefinition definition = _services.TowerCatalog.GetTower(i);
                if (definition == null)
                {
                    continue;
                }

                TowerUpgradeCardView card = Instantiate(_cardPrefab, _view.CardContainer);
                int capturedIndex = _cardDefinitions.Count;
                card.Clicked += () => HandleUpgradeClicked(capturedIndex);

                _spawnedCards.Add(card);
                _cardDefinitions.Add(definition);
                RefreshCard(capturedIndex);
            }
        }

        private void HandleUpgradeClicked(int index)
        {
            if (_upgradeService == null || index < 0 || index >= _cardDefinitions.Count)
            {
                return;
            }

            if (_upgradeService.TryUpgrade(_cardDefinitions[index]))
            {
                RefreshCard(index);
                _menuShell?.RefreshHud();
            }
        }

        private void RefreshCard(int index)
        {
            if (_upgradeService == null || index < 0 || index >= _cardDefinitions.Count)
            {
                return;
            }

            TowerDefinition definition = _cardDefinitions[index];
            TowerUpgradeCardView card = _spawnedCards[index];

            bool isUnlocked = _services.PlayerProfileService == null || _services.PlayerProfileService.IsTowerUnlocked(definition.Id);
            bool isMaxLevel = _upgradeService.IsMaxLevel(definition);
            _upgradeService.TryGetNextLevelCost(definition, out int nextCost);
            bool canAfford = _upgradeService.CanAffordNextLevel(definition);

            card.Configure(
                definition.DisplayName,
                definition.Icon,
                _upgradeService.GetCurrentLevelIndex(definition) + 1,
                definition.LevelCount,
                nextCost,
                isMaxLevel,
                !isUnlocked,
                canAfford);
        }

        private void ClearCards()
        {
            foreach (TowerUpgradeCardView card in _spawnedCards)
            {
                if (card != null)
                {
                    Destroy(card.gameObject);
                }
            }

            _spawnedCards.Clear();
            _cardDefinitions.Clear();
        }

        private void OnDestroy()
        {
            ClearCards();
        }
    }
}
