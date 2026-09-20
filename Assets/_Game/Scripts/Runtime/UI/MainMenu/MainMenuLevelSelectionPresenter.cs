using System.Collections.Generic;
using AlienDefense.Core;
using AlienDefense.Meta;
using AlienDefense.Progression;
using AlienDefense.Save;
using UnityEngine;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Drives center level-selection + Play. Level 1 hides objectives; Level 2+ shows claimable chests.</summary>
    public sealed class MainMenuLevelSelectionPresenter : MonoBehaviour
    {
        [SerializeField]
        private MainMenuLevelSelectionView _view;

        [SerializeField]
        private LevelPreviewView _previewView;

        [SerializeField]
        private PlayButtonView _playButtonView;

        [SerializeField]
        private ObjectiveRewardOverlayView _rewardOverlay;

        [SerializeField]
        private MetaItemCatalog _metaItemCatalog;

        private ApplicationServices _services;
        private ILevelAccessProvider _accessProvider;
        private ObjectiveRewardService _rewardService;
        private int _selectedIndex;
        private int _navigationDirection;

        public void Initialize(ApplicationServices services)
        {
            _services = services;
            _accessProvider = services.PlayerProfileService != null
                ? new ProfileLevelAccessProvider(services.PlayerProfileService, services.LevelCatalog)
                : new DefaultLevelAccessProvider(services.LevelCatalog);

            if (services.PlayerProfileService != null && services.LevelCatalog != null)
            {
                _rewardService = new ObjectiveRewardService(services.PlayerProfileService, services.LevelCatalog);
            }

            if (_view != null)
            {
                _view.PreviousClicked += HandlePreviousClicked;
                _view.NextClicked += HandleNextClicked;
                _view.MoreClicked += HandleMoreClicked;
                _view.ObjectiveClaimClicked += HandleObjectiveClaimClicked;
            }

            if (_playButtonView != null)
            {
                _playButtonView.Clicked += HandlePlayClicked;
            }

            _selectedIndex = ResolveInitialIndex();
            RefreshCurrentLevel();
        }

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

        private void HandleObjectiveClaimClicked(LevelObjectiveKind kind)
        {
            if (_rewardService == null || _services?.LevelCatalog == null)
            {
                return;
            }

            if (_selectedIndex <= 0)
            {
                return;
            }

            LevelCatalogEntry entry = _services.LevelCatalog.GetEntry(_selectedIndex);
            if (entry == null)
            {
                return;
            }

            if (!_rewardService.TryClaim(entry.LevelId, kind, out List<GrantedObjectiveReward> granted))
            {
                return;
            }

            if (_rewardOverlay != null && granted != null && granted.Count > 0)
            {
                _rewardOverlay.Show(granted);
            }

            RefreshCurrentLevel();
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
            // Level 1 (index 0): hide objectives; Level 2+ shows claimable chests + preview bubble.
            _view.SetObjectives(
                _selectedIndex == 0 ? null : BuildObjectives(entry.LevelId, progress, isUnlocked));

            Sprite previewSprite = isUnlocked ? entry.MenuPreviewSprite : entry.MenuPreviewSpriteLocked;
            _previewView?.ShowPreview(previewSprite, isUnlocked, _navigationDirection);
            _navigationDirection = 0;

            bool canPlay = isUnlocked;
            _playButtonView?.SetVisible(canPlay);
            _playButtonView?.SetPlayedBefore(progress.IsCompleted);
            _playButtonView?.ShowEnergyCost(5);
            _playButtonView?.SetInteractable(canPlay);
        }

        /// <summary>Maps BestStars thresholds onto Clear / HP50 / Perfect claim states. Level 1 skipped at call site.</summary>
        private LevelObjectivePresentation[] BuildObjectives(string levelId, LevelProgressSnapshot progress, bool isUnlocked)
        {
            return new[]
            {
                BuildOne(levelId, LevelObjectiveKind.Clear, "Clear", progress, isUnlocked),
                BuildOne(levelId, LevelObjectiveKind.Hp50, "50%+ HP", progress, isUnlocked),
                BuildOne(levelId, LevelObjectiveKind.Perfect, "Perfect", progress, isUnlocked),
            };
        }

        private LevelObjectivePresentation BuildOne(
            string levelId,
            LevelObjectiveKind kind,
            string label,
            LevelProgressSnapshot progress,
            bool isUnlocked)
        {
            ObjectiveRewardUiState rewardState = ObjectiveRewardUiState.Locked;
            if (_rewardService != null)
            {
                rewardState = _rewardService.GetUiState(levelId, kind);
            }
            else if (isUnlocked)
            {
                bool achieved = ObjectiveRewardService.IsObjectiveAchieved(progress, kind);
                rewardState = achieved
                    ? (progress.IsObjectiveRewardClaimed(kind) ? ObjectiveRewardUiState.Claimed : ObjectiveRewardUiState.Claimable)
                    : ObjectiveRewardUiState.Locked;
            }

            LevelObjectiveState legacy = rewardState == ObjectiveRewardUiState.Locked
                ? (isUnlocked ? LevelObjectiveState.Incomplete : LevelObjectiveState.Locked)
                : LevelObjectiveState.Completed;

            ObjectiveRewardPreviewEntry[] preview = BuildPreview(levelId, kind, rewardState);
            return new LevelObjectivePresentation(kind, label, legacy, rewardState, preview);
        }

        private ObjectiveRewardPreviewEntry[] BuildPreview(string levelId, LevelObjectiveKind kind, ObjectiveRewardUiState state)
        {
            if (_rewardService == null)
            {
                return System.Array.Empty<ObjectiveRewardPreviewEntry>();
            }

            IReadOnlyList<ObjectiveRewardEntry> entries = _rewardService.GetRewardEntries(levelId, kind);
            var list = new List<ObjectiveRewardPreviewEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                ObjectiveRewardEntry e = entries[i];
                if (e == null || e.Amount <= 0)
                {
                    continue;
                }

                bool mystery = e.HideUntilUnlocked && state == ObjectiveRewardUiState.Locked;
                list.Add(new ObjectiveRewardPreviewEntry(e.ItemId, e.Amount, mystery));
            }

            return list.ToArray();
        }

        private void OnDestroy()
        {
            if (_view != null)
            {
                _view.PreviousClicked -= HandlePreviousClicked;
                _view.NextClicked -= HandleNextClicked;
                _view.MoreClicked -= HandleMoreClicked;
                _view.ObjectiveClaimClicked -= HandleObjectiveClaimClicked;
            }

            if (_playButtonView != null)
            {
                _playButtonView.Clicked -= HandlePlayClicked;
            }
        }
    }
}
