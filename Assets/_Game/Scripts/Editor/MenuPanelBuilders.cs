using AlienDefense.UI;
using AlienDefense.UI.Base;
using AlienDefense.UI.Defense;
using AlienDefense.UI.Shop;
using AlienDefense.UI.Upgrade;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static AlienDefense.EditorTools.EditorScreenBuildingBlocks;

namespace AlienDefense.EditorTools
{
    internal static class MenuPanelBuilders
    {
        private static readonly Color PanelColor = new Color(0.08f, 0.12f, 0.22f, 0.85f);
        private static readonly Color AccentGreen = new Color(0.35f, 0.85f, 0.45f, 1f);
        private static readonly Color DisabledColor = new Color(0.3f, 0.3f, 0.35f, 0.7f);

        public readonly struct ShopPanelBuildResult
        {
            public ShopPanelBuildResult(GameObject panel, ShopScreenView view, ShopExchangeCardView cardPrefab)
            {
                Panel = panel;
                View = view;
                CardPrefab = cardPrefab;
            }

            public GameObject Panel { get; }
            public ShopScreenView View { get; }
            public ShopExchangeCardView CardPrefab { get; }
        }

        public readonly struct UpgradePanelBuildResult
        {
            public UpgradePanelBuildResult(GameObject panel, TowerUpgradeScreenView view, TowerUpgradeCardView cardPrefab)
            {
                Panel = panel;
                View = view;
                CardPrefab = cardPrefab;
            }

            public GameObject Panel { get; }
            public TowerUpgradeScreenView View { get; }
            public TowerUpgradeCardView CardPrefab { get; }
        }

        public readonly struct DefensePanelBuildResult
        {
            public DefensePanelBuildResult(GameObject panel, DefenseScreenView view, TowerUnlockCardView cardPrefab)
            {
                Panel = panel;
                View = view;
                CardPrefab = cardPrefab;
            }

            public GameObject Panel { get; }
            public DefenseScreenView View { get; }
            public TowerUnlockCardView CardPrefab { get; }
        }

        public readonly struct BasePanelBuildResult
        {
            public BasePanelBuildResult(GameObject panel, BaseScreenView view)
            {
                Panel = panel;
                View = view;
            }

            public GameObject Panel { get; }
            public BaseScreenView View { get; }
        }

        public static ShopPanelBuildResult BuildShopPanel(Transform contentRoot)
        {
            GameObject panel = EditorMenuShellLayout.CreateTabPanel(contentRoot, "ShopPanel", false);
            (_, RectTransform scrollContent) = EditorMenuShellLayout.BuildScrollPanel(panel.transform, "ShopScroll");

            AddSectionTitle(scrollContent, "Shop");
            AddSectionTitle(scrollContent, "Exchange Gems for Coins", 26f);

            Transform exchangeRow = BuildHorizontalCardRow(scrollContent, "ExchangeCardContainer", 280f, 260f);
            ShopExchangeCardView cardPrefab = AssetDatabase.LoadAssetAtPath<ShopExchangeCardView>("Assets/_Game/Prefabs/UI/Shop/ShopExchangeCard.prefab");

            AddSectionTitle(scrollContent, "Buy Gems (requires real payment integration)", 22f);
            Transform iapRow = BuildHorizontalCardRow(scrollContent, "IapCardRow", 280f, 200f);
            string[] iapLabels = { "100 Gem", "500 Gem", "1200 Gem" };
            foreach (string label in iapLabels)
            {
                BuildDisabledIapCard(iapRow, label);
            }

            var view = panel.AddComponent<ShopScreenView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_exchangeCardContainer").objectReferenceValue = exchangeRow;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            return new ShopPanelBuildResult(panel, view, cardPrefab);
        }

        public static UpgradePanelBuildResult BuildUpgradePanel(Transform contentRoot)
        {
            GameObject panel = EditorMenuShellLayout.CreateTabPanel(contentRoot, "UpgradePanel", false);
            (_, RectTransform scrollContent) = EditorMenuShellLayout.BuildScrollPanel(panel.transform, "UpgradeScroll");

            AddSectionTitle(scrollContent, "Upgrade Tower");

            Transform cardContainer = BuildGridContainer(scrollContent, "CardContainer", 300f, 340f);
            TowerUpgradeCardView cardPrefab = AssetDatabase.LoadAssetAtPath<TowerUpgradeCardView>("Assets/_Game/Prefabs/UI/Upgrade/TowerUpgradeCard.prefab");

            var view = panel.AddComponent<TowerUpgradeScreenView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_cardContainer").objectReferenceValue = cardContainer;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            return new UpgradePanelBuildResult(panel, view, cardPrefab);
        }

