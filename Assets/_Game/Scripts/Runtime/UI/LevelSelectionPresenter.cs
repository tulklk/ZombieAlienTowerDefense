using System.Collections.Generic;
using AlienDefense.Core;
using AlienDefense.Progression;
using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Level Selection's scene entry point. Builds one LevelCardView per LevelCatalog entry exactly once
    /// (not per frame), asks ILevelAccessProvider for lock state, and routes selection to SceneTransitionService.</summary>
    public sealed class LevelSelectionPresenter : MonoBehaviour, IApplicationServicesReceiver
    {
        [SerializeField]
        private LevelSelectionView _view;

        [SerializeField]
        private LevelCardView _cardPrefab;

        [SerializeField]
        [Tooltip("Optional.")]
        private BackNavigationController _backNavigation;

        private readonly List<LevelCardView> _spawnedCards = new List<LevelCardView>();

        private ApplicationServices _services;
        private ILevelAccessProvider _accessProvider;

        public void ReceiveApplicationServices(ApplicationServices services)
        {
            _services = services;
            _accessProvider = services.PlayerProfileService != null
                ? new ProfileLevelAccessProvider(services.PlayerProfileService, services.LevelCatalog)
                : new DefaultLevelAccessProvider(services.LevelCatalog);

            if (_view != null)
            {
                _view.BackClicked += HandleBackClicked;
            }

            _backNavigation?.SetHandler(HandleBackClicked);

            BuildCards();
        }

        private void BuildCards()
        {
            ClearCards();

            if (_services?.LevelCatalog == null || _view == null || _cardPrefab == null)
            {
                Debug.LogError("[LevelSelectionPresenter] Missing LevelCatalog, view, or card prefab; no cards built.", this);
                return;
            }

            int count = _services.LevelCatalog.Count;
            for (int i = 0; i < count; i++)
            {
                LevelCatalogEntry entry = _services.LevelCatalog.GetEntry(i);
                if (entry == null || entry.LevelDefinition == null)
                {
                    continue;
                }

                LevelCardView card = Instantiate(_cardPrefab, _view.CardContainer);
                bool unlocked = _accessProvider.IsUnlocked(entry.LevelId);
                int bestStars = _services.PlayerProfileService != null ? _services.PlayerProfileService.GetLevelProgress(entry.LevelId).BestStars : 0;
                card.Configure("Level " + (i + 1), null, !unlocked, bestStars);
                card.Selected += () => HandleCardSelected(entry);
                _spawnedCards.Add(card);
            }
        }

        private void HandleCardSelected(LevelCatalogEntry entry)
        {
            if (_accessProvider == null || !_accessProvider.IsUnlocked(entry.LevelId))
            {
                return;
            }

            _services?.LevelLaunchContext.SetSelectedLevel(entry.LevelId);
            _services?.SceneTransition.TryLoadSceneViaBootstrap(entry.SceneName);
        }

        private void HandleBackClicked()
        {
            _services?.LevelLaunchContext.Clear();
            _services?.SceneTransition.TryLoadSceneViaBootstrap(SceneNames.MainMenu);
        }

        private void ClearCards()
        {
            foreach (LevelCardView card in _spawnedCards)
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
            if (_view != null)
            {
                _view.BackClicked -= HandleBackClicked;
            }

            ClearCards();
        }
    }
}
