using AlienDefense.Core;
using AlienDefense.Meta;
using AlienDefense.Save;
using UnityEngine;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Drives TopHUD's 4 widgets. Coin (MetaCurrency) and Gem are both real, persisted resources now
    /// (Gem earned via first-time-perfect level clears / Daily Reward day 7, spent on VIP and in the Shop) — both
    /// widgets show the real amount and their "+" button opens the Shop screen. Profile level is campaign-derived
    /// (DisplayLevel) and Power comes from PlayerPowerCalculator; lobby Energy still has no regen economy, so that
    /// pill stays unavailable (ResourceWidgetView.SetUnavailable).
    ///
    /// Reads once on Initialize rather than subscribing to a change event: nothing on MainMenu itself spends or
    /// earns while this screen is open (Shop/VIP/Daily are separate screens/popups that fully reload or reopen
    /// this presenter), so no per-frame polling and no unused event plumbing on PlayerProfileService.</summary>
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
        private MenuShellPresenter _menuShell;

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
                int power = PlayerPowerCalculator.Compute(profile, _services.TowerCatalog);
                _playerProfileWidget.SetLevel(profile.DisplayLevel, 0f, CurrencyFormatter.Format(power));
            }
            else
            {
                _playerProfileWidget?.SetUnavailable();
            }

            _energyWidget?.SetUnavailable();

            int metaCurrency = profile?.MetaCurrency ?? 0;
            _coinWidget?.SetAmount(CurrencyFormatter.Format(metaCurrency));

            int gems = profile?.Gems ?? 0;
            _premiumCurrencyWidget?.SetAmount(CurrencyFormatter.Format(gems));
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
