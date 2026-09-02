using UnityEngine;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Drives the 5 bottom-nav tabs inside the persistent MainMenu shell. Tab clicks call
    /// MenuShellPresenter.SwitchTab instead of loading separate scenes.</summary>
    public sealed class BottomNavigationPresenter : MonoBehaviour
    {
        [SerializeField]
        private BottomNavigationView _view;

        private MenuShellPresenter _menuShell;

        public void Initialize(MenuShellPresenter menuShell)
        {
            _menuShell = menuShell;

            if (_view == null)
            {
                return;
            }

            WireTab(_view.ShopTab, MenuTab.Shop);
            WireTab(_view.UpgradeTab, MenuTab.Upgrade);
            WireTab(_view.PlayTab, MenuTab.Play);
            WireTab(_view.BaseTab, MenuTab.Base);
            WireTab(_view.DefenseTab, MenuTab.Defense);
        }

        private void WireTab(BottomNavTabView tab, MenuTab menuTab)
        {
            if (tab == null)
            {
                return;
            }

            tab.SetDisabled(false);
            tab.SetNotification(false);
            tab.Clicked += () => HandleTabClicked(menuTab);
        }

        private void HandleTabClicked(MenuTab menuTab)
        {
            _menuShell?.SwitchTab(menuTab);
        }
    }
}