        public static DefensePanelBuildResult BuildDefensePanel(Transform contentRoot)
        {
            GameObject panel = EditorMenuShellLayout.CreateTabPanel(contentRoot, "DefensePanel", false);
            (_, RectTransform scrollContent) = EditorMenuShellLayout.BuildScrollPanel(panel.transform, "DefenseScroll");

            AddSectionTitle(scrollContent, "Unlock Towers");

            Transform cardContainer = BuildGridContainer(scrollContent, "CardContainer", 300f, 300f);
            TowerUnlockCardView cardPrefab = AssetDatabase.LoadAssetAtPath<TowerUnlockCardView>("Assets/_Game/Prefabs/UI/Defense/TowerUnlockCard.prefab");

            var view = panel.AddComponent<DefenseScreenView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_cardContainer").objectReferenceValue = cardContainer;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            return new DefensePanelBuildResult(panel, view, cardPrefab);
        }

        public static BasePanelBuildResult BuildBasePanel(Transform contentRoot)
        {
            GameObject panel = EditorMenuShellLayout.CreateTabPanel(contentRoot, "BasePanel", false);
            (_, RectTransform scrollContent) = EditorMenuShellLayout.BuildScrollPanel(panel.transform, "BaseScroll");

            AddSectionTitle(scrollContent, "Base");

            var statsPanel = new GameObject("StatsPanel", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            statsPanel.transform.SetParent(scrollContent, false);
            statsPanel.GetComponent<Image>().color = PanelColor;
            statsPanel.GetComponent<LayoutElement>().preferredHeight = 480f;

            VerticalLayoutGroup layout = statsPanel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 20f;
            layout.padding = new RectOffset(40, 40, 40, 40);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = false;

            TMP_Text completedLevelsText = BuildStatRow(statsPanel.transform, "CompletedLevelsText", "Levels completed: 0");
            TMP_Text totalStarsText = BuildStatRow(statsPanel.transform, "TotalStarsText", "Total stars: 0");
            TMP_Text unlockedTowersText = BuildStatRow(statsPanel.transform, "UnlockedTowersText", "Towers unlocked: 0");
            TMP_Text vipTierText = BuildStatRow(statsPanel.transform, "VipTierText", "VIP: None");
            TMP_Text coinText = BuildStatRow(statsPanel.transform, "CoinText", "Coin: 0");
            TMP_Text gemText = BuildStatRow(statsPanel.transform, "GemText", "Gem: 0");

            var view = panel.AddComponent<BaseScreenView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_completedLevelsText").objectReferenceValue = completedLevelsText;
            serializedView.FindProperty("_totalStarsText").objectReferenceValue = totalStarsText;
            serializedView.FindProperty("_unlockedTowersText").objectReferenceValue = unlockedTowersText;
            serializedView.FindProperty("_vipTierText").objectReferenceValue = vipTierText;
            serializedView.FindProperty("_coinText").objectReferenceValue = coinText;
            serializedView.FindProperty("_gemText").objectReferenceValue = gemText;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            return new BasePanelBuildResult(panel, view);
        }

        private static void AddSectionTitle(Transform parent, string text, float fontSize = 36f)
        {
            var titleObject = new GameObject("SectionTitle", typeof(RectTransform), typeof(LayoutElement));
            titleObject.transform.SetParent(parent, false);
            titleObject.GetComponent<LayoutElement>().preferredHeight = fontSize + 16f;
            TMP_Text title = titleObject.AddComponent<TextMeshProUGUI>();
            title.text = text;
            title.fontSize = fontSize;
            title.alignment = TextAlignmentOptions.Center;
            title.color = Color.white;
        }

        private static Transform BuildHorizontalCardRow(Transform parent, string name, float cardWidth, float cardHeight)
        {
            var container = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            container.transform.SetParent(parent, false);
            container.GetComponent<LayoutElement>().preferredHeight = cardHeight + 8f;

            HorizontalLayoutGroup layout = container.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            return container.transform;
        }

        private static Transform BuildGridContainer(Transform parent, string name, float cellWidth, float cellHeight)
        {
            var container = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(GridLayoutGroup));
            container.transform.SetParent(parent, false);

            GridLayoutGroup grid = container.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(cellWidth, cellHeight);
            grid.spacing = new Vector2(16f, 16f);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.Flexible;

            ContentSizeFitter fitter = container.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return container.transform;
        }

        private static void BuildDisabledIapCard(Transform parent, string label)
        {
            var card = new GameObject("IapCard_" + label, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            card.transform.SetParent(parent, false);
            card.GetComponent<Image>().color = DisabledColor;
            LayoutElement layoutElement = card.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 280f;
            layoutElement.preferredHeight = 200f;

            BuildAnchoredText(card.transform, "Label", label, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(240f, 40f), 24f, TextAlignmentOptions.Center);
            BuildAnchoredText(card.transform, "Notice", "Not available", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(240f, 30f), 16f, TextAlignmentOptions.Center);

            Button disabledButton = card.AddComponent<Button>();
            disabledButton.interactable = false;
        }
    }
}
