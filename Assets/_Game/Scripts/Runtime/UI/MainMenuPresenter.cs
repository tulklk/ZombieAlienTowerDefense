using AlienDefense.Core;
using AlienDefense.UI.MainMenu;
using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>MainMenu's scene entry point. Receives application services via IApplicationServicesReceiver
    /// (never a global lookup), then hands them to each child presenter — MainMenuLevelSelectionPresenter
    /// (title/status/objectives/preview/Prev-Next/Play), MainMenuResourcePresenter (TopHUD),
    /// BottomNavigationPresenter (5 tabs), MainMenuRewardsPresenter (Quest banner / Daily Reward / VIP — all real
    /// persisted systems now). FeatureButton_02/03 (left rail) still have no backing system and stay disabled
    /// placeholders. Contains no business logic of its own: never computes progression, never spends currency.</summary>
    public sealed class MainMenuPresenter : MonoBehaviour, IApplicationServicesReceiver
    {
        [SerializeField]
        private MainMenuView _view;

        [SerializeField]
        private MainMenuLevelSelectionPresenter _levelSelectionPresenter;

        [SerializeField]
        private MainMenuResourcePresenter _resourcePresenter;

        [SerializeField]
        private BottomNavigationPresenter _bottomNavigationPresenter;

        [SerializeField]
        private MainMenuRewardsPresenter _rewardsPresenter;

        [SerializeField]
        private MenuShellPresenter _menuShellPresenter;

        [SerializeField]
        [Tooltip("Optional.")]
        private BackNavigationController _backNavigation;

        public void ReceiveApplicationServices(ApplicationServices services)
        {
            _menuShellPresenter?.Initialize(services);
            _levelSelectionPresenter?.Initialize(services);
            _resourcePresenter?.Initialize(services, _menuShellPresenter);
            _bottomNavigationPresenter?.Initialize(_menuShellPresenter);
            _rewardsPresenter?.Initialize(services);

            SetupFeatureRails();

            _backNavigation?.SetHandler(HandleExitRequested);
        }

        private void SetupFeatureRails()
        {
            if (_view == null)
            {
                return;
            }

            FeatureButtonView[] leftButtons = _view.LeftFeatureButtons;

            // Slot 0 (left rail) = VIP — a real system now.
            if (leftButtons.Length > 0 && leftButtons[0] != null)
            {
                leftButtons[0].SetInteractable(true);
                leftButtons[0].SetBadgeVisible(false);
                leftButtons[0].SetNotification(false);
                leftButtons[0].SetCountdown(null);
                leftButtons[0].Clicked += HandleVipButtonClicked;
            }

            // Remaining left-rail slots have no backing feature yet.
            for (int i = 1; i < leftButtons.Length; i++)
            {
                SetFeaturePlaceholder(leftButtons[i]);
            }

            // Daily Reward — this project has no separate "watch ad for a free gift" system, so both slots open
            // the same daily-login popup (see MainMenuRewardsPresenter.OpenDailyRewardPanel's doc comment).
            SetupDailyRewardButton(_view.DailyButton);
            SetupDailyRewardButton(_view.FreeRewardButton);
        }

        private void SetupDailyRewardButton(FeatureButtonView button)
        {
            if (button == null)
            {
                return;
            }

            button.SetInteractable(true);
            button.SetNotification(false);
            button.SetCountdown(null);
            button.Clicked += HandleDailyRewardButtonClicked;
        }

        private static void SetFeaturePlaceholder(FeatureButtonView button)
        {
            if (button == null)
            {
                return;
            }

            button.SetInteractable(false);
            button.SetNotification(false);
            button.SetCountdown(null);
        }

        private void HandleVipButtonClicked()
        {
            _rewardsPresenter?.OpenVipPanel();
        }

        private void HandleDailyRewardButtonClicked()
        {
            _rewardsPresenter?.OpenDailyRewardPanel();
        }

        private void HandleExitRequested()
        {
#if UNITY_EDITOR
            Debug.Log("[MainMenuPresenter] Exit requested (no-op in the Editor).");
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (_view == null)
            {
                return;
            }

            FeatureButtonView[] leftButtons = _view.LeftFeatureButtons;
            if (leftButtons.Length > 0 && leftButtons[0] != null)
            {
                leftButtons[0].Clicked -= HandleVipButtonClicked;
            }

            if (_view.DailyButton != null)
            {
                _view.DailyButton.Clicked -= HandleDailyRewardButtonClicked;
            }

            if (_view.FreeRewardButton != null)
            {
                _view.FreeRewardButton.Clicked -= HandleDailyRewardButtonClicked;
            }
        }
    }
}
