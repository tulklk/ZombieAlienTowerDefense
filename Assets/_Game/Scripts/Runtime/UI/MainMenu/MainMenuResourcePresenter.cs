using System;
using AlienDefense.Core;
using AlienDefense.Economy;
using AlienDefense.Progression;
using AlienDefense.Meta;
using AlienDefense.Save;
using UnityEngine;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Drives TopHUD's 4 widgets. Coin (MetaCurrency) and Gem are both real, persisted resources now
    /// (Gem earned via first-time-perfect level clears / Daily Reward day 7, spent on VIP and in the Shop) — both
    /// widgets show the real amount and their "+" button opens the Shop screen. Player level and
    /// the XP bar come from PlayerLevelCurve over the saved XP, Power from PlayerPowerCalculator, and Energy from
    /// the lobby PlayEnergyService ("50/60" plus the refill countdown under the pill).
    ///
    /// Coin/Gem are read on Initialize rather than through a change event: nothing on MainMenu itself spends or
    /// earns them while this screen is open (Shop/VIP/Daily reload or reopen this presenter). Energy refills with
    /// the clock, so it alone is re-read - once a second.</summary>
    public sealed class MainMenuResourcePresenter : MonoBehaviour
    {
        [SerializeField]
        private PlayerProfileWidgetView _playerProfileWidget;

        [SerializeField]
        private ResourceWidgetView _energyWidget;

        [SerializeField]
        private ResourceWidgetView _premiumCurrencyWidget;

        [SerializeField]
        private ResourceWidgetView _coinWidget;

        private ApplicationServices _services;
        private int _coinCountFrom;
        private int _gemCountFrom;
        private MenuShellPresenter _menuShell;
        private float _nextEnergyRefresh;
        private int _shownEnergy = -1;
        private int _shownSeconds = -1;

        public void Initialize(ApplicationServices services, MenuShellPresenter menuShell)
        {
            _services = services;
            _menuShell = menuShell;

            Refresh();

            WireShopButton(_coinWidget);
            WireShopButton(_premiumCurrencyWidget);
        }

        public void Refresh()
        {
            PlayerProfileService profile = _services?.PlayerProfileService;
            if (profile != null && _playerProfileWidget != null)
            {
                ShowPlayerLevel(profile.PlayerExperience);
            }
            else
            {
                _playerProfileWidget?.SetUnavailable();
            }

            RefreshEnergy();

            int metaCurrency = profile?.MetaCurrency ?? 0;
            _coinWidget?.SetAmount(CurrencyFormatter.Format(metaCurrency));

            int gems = profile?.Gems ?? 0;
            _premiumCurrencyWidget?.SetAmount(CurrencyFormatter.Format(gems));
        }

        /// <summary>Level + XP bar from lifetime XP (PlayerLevelCurve): the level number, the bar filled to the XP into
        /// that level, and "1.2K/3.5K" on the bar. Power stays on the secondary line.</summary>
        private void ShowPlayerLevel(int lifetimeXp)
        {
            PlayerProfileService profile = _services?.PlayerProfileService;
            if (profile == null || _playerProfileWidget == null)
            {
                return;
            }

            PlayerLevelProgress progress = _services.PlayerLevels.Evaluate(lifetimeXp);
            string xpText = progress.IsMaxLevel
                ? "MAX"
                : CurrencyFormatter.Format(progress.XpIntoLevel) + "/" + CurrencyFormatter.Format(progress.XpForNextLevel);
            int power = PlayerPowerCalculator.Compute(profile, _services.TowerCatalog);
            _playerProfileWidget.SetLevel(progress.Level, progress.Progress01, CurrencyFormatter.Format(power), xpText);
        }

        /// <summary>Which TopHUD widget a reward of this kind belongs to: coins and gems to their pills; XP, cards
        /// and blueprints have no pill of their own, so they fly into the profile badge. A missing widget falls back
        /// to the Coin pill rather than leaving an icon stranded mid-screen.</summary>
        public RectTransform GetFlyTarget(VictoryRewardType type)
        {
            switch (type)
            {
                case VictoryRewardType.Gems:
                    return _premiumCurrencyWidget != null ? _premiumCurrencyWidget.FlyTarget : FallbackTarget();
                case VictoryRewardType.Coins:
                    return FallbackTarget();
                default:
                    // XP, upgrade cards and blueprints belong to the player profile (its inventory), not a currency pill.
                    return _playerProfileWidget != null ? (RectTransform)_playerProfileWidget.transform : FallbackTarget();
            }
        }

        /// <summary>Rolls a widget's label back to what it showed before this level's reward, so the count-up has
        /// somewhere to climb from. The profile value is never touched - only the text.</summary>
        public void PrimeForIncoming(VictoryRewardType type, int amount)
        {
            PlayerProfileService profile = _services?.PlayerProfileService;
            if (profile == null)
            {
                return;
            }

            switch (type)
            {
                case VictoryRewardType.Coins:
                    _coinCountFrom = Mathf.Max(0, profile.MetaCurrency - amount);
                    _coinWidget?.SetAmount(CurrencyFormatter.Format(_coinCountFrom));
                    break;
                case VictoryRewardType.Gems:
                    _gemCountFrom = Mathf.Max(0, profile.Gems - amount);
                    _premiumCurrencyWidget?.SetAmount(CurrencyFormatter.Format(_gemCountFrom));
                    break;
                case VictoryRewardType.Experience:
                    ShowPlayerLevel(Mathf.Max(0, profile.PlayerExperience - amount)); // fills up again as the XP lands
                    break;
            }
        }

        /// <summary>The last icon of a reward landed: pulse the widget and count its number up to the real total.</summary>
        public void PlayArrival(VictoryRewardType type, float counterDuration)
        {
            PlayerProfileService profile = _services?.PlayerProfileService;
            PulseOnly(type);

            if (profile == null)
            {
                return;
            }

            switch (type)
            {
                case VictoryRewardType.Coins:
                    _coinWidget?.CountTo(_coinCountFrom, profile.MetaCurrency, counterDuration);
                    _coinCountFrom = profile.MetaCurrency;
                    break;
                case VictoryRewardType.Gems:
                    _premiumCurrencyWidget?.CountTo(_gemCountFrom, profile.Gems, counterDuration);
                    _gemCountFrom = profile.Gems;
                    break;
                case VictoryRewardType.Experience:
                    ShowPlayerLevel(profile.PlayerExperience);
                    break;
            }
        }

        /// <summary>A mid-flight icon arriving: pulse only, no number change yet.</summary>
        public void PulseOnly(VictoryRewardType type)
        {
            switch (type)
            {
                case VictoryRewardType.Gems:
                    _premiumCurrencyWidget?.PlayArrivalPulse();
                    break;
                case VictoryRewardType.Coins:
                    _coinWidget?.PlayArrivalPulse();
                    break;
            }
        }

        private RectTransform FallbackTarget()
        {
            if (_coinWidget != null)
            {
                return _coinWidget.FlyTarget;
            }

            return _playerProfileWidget != null ? (RectTransform)_playerProfileWidget.transform : null;
        }

        /// <summary>The only per-frame work on this screen, and it is a float compare: the energy pill and its
        /// countdown are rebuilt once a second, and only when the number shown would actually change.</summary>
        private void Update()
        {
            if (_energyWidget == null || _services?.PlayEnergy == null || Time.unscaledTime < _nextEnergyRefresh)
            {
                return;
            }

            RefreshEnergy();
        }

        /// <summary>"50/60" and, while refilling, the time to the next point under it ("7m 16s"). Full = no countdown.</summary>
        public void RefreshEnergy()
        {
            _nextEnergyRefresh = Time.unscaledTime + 1f;
            if (_energyWidget == null)
            {
                return;
            }

            PlayEnergyService energy = _services?.PlayEnergy;
            if (energy == null)
            {
                _energyWidget.SetUnavailable();
                _energyWidget.SetSubText(null);
                return;
            }

            DateTime now = DateTime.UtcNow;
            int amount = energy.GetCurrent(now);
            int seconds = amount >= energy.Max ? 0 : (int)Math.Ceiling(energy.GetTimeToNext(now).TotalSeconds);
            if (amount == _shownEnergy && seconds == _shownSeconds)
            {
                return;
            }

            _shownEnergy = amount;
            _shownSeconds = seconds;
            _energyWidget.SetAmount(amount + "/" + energy.Max);
            _energyWidget.SetSubText(seconds > 0 ? FormatCountdown(seconds) : null);
        }

        /// <summary>"7m 16s", "45s", or "1h 05m" for long waits.</summary>
        public static string FormatCountdown(int totalSeconds)
        {
            if (totalSeconds <= 0)
            {
                return string.Empty;
            }

            int hours = totalSeconds / 3600;
            int minutes = totalSeconds / 60 % 60;
            int seconds = totalSeconds % 60;
            if (hours > 0)
            {
                return hours + "h " + minutes.ToString("00") + "m";
            }

            return minutes > 0 ? minutes + "m " + seconds.ToString("00") + "s" : seconds + "s";
        }

        private void WireShopButton(ResourceWidgetView widget)
        {
            if (widget == null)
            {
                return;
            }

            widget.SetAddButtonEnabled(true);
            widget.AddButtonClicked += HandleAddButtonClicked;
        }

        private void HandleAddButtonClicked()
        {
            _menuShell?.SwitchTab(MenuTab.Shop);
        }

        private void OnDestroy()
        {
            if (_coinWidget != null)
            {
                _coinWidget.AddButtonClicked -= HandleAddButtonClicked;
            }

            if (_premiumCurrencyWidget != null)
            {
                _premiumCurrencyWidget.AddButtonClicked -= HandleAddButtonClicked;
            }
        }
    }
}
