using AlienDefense.Core;
using UnityEngine;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Drives TopHUD's 4 widgets. Coin (MetaCurrency) and Gem are both real, persisted resources now
    /// (Gem earned via first-time-perfect level clears / Daily Reward day 7, spent on VIP and in the Shop) — both
    /// widgets show the real amount and their "+" button opens the Shop screen. Player Level/XP and lobby Energy
    /// still have no backing data in this project, so those 2 stay in their dimmed placeholder state instead of
    /// showing invented numbers (see PlayerProfileWidgetView.SetUnavailable / ResourceWidgetView.SetUnavailable).
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
            _playerProfileWidget?.SetUnavailable();
            _energyWidget?.SetUnavailable();

            int metaCurrency = _services?.PlayerProfileService?.MetaCurrency ?? 0;
            _coinWidget?.SetAmount(CurrencyFormatter.Format(metaCurrency));

            int gems = _services?.PlayerProfileService?.Gems ?? 0;
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
