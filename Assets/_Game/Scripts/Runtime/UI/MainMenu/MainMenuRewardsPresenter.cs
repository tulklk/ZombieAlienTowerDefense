using System;
using AlienDefense.Core;
using AlienDefense.Economy;
using AlienDefense.Progression;
using AlienDefense.Save;
using UnityEngine;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Drives the 3 small engagement features that ARE real, persisted systems: the Quest banner
    /// (1 fixed daily quest), the Daily Reward popup (7-day login streak), and the VIP popup (Gem-purchased
    /// permanent Coin bonus). Grouped in one presenter because each is a handful of calls into
    /// PlayerProfileService/VipService — not enough independent responsibility to justify 3 separate classes.</summary>
    public sealed class MainMenuRewardsPresenter : MonoBehaviour
    {
        [SerializeField]
        private QuestBannerView _questBanner;

        [SerializeField]
        private DailyRewardPanelView _dailyRewardPanel;

        [SerializeField]
        private VipPanelView _vipPanel;

        private ApplicationServices _services;
        private VipService _vipService;

        public void Initialize(ApplicationServices services)
        {
            _services = services;
            _vipService = services.PlayerProfileService != null ? new VipService(services.PlayerProfileService) : null;

            if (_questBanner != null)
            {
                _questBanner.Clicked += HandleQuestClicked;
            }

            if (_dailyRewardPanel != null)
            {
                _dailyRewardPanel.ClaimClicked += HandleDailyRewardClaimClicked;
                _dailyRewardPanel.CloseClicked += HandleDailyRewardCloseClicked;
                _dailyRewardPanel.Hide();
            }

            if (_vipPanel != null)
            {
                _vipPanel.TierBuyClicked += HandleVipTierBuyClicked;
                _vipPanel.CloseClicked += HandleVipCloseClicked;
                _vipPanel.Hide();
            }

            RefreshQuestBanner();
        }

        // ---------------------------------------------------------------- Quest banner

        private void RefreshQuestBanner()
        {
            PlayerProfileService profile = _services?.PlayerProfileService;
            if (profile == null || _questBanner == null)
            {
                _questBanner?.SetUnavailable();
                return;
            }

            DateTime now = DateTime.UtcNow;
            bool claimed = profile.IsDailyQuestClaimed(now);
            bool completed = profile.IsDailyQuestCompleted(now);

            string progressText = claimed ? "Reward claimed" : completed ? "Ready to claim" : "0/1";
            _questBanner.SetQuest("Complete 1 level today", progressText, completed ? 1f : 0f);
        }

        private void HandleQuestClicked()
        {
            PlayerProfileService profile = _services?.PlayerProfileService;
            if (profile == null)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            if (profile.TryClaimDailyQuest(now, DailyQuestRewardTable.CoinReward, out _))
            {
                RefreshQuestBanner();
            }
        }

        // ---------------------------------------------------------------- Daily Reward popup

        /// <summary>Called by MainMenuPresenter when DailyButton or FreeRewardButton is clicked — both open the
        /// same daily-login popup (this project has no separate "watch ad for a free gift" system to distinguish
        /// them, see MainMenu report).</summary>
        public void OpenDailyRewardPanel()
        {
            PlayerProfileService profile = _services?.PlayerProfileService;
            if (profile == null || _dailyRewardPanel == null)
            {
                return;
            }

            RefreshDailyRewardPanel();
            _dailyRewardPanel.Show();
        }

        private void RefreshDailyRewardPanel()
        {
            PlayerProfileService profile = _services.PlayerProfileService;
            DateTime now = DateTime.UtcNow;

            if (DailyRewardCalculator.CanClaim(profile.LastDailyRewardClaimUtc, now))
            {
                int nextDay = DailyRewardCalculator.ComputeNextStreakDay(profile.LastDailyRewardClaimUtc, profile.DailyRewardStreakDay, now);
                _dailyRewardPanel.SetAvailable(nextDay, DailyRewardCalculator.CoinReward(nextDay), DailyRewardCalculator.GemReward(nextDay));
            }
            else
            {
                _dailyRewardPanel.SetAlreadyClaimedToday(profile.DailyRewardStreakDay);
            }
        }

        private void HandleDailyRewardClaimClicked()
        {
            PlayerProfileService profile = _services?.PlayerProfileService;
            if (profile == null)
            {
                return;
            }

            if (profile.TryClaimDailyReward(DateTime.UtcNow, out _, out _))
            {
                RefreshDailyRewardPanel();
            }
        }

        private void HandleDailyRewardCloseClicked()
        {
            _dailyRewardPanel?.Hide();
        }

        // ---------------------------------------------------------------- VIP popup

        public void OpenVipPanel()
        {
            if (_vipService == null || _vipPanel == null)
            {
                return;
            }

            RefreshVipPanel();
            _vipPanel.Show();
        }

        private void RefreshVipPanel()
        {
            _vipPanel.SetCurrentTier(_vipService.CurrentTier);

            for (int i = 0; i < VipTierTable.Tiers.Length; i++)
            {
                VipTierInfo info = VipTierTable.Tiers[i];
                bool isOwned = _vipService.CurrentTier >= info.Tier;
                bool canAfford = !isOwned && _services.PlayerProfileService.Gems >= info.GemCost;
                _vipPanel.ConfigureRow(i, info.GemCost, Mathf.RoundToInt(info.CoinBonusMultiplier * 100f), isOwned, canAfford);
            }
        }

        private void HandleVipTierBuyClicked(int tier)
        {
            if (_vipService != null && _vipService.TryPurchase(tier))
            {
                RefreshVipPanel();
            }
        }

        private void HandleVipCloseClicked()
        {
            _vipPanel?.Hide();
        }

        private void OnDestroy()
        {
            if (_questBanner != null)
            {
                _questBanner.Clicked -= HandleQuestClicked;
            }

            if (_dailyRewardPanel != null)
            {
                _dailyRewardPanel.ClaimClicked -= HandleDailyRewardClaimClicked;
                _dailyRewardPanel.CloseClicked -= HandleDailyRewardCloseClicked;
            }

            if (_vipPanel != null)
            {
                _vipPanel.TierBuyClicked -= HandleVipTierBuyClicked;
                _vipPanel.CloseClicked -= HandleVipCloseClicked;
            }
        }
    }
}
