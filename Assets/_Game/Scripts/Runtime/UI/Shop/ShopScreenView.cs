using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Shop
{
    /// <summary>Scene references of the Shop screen (built by ShopTabBuilder): header currencies, the fixed bottom
    /// category bar, the vertical content scroll with one container per category, the popup layer and the card
    /// prefabs the presenter instantiates once.</summary>
    public sealed class ShopScreenView : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField]
        private TMP_Text _gemText;

        [SerializeField]
        private TMP_Text _coinText;

        [Header("Bottom bar")]
        [SerializeField]
        private Button _backButton;

        [SerializeField]
        private ScrollRect _categoryScroll;

        [SerializeField, Tooltip("Indexed by ShopCategory.")]
        private ShopCategoryButtonView[] _categoryButtons = new ShopCategoryButtonView[0];

        [Header("Content")]
        [SerializeField]
        private ScrollRect _contentScroll;

        [SerializeField]
        private CanvasGroup _contentGroup;

        [SerializeField, Tooltip("Indexed by ShopCategory: the container shown for that category.")]
        private RectTransform[] _categoryRoots = new RectTransform[0];

        [SerializeField, Tooltip("Where Crystals cards go (a grid inside the Crystals container).")]
        private Transform _crystalGrid;

        [SerializeField]
        private ShopVipView _vipView;

        [SerializeField]
        private ShopPopupView _popup;

        [SerializeField]
        private ShopTimer _timer;

        [Header("Card prefabs")]
        [SerializeField]
        private ShopPackCardView _largeCardPrefab;

        [SerializeField]
        private ShopPackCardView _campaignCardPrefab;

        [SerializeField]
        private ShopPackCardView _resourceCardPrefab;

        [SerializeField]
        private ShopPackCardView _privilegeCardPrefab;

        [SerializeField]
        private ShopCrystalCardView _crystalCardPrefab;

        public TMP_Text GemText => _gemText;
        public TMP_Text CoinText => _coinText;
        public Button BackButton => _backButton;
        public ScrollRect CategoryScroll => _categoryScroll;
        public ShopCategoryButtonView[] CategoryButtons => _categoryButtons;
        public ScrollRect ContentScroll => _contentScroll;
        public CanvasGroup ContentGroup => _contentGroup;
        public RectTransform[] CategoryRoots => _categoryRoots;
        public Transform CrystalGrid => _crystalGrid;
        public ShopVipView VipView => _vipView;
        public ShopPopupView Popup => _popup;
        public ShopTimer Timer => _timer;
        public ShopPackCardView LargeCardPrefab => _largeCardPrefab;
        public ShopPackCardView CampaignCardPrefab => _campaignCardPrefab;
        public ShopPackCardView ResourceCardPrefab => _resourceCardPrefab;
        public ShopPackCardView PrivilegeCardPrefab => _privilegeCardPrefab;
        public ShopCrystalCardView CrystalCardPrefab => _crystalCardPrefab;
    }
}
