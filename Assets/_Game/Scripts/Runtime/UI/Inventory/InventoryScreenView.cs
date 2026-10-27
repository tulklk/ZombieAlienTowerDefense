using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Inventory
{
    /// <summary>Scene references of the Inventory screen (built by InventoryTabBuilder): the UFO area (bonus, preview,
    /// six sockets), the artifact progression overview, the part filter bar with Craft / Merge, the three content
    /// pages, the sub-tabs, the popups, the merge screen and the shared art (part glyphs, rarity colours).</summary>
    public sealed class InventoryScreenView : MonoBehaviour
    {
        [Header("UFO area")]
        public GameObject UfoArea;
        public TMP_Text BonusText;
        public RawImage UfoPreview;
        public Image UfoFallback;
        [Tooltip("Indexed by EquipmentSlotType.")]
        public InvItemCardView[] Slots = new InvItemCardView[6];
        public InvProgressView Progress;

        [Header("Filter bar")]
        public GameObject FilterIcons;
        [Tooltip("All, then EquipmentSlotNames.BarOrder.")]
        public InvToggleButtonView[] Filters = new InvToggleButtonView[7];
        public Button CraftButton;
        public GameObject CraftBadge;
        public Button MergeButton;
        public Image MergeImage;
        public Sprite ButtonGreen;
        public Sprite ButtonGrey;

        [Header("Content")]
        public CanvasGroup ContentGroup;
        public InvItemCardView CardTemplate;
        public GameObject EquipmentPage;
        public ScrollRect EquipmentScroll;
        public Transform EquipmentGrid;
        public TMP_Text EquipmentEmpty;
        public GameObject ArtifactPage;
        public ScrollRect ArtifactScroll;
        public Transform ArtifactGrid;
        public TMP_Text ArtifactEmpty;
        public GameObject MaterialsPage;
        public ScrollRect MaterialsScroll;
        public GameObject ContainersSection;
        public Transform ContainersGrid;
        public GameObject MaterialsSection;
        public Transform MaterialsGrid;
        public TMP_Text MaterialsEmpty;

        [Header("Tabs, popups")]
        [Tooltip("Indexed by InventoryTab.")]
        public InvToggleButtonView[] Tabs = new InvToggleButtonView[3];
        public InventoryPopupsView Popups;
        public InvMergeView Merge;

        [Header("Art")]
        [Tooltip("Part glyphs, indexed by EquipmentSlotType.")]
        public Sprite[] SlotGlyphs = new Sprite[6];
        [Tooltip("Card colours, indexed by MetaItemRarity.")]
        public Color[] RarityColors = new Color[5];
        public Color EmptySlotColor = new Color(0.12f, 0.27f, 0.52f, 1f);
        public Color LockedColor = new Color(0.1f, 0.3f, 0.6f, 1f);
        public Sprite CoinIcon;
        public Sprite GemIcon;
    }
}
