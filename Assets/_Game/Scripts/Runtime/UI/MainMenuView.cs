using AlienDefense.UI.MainMenu;
using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>MainMenu's top-level view. Owns references to each nested section's own dumb view (never 30+
    /// individual widget fields itself) — MainMenuPresenter and its child presenters read these and drive the
    /// nested views directly.</summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        [Header("Level Selection & Play")]
        [SerializeField]
        private MainMenuLevelSelectionView _levelSelectionView;

        [SerializeField]
        private LevelPreviewView _levelPreviewView;

        [SerializeField]
        private PlayButtonView _playButtonView;

        [Header("Top HUD")]
        [SerializeField]
        private PlayerProfileWidgetView _playerProfileWidget;

        [SerializeField]
        private ResourceWidgetView _energyWidget;

        [SerializeField]
        private ResourceWidgetView _premiumCurrencyWidget;

        [SerializeField]
        private ResourceWidgetView _coinWidget;

        [Header("Feature Rails")]
        [SerializeField]
        private FeatureButtonView[] _leftFeatureButtons = System.Array.Empty<FeatureButtonView>();

        [SerializeField]
        private FeatureButtonView _dailyButton;

        [SerializeField]
        private FeatureButtonView _freeRewardButton;

        [Header("Quest & Navigation")]
        [SerializeField]
        private QuestBannerView _questBanner;

        [SerializeField]
        private BottomNavigationView _bottomNavigation;

        public MainMenuLevelSelectionView LevelSelectionView => _levelSelectionView;
        public LevelPreviewView LevelPreviewView => _levelPreviewView;
        public PlayButtonView PlayButtonView => _playButtonView;

        public PlayerProfileWidgetView PlayerProfileWidget => _playerProfileWidget;
        public ResourceWidgetView EnergyWidget => _energyWidget;
        public ResourceWidgetView PremiumCurrencyWidget => _premiumCurrencyWidget;
        public ResourceWidgetView CoinWidget => _coinWidget;

        public FeatureButtonView[] LeftFeatureButtons => _leftFeatureButtons;
        public FeatureButtonView DailyButton => _dailyButton;
        public FeatureButtonView FreeRewardButton => _freeRewardButton;

        public QuestBannerView QuestBanner => _questBanner;
        public BottomNavigationView BottomNavigation => _bottomNavigation;
    }
}
