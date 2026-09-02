using System;
using System.Collections.Generic;
using AlienDefense.Core;
using AlienDefense.Economy;
using AlienDefense.UI.MainMenu;
using UnityEngine;

namespace AlienDefense.UI.Shop
{
    /// <summary>Shop tab presenter. Builds exchange cards and drives purchases through ShopExchangeService.</summary>
    // Intentionally NOT IApplicationServicesReceiver — see BaseScreenPresenter's doc comment: only one such
    // receiver per loaded scene gets pushed services, and MainMenuPresenter must be the one that wins that slot.
    // Services instead cascade in explicitly via MenuShellPresenter.Initialize.
    public sealed class ShopScreenPresenter : MonoBehaviour
    {
        [SerializeField]
        private ShopScreenView _view;

        [SerializeField]
        private ShopExchangeCardView _cardPrefab;

        [SerializeField]
        private MenuShellPresenter _menuShell;

        private readonly List<ShopExchangeCardView> _spawnedCards = new List<ShopExchangeCardView>();

        private ApplicationServices _services;
        private ShopExchangeService _exchangeService;
        private bool _isInitialized;

        public void ReceiveApplicationServices(ApplicationServices services)
        {
            _services = services;
            _exchangeService = services.PlayerProfileService != null
                ? new ShopExchangeService(services.PlayerProfileService)
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

            for (int i = 0; i < _spawnedCards.Count; i++)
            {
                RefreshCard(i);
            }
        }

        private void BuildCards()
        {
            ClearCards();

            if (_view == null || _cardPrefab == null)
            {
                Debug.LogError("[ShopScreenPresenter] Missing view or card prefab; no cards built.", this);
                return;
            }

            for (int i = 0; i < ShopExchangeTable.Packs.Length; i++)
            {
                ShopExchangeCardView card = Instantiate(_cardPrefab, _view.ExchangeCardContainer);
                int capturedIndex = i;
                card.Clicked += () => HandleExchangeClicked(capturedIndex);
                _spawnedCards.Add(card);
                RefreshCard(capturedIndex);
            }
        }

        private void HandleExchangeClicked(int index)
        {
            if (_exchangeService == null)
            {
                return;
            }

            if (_exchangeService.TryExchange(index))
            {
                Refresh();
                _menuShell?.RefreshHud();
            }
        }

        private void RefreshCard(int index)
        {
            if (_exchangeService == null || index < 0 || index >= _spawnedCards.Count)
            {
                return;
            }

            GemToCoinPack pack = ShopExchangeTable.Packs[index];
            _spawnedCards[index].Configure(pack.CoinAmount, pack.GemCost, _exchangeService.CanAfford(index));
        }

        private void ClearCards()
        {
            foreach (ShopExchangeCardView card in _spawnedCards)
            {
                if (card != null)
                {
                    Destroy(card.gameObject);
                }
            }

            _spawnedCards.Clear();
        }

        private void OnDestroy()
        {
            ClearCards();
        }
    }
}
