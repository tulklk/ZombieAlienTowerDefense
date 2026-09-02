using UnityEngine;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Container exposing the 5 bottom-nav tab views. No behavior of its own — BottomNavigationPresenter
    /// drives each tab's selected/disabled/notification state.</summary>
    public sealed class BottomNavigationView : MonoBehaviour
    {
        [SerializeField]
        private BottomNavTabView _shopTab;

        [SerializeField]
        private BottomNavTabView _upgradeTab;

        [SerializeField]
        private BottomNavTabView _playTab;

        [SerializeField]
        private BottomNavTabView _baseTab;

        [SerializeField]
        private BottomNavTabView _defenseTab;

        public BottomNavTabView ShopTab => _shopTab;
        public BottomNavTabView UpgradeTab => _upgradeTab;
        public BottomNavTabView PlayTab => _playTab;
        public BottomNavTabView BaseTab => _baseTab;
        public BottomNavTabView DefenseTab => _defenseTab;
    }
}
