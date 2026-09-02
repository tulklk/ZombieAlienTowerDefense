using AlienDefense.Core;
using AlienDefense.UI.Base;
using AlienDefense.UI.Defense;
using AlienDefense.UI.Shop;
using AlienDefense.UI.Upgrade;
using UnityEngine;

namespace AlienDefense.UI.MainMenu
{
    public sealed class MenuShellPresenter : MonoBehaviour
    {
        [SerializeField]
        private MenuShellView _view;

        [SerializeField]
        private MainMenuResourcePresenter _resourcePresenter;

        [SerializeField]
        private ShopScreenPresenter _shopPresenter;

        [SerializeField]
        private TowerUpgradeScreenPresenter _upgradePresenter;

        [SerializeField]
        private DefenseScreenPresenter _defensePresenter;

        [SerializeField]
        private BaseScreenPresenter _basePresenter;

        private MenuTab _activeTab = MenuTab.Play;

        public MenuTab ActiveTab => _activeTab;

        public void Initialize(ApplicationServices services)
        {
            _shopPresenter?.ReceiveApplicationServices(services);
            _upgradePresenter?.ReceiveApplicationServices(services);
            _defensePresenter?.ReceiveApplicationServices(services);
            _basePresenter?.ReceiveApplicationServices(services);

            SwitchTab(MenuTab.Play, force: true);
        }

        public void SwitchTab(MenuTab tab)
        {
            SwitchTab(tab, force: false);
        }

        public void RefreshHud()
        {
            _resourcePresenter?.Refresh();
        }

        public void RefreshActiveTab()
        {
            RefreshHud();
            RefreshTabContent(_activeTab);
        }

        private void SwitchTab(MenuTab tab, bool force)
        {
            if (!force && _activeTab == tab)
            {
                return;
            }

            _activeTab = tab;

            if (_view != null)
            {
                SetPanelActive(_view.PlayPanel, tab == MenuTab.Play);
                SetPanelActive(_view.ShopPanel, tab == MenuTab.Shop);
                SetPanelActive(_view.UpgradePanel, tab == MenuTab.Upgrade);
                SetPanelActive(_view.BasePanel, tab == MenuTab.Base);
                SetPanelActive(_view.DefensePanel, tab == MenuTab.Defense);

                if (_view.LevelPreviewRoot != null)
                {
                    _view.LevelPreviewRoot.SetActive(tab == MenuTab.Play);
                }

                UpdateBottomNavigation(tab);
            }

            RefreshHud();
            RefreshTabContent(tab);
        }

        private void UpdateBottomNavigation(MenuTab tab)
        {
            BottomNavigationView navigation = _view?.BottomNavigation;
            if (navigation == null)
            {
                return;
            }

            navigation.ShopTab?.SetSelected(tab == MenuTab.Shop);
            navigation.UpgradeTab?.SetSelected(tab == MenuTab.Upgrade);
            navigation.PlayTab?.SetSelected(tab == MenuTab.Play);
            navigation.BaseTab?.SetSelected(tab == MenuTab.Base);
            navigation.DefenseTab?.SetSelected(tab == MenuTab.Defense);
        }

        private void RefreshTabContent(MenuTab tab)
        {
            switch (tab)
            {
                case MenuTab.Shop:
                    _shopPresenter?.Refresh();
                    break;
                case MenuTab.Upgrade:
                    _upgradePresenter?.Refresh();
                    break;
                case MenuTab.Defense:
                    _defensePresenter?.Refresh();
                    break;
                case MenuTab.Base:
                    _basePresenter?.Refresh();
                    break;
            }
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }
    }
}
