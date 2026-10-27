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

        [SerializeField]
        [Tooltip("The Inventory screen that now fills the Upgrade tab. The tower-upgrade presenter above is kept " +
            "wired so its old content can be switched back on without re-scaffolding anything.")]
        private AlienDefense.UI.Inventory.InventoryScreenPresenter _inventoryPresenter;

        [SerializeField]
        [Tooltip("The 3D base inside BasePanel. Switching to the Base tab activates the panel, which is what shows " +
            "the world - this reference is only needed to hand it the services.")]
        private AlienDefense.UI.BaseBuilding.BaseWorldPresenter _baseWorldPresenter;

        private MenuTab _activeTab = MenuTab.Play;
        private MenuTab _tabBeforeShop = MenuTab.Play;
        private AlienDefense.Meta.InventoryService _inventory;

        private void HandleInventoryChanged()
        {
            RefreshHud();
            UpdateInventoryBadge();
        }

        private void UpdateInventoryBadge()
        {
            _view?.BottomNavigation?.UpgradeTab?.SetNotification(_inventory != null && _inventory.HasAnyBadge());
        }

        private void OnDestroy()
        {
            if (_inventory != null)
            {
                _inventory.Changed -= HandleInventoryChanged;
            }
        }

        public MenuTab ActiveTab => _activeTab;

        /// <summary>The Shop's back button / Android back: return to the tab the player came from.</summary>
        public void LeaveShop()
        {
            if (_activeTab == MenuTab.Shop)
            {
                SwitchTab(_tabBeforeShop == MenuTab.Shop ? MenuTab.Play : _tabBeforeShop);
            }
        }

        /// <summary>Lets the active tab consume the Android back button. True when it did.</summary>
        public bool TryHandleBack()
        {
            if (_activeTab == MenuTab.Shop && _shopPresenter != null)
            {
                _shopPresenter.HandleBack();
                return true;
            }

            if (_activeTab == MenuTab.Upgrade && _inventoryPresenter != null && _inventoryPresenter.TryHandleBack())
            {
                return true;
            }

            return false;
        }

        public void Initialize(ApplicationServices services)
        {
            _shopPresenter?.ReceiveApplicationServices(services);
            _upgradePresenter?.ReceiveApplicationServices(services);
            _defensePresenter?.ReceiveApplicationServices(services);
            _basePresenter?.ReceiveApplicationServices(services);
            _inventoryPresenter?.Initialize(services);
            _baseWorldPresenter?.Initialize(services);

            // Equip / craft / merge change Power (HUD) and the Inventory tab's red badge.
            _inventory = services?.Inventory;
            if (_inventory != null)
            {
                _inventory.Changed += HandleInventoryChanged;
            }

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

            if (tab == MenuTab.Shop && _activeTab != MenuTab.Shop)
            {
                _tabBeforeShop = _activeTab;
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
            UpdateInventoryBadge();
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
                    _inventoryPresenter?.ShowTab(_inventoryPresenter.ActiveTab);
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
