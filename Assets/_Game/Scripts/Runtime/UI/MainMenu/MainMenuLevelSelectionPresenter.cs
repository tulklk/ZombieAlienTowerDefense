using AlienDefense.Core;
using AlienDefense.Progression;
using AlienDefense.Save;
using UnityEngine;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Drives the center level-selection area (title/status/objectives/preview/Prev-Next) and the Play
    /// button. Replaces the old separate LevelSelectionPresenter/scene: MainMenu now shows one focused campaign
    /// level at a time (matching the reference composition) instead of a scrollable grid of every level.
    ///
    /// No lobby/play-stamina system exists in PlayerProfileSaveData — only the in-level UFO tractor Energy
    /// (EnergyWalletService, session-only, never persisted) exists, and that is a completely different currency
    /// used to build/upgrade towers mid-match. Play never shows or spends an energy cost here; see
    /// PlayButtonView's doc comment for the same rule from the view side.</summary>
    public sealed class MainMenuLevelSelectionPresenter : MonoBehaviour
    {
        [SerializeField]
        private MainMenuLevelSelectionView _view;

        [SerializeField]
        private LevelPreviewView _previewView;

        [SerializeField]
        private PlayButtonView _playButtonView;

        private ApplicationServices _services;
        private ILevelAccessProvider _accessProvider;
        private int _selectedIndex;

        /// <summary>Which way LevelPreviewView should slide for the NEXT RefreshCurrentLevel call only: -1 from
        /// Previous, +1 from Next, 0 for the initial reveal (Initialize never touches this field, so it stays at
        /// its C# default of 0 for that first call).</summary>
        private int _navigationDirection;

        public void Initialize(ApplicationServices services)
        {
            _services = services;
            _accessProvider = services.PlayerProfileService != null
                ? new ProfileLevelAccessProvider(services.PlayerProfileService, services.LevelCatalog)
                : new DefaultLevelAccessProvider(services.LevelCatalog);

            if (_view != null)
            {
                _view.PreviousClicked += HandlePreviousClicked;
                _view.NextClicked += HandleNextClicked;
                _view.MoreClicked += HandleMoreClicked;
            }

            if (_playButtonView != null)
            {
                _playButtonView.Clicked += HandlePlayClicked;
            }

            _selectedIndex = ResolveInitialIndex();
            RefreshCurrentLevel();
        }

        /// <summary>Starts on the level right after the highest one completed so far (i.e. "what to play next"),
        /// falling back to the first catalog entry. This project has no "last opened level" persistence and
        /// doesn't need one: MainMenu fully reloads every time it's entered (SceneTransitionService), so
        /// recomputing "what's next" from HighestUnlockedLevelId here is equivalent and needs no extra save field.</summary>
        private int ResolveInitialIndex()
        {
            LevelCatalog catalog = _services.LevelCatalog;
            if (catalog == null || catalog.Count == 0)
            {
                return 0;
            }

            string highestUnlocked = _services.PlayerProfileService?.HighestUnlockedLevelId;
            int index = catalog.IndexOf(highestUnlocked);
            return index >= 0 ? index : 0;
        }

        private void HandlePreviousClicked()
        {
            if (_selectedIndex <= 0)
            {
                return;
            }

            _selectedIndex--;
            _navigationDirection = -1;
            RefreshCurrentLevel();
        }

        private void HandleNextClicked()
        {
            LevelCatalog catalog = _services?.LevelCatalog;
            if (catalog == null || _selectedIndex >= catalog.Count - 1)
            {
                return;
            }

            _selectedIndex++;
            _navigationDirection = 1;
            RefreshCurrentLevel();
        }

        private void HandleMoreClicked()
        {
            // MainMenuMorePanel (Settings/Credits/Language/Exit) doesn't exist yet — placeholder hook only,
            // matches MainMenuPresenter.HandleExitClicked's existing Editor-safe no-op pattern.
            Debug.Log("[MainMenuLevelSelectionPresenter] More requested (no MainMenuMorePanel yet).");
        }

        private void HandlePlayClicked()
        {
            if (_services == null || _services.SceneTransition == null || _services.SceneTransition.IsTransitioning)
            {
                return;
            }

            LevelCatalog catalog = _services.LevelCatalog;
            if (catalog == null || _selectedIndex < 0 || _selectedIndex >= catalog.Count)
            {
                return;
            }

            LevelCatalogEntry entry = catalog.GetEntry(_selectedIndex);
            if (entry == null || !_accessProvider.IsUnlocked(entry.LevelId))
            {
                return;
            }

            _services.LevelLaunchContext.SetSelectedLevel(entry.LevelId);
            _services.SceneTransition.TryLoadSceneViaBootstrap(entry.SceneName);
        }

        private void RefreshCurrentLevel()
        {
            LevelCatalog catalog = _services?.LevelCatalog;
            if (catalog == null || catalog.Count == 0 || _view == null)
            {
                return;
            }

            LevelCatalogEntry entry = catalog.GetEntry(_selectedIndex);
            if (entry == null || entry.LevelDefinition == null)
            {
                return;
            }

            bool isUnlocked = _accessProvider.IsUnlocked(entry.LevelId);
            LevelProgressSnapshot progress = _services.PlayerProfileService != null
                ? _services.PlayerProfileService.GetLevelProgress(entry.LevelId)
                : LevelProgressSnapshot.NotStarted(entry.LevelId);

            string title = LevelTitleFormatter.Format(_selectedIndex + 1);
            string status = isUnlocked
                ? LevelStatusFormatter.Format(progress, entry.LevelDefinition.BaseMaxHealth)
                : LevelStatusFormatter.FormatLockedRequirement(_selectedIndex > 0 ? LevelTitleFormatter.Format(_selectedIndex) : null);

            _view.SetTitle(title);
            _view.SetStatus(status);
            _view.SetNavigationAvailable(_selectedIndex > 0, _selectedIndex < catalog.Count - 1);
            _view.SetObjectives(BuildObjectives(progress, isUnlocked));

            Sprite previewSprite = isUnlocked ? entry.MenuPreviewSprite : entry.MenuPreviewSpriteLocked;
            _previewView?.ShowPreview(previewSprite, isUnlocked, _navigationDirection);
            _navigationDirection = 0;

            bool canPlay = isUnlocked;
            _playButtonView?.SetLabel("CHƠI");
            _playButtonView?.SetInteractable(canPlay);
        }

        /// <summary>Maps the existing 3-star formula (LevelCompositionRoot.BuildLevelCompletedResult: 1 star =
        /// win, 2 = half+ base HP, 3 = undamaged base) onto 3 objective slots — no separate objective/reward
        /// domain exists, so this only ever reflects star thresholds already being tracked.</summary>
        private static LevelObjectivePresentation[] BuildObjectives(LevelProgressSnapshot progress, bool isUnlocked)
        {
            if (!isUnlocked)
            {
                return null;
            }

            var objectives = new LevelObjectivePresentation[3];
            objectives[0] = new LevelObjectivePresentation("Hoàn thành màn", progress.IsCompleted ? LevelObjectiveState.Completed : LevelObjectiveState.Incomplete);
            objectives[1] = new LevelObjectivePresentation("Base còn ≥50% máu", progress.BestStars >= 2 ? LevelObjectiveState.Completed : LevelObjectiveState.Incomplete);
            objectives[2] = new LevelObjectivePresentation("Hoàn hảo (Base nguyên vẹn)", progress.BestStars >= 3 ? LevelObjectiveState.Completed : LevelObjectiveState.Incomplete);
            return objectives;
        }

        private void OnDestroy()
        {
            if (_view != null)
            {
                _view.PreviousClicked -= HandlePreviousClicked;
                _view.NextClicked -= HandleNextClicked;
                _view.MoreClicked -= HandleMoreClicked;
            }

            if (_playButtonView != null)
            {
                _playButtonView.Clicked -= HandlePlayClicked;
            }
        }
    }
}
