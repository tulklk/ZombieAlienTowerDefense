using System.Collections.Generic;
using AlienDefense.Core;
using AlienDefense.Towers;
using AlienDefense.UI.MainMenu;
using UnityEngine;

namespace AlienDefense.UI.Defense
{
    // Intentionally NOT IApplicationServicesReceiver — see BaseScreenPresenter's doc comment: only one such
    // receiver per loaded scene gets pushed services, and MainMenuPresenter must be the one that wins that slot.
    // Services instead cascade in explicitly via MenuShellPresenter.Initialize.
    public sealed class DefenseScreenPresenter : MonoBehaviour
    {
        [SerializeField]
        private DefenseScreenView _view;

        [SerializeField]
        private TowerUnlockCardView _cardPrefab;

        [SerializeField]
        private MenuShellPresenter _menuShell;

        private readonly List<TowerUnlockCardView> _spawnedCards = new List<TowerUnlockCardView>();
        private readonly List<TowerDefinition> _cardDefinitions = new List<TowerDefinition>();

        private ApplicationServices _services;
        private TowerUnlockService _unlockService;
        private bool _isInitialized;

        public void ReceiveApplicationServices(ApplicationServices services)
        {
            _services = services;
            _unlockService = services.PlayerProfileService != null
                ? new TowerUnlockService(services.PlayerProfileService)
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
                Debug.LogError("[DefenseScreenPresenter] Missing TowerCatalog, view, or card prefab; no cards built.", this);
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

                TowerUnlockCardView card = Instantiate(_cardPrefab, _view.CardContainer);
                int capturedIndex = _cardDefinitions.Count;
                card.Clicked += () => HandleUnlockClicked(capturedIndex);

                _spawnedCards.Add(card);
                _cardDefinitions.Add(definition);
                RefreshCard(capturedIndex);
            }
        }

        private void HandleUnlockClicked(int index)
        {
            if (_unlockService == null || index < 0 || index >= _cardDefinitions.Count)
            {
                return;
            }

            if (_unlockService.TryUnlock(_cardDefinitions[index]))
            {
                RefreshCard(index);
                _menuShell?.RefreshHud();
            }
        }

        private void RefreshCard(int index)
        {
            if (_unlockService == null || index < 0 || index >= _cardDefinitions.Count)
            {
                return;
            }

            TowerDefinition definition = _cardDefinitions[index];
            TowerUnlockCardView card = _spawnedCards[index];

            bool isUnlocked = _unlockService.IsUnlocked(definition);
            bool canAfford = !isUnlocked && _services.PlayerProfileService.MetaCurrency >= definition.UnlockCost;

            card.Configure(definition.DisplayName, definition.Icon, definition.UnlockCost, isUnlocked, canAfford);
        }

        private void ClearCards()
        {
            foreach (TowerUnlockCardView card in _spawnedCards)
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
