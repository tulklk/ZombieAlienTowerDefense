using UnityEngine;

namespace AlienDefense.UI.MainMenu
{
    public sealed class MenuShellView : MonoBehaviour
    {
        [SerializeField]
        private GameObject _playPanel;

        [SerializeField]
        private GameObject _shopPanel;

        [SerializeField]
        private GameObject _upgradePanel;

        [SerializeField]
        private GameObject _basePanel;

        [SerializeField]
        private GameObject _defensePanel;

        [SerializeField]
        private GameObject _levelPreviewRoot;

        [SerializeField]
        private BottomNavigationView _bottomNavigation;

        public GameObject PlayPanel => _playPanel;
        public GameObject ShopPanel => _shopPanel;
        public GameObject UpgradePanel => _upgradePanel;
        public GameObject BasePanel => _basePanel;
        public GameObject DefensePanel => _defensePanel;
        public GameObject LevelPreviewRoot => _levelPreviewRoot;
        public BottomNavigationView BottomNavigation => _bottomNavigation;

        public GameObject GetPanel(MenuTab tab)
        {
            switch (tab)
            {
                case MenuTab.Shop:
                    return _shopPanel;
                case MenuTab.Upgrade:
                    return _upgradePanel;
                case MenuTab.Base:
                    return _basePanel;
                case MenuTab.Defense:
                    return _defensePanel;
                default:
                    return _playPanel;
            }
        }
    }
}
