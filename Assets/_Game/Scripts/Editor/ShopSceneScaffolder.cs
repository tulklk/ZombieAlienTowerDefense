using AlienDefense.Core;
using AlienDefense.UI;
using AlienDefense.UI.MainMenu;
using AlienDefense.UI.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static AlienDefense.EditorTools.EditorScreenBuildingBlocks;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds the Shop scene: 3 Gem-to-Coin exchange cards (the only real purchase this project can
    /// offer without a store/IAP integration) plus a clearly-labeled, permanently-disabled "Nạp Gem" (buy Gems
    /// with real money) placeholder section — see ShopScreenView's doc comment for why that part can't be real.</summary>
    internal static class ShopSceneScaffolder
    {
        private const string SceneFolder = "Assets/_Game/Scenes/Menu";
        private const string ScenePath = SceneFolder + "/Shop.unity";

        private static readonly Color PanelColor = new Color(0.08f, 0.12f, 0.22f, 0.85f);
        private static readonly Color AccentGreen = new Color(0.35f, 0.85f, 0.45f, 1f);
        private static readonly Color DisabledColor = new Color(0.3f, 0.3f, 0.35f, 0.7f);
        private static readonly Color BackgroundColor = new Color(0.04f, 0.08f, 0.16f, 1f);

        [MenuItem("AlienDefense/Setup/19. Build Shop Scene")]
        public static void BuildShopScene()
        {
            EditorFolderUtility.EnsureFolder(SceneFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            (GameObject canvasObject, Transform safeArea) = EditorCanvasUtility.BuildCanvasWithSafeArea("Canvas");
            BuildFullScreenImage(canvasObject.transform, "BackgroundOverlay", BackgroundColor).transform.SetAsFirstSibling();

            LevelSceneScaffolder.CreateTMPText(safeArea, "TitleText", "Shop", 40f, 90f, 48f, TextAlignmentOptions.Center);
            Button backButton = BuildTopLeftButton(safeArea, "BackButton", "<", PanelColor);
            (ResourceWidgetView coinWidget, ResourceWidgetView gemWidget) = BuildCurrencyWidgets(safeArea);

            LevelSceneScaffolder.CreateTMPText(safeArea, "ExchangeSectionLabel", "Exchange Gems for Coins", 120f, 40f, 26f, TextAlignmentOptions.Center);
            Transform exchangeCardContainer = BuildExchangeCardRow(safeArea);
            ShopExchangeCardView cardPrefab = BuildOrLoadExchangeCardPrefab();

            BuildRealMoneyPlaceholderSection(safeArea);

            var view = safeArea.gameObject.AddComponent<ShopScreenView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_exchangeCardContainer").objectReferenceValue = exchangeCardContainer;
            serializedView.FindProperty("_backButton").objectReferenceValue = backButton;
            serializedView.FindProperty("_coinWidget").objectReferenceValue = coinWidget;
            serializedView.FindProperty("_gemWidget").objectReferenceValue = gemWidget;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            var backNavigation = canvasObject.AddComponent<BackNavigationController>();

            var presenterObject = new GameObject("ShopScreenPresenter");
            presenterObject.transform.SetParent(canvasObject.transform, false);
            var presenter = presenterObject.AddComponent<ShopScreenPresenter>();
            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("_view").objectReferenceValue = view;
            serializedPresenter.FindProperty("_cardPrefab").objectReferenceValue = cardPrefab;
            serializedPresenter.FindProperty("_backNavigation").objectReferenceValue = backNavigation;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            LevelSceneScaffolder.BuildEventSystem();
            SceneServicesHostScaffolder.EnsureInActiveScene(includeSaveDebugControls: false);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureSceneInBuildSettings(ScenePath);

            Debug.Log("[AlienDefense Setup] Saved Shop scene to " + ScenePath + ".");
        }

        private static (ResourceWidgetView coin, ResourceWidgetView gem) BuildCurrencyWidgets(Transform safeArea)
        {
            var stack = new GameObject("CurrencyStack", typeof(RectTransform));
            stack.transform.SetParent(safeArea, false);
            RectTransform rect = stack.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(180f, 120f);
            var layout = stack.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;

            ResourceWidgetView coin = BuildCurrencyWidget(stack.transform, "CoinWidget");
            ResourceWidgetView gem = BuildCurrencyWidget(stack.transform, "GemWidget");
            return (coin, gem);
        }

        private static ResourceWidgetView BuildCurrencyWidget(Transform parent, string name)
        {
            var widget = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
            widget.transform.SetParent(parent, false);
            widget.GetComponent<Image>().color = PanelColor;
            widget.GetComponent<LayoutElement>().preferredHeight = 56f;

            TMP_Text amount = BuildAnchoredText(widget.transform, "AmountText", "0", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 40f), 26f, TextAlignmentOptions.Center);

            var view = widget.AddComponent<ResourceWidgetView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_amountText").objectReferenceValue = amount;
            serialized.FindProperty("_canvasGroup").objectReferenceValue = widget.GetComponent<CanvasGroup>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static Transform BuildExchangeCardRow(Transform safeArea)
        {
            var container = new GameObject("ExchangeCardContainer", typeof(RectTransform));
            container.transform.SetParent(safeArea, false);
            RectTransform rect = container.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -170f);
            rect.sizeDelta = new Vector2(940f, 280f);

            var layout = container.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            return container.transform;
        }

        private static ShopExchangeCardView BuildOrLoadExchangeCardPrefab()
        {
            const string prefabPath = "Assets/_Game/Prefabs/UI/Shop/ShopExchangeCard.prefab";
            EditorFolderUtility.EnsureFolder("Assets/_Game/Prefabs/UI/Shop");

            var go = new GameObject("ShopExchangeCard", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.GetComponent<Image>().color = PanelColor;
            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 280f;
            layoutElement.preferredHeight = 260f;

            TMP_Text coinAmountText = BuildAnchoredText(go.transform, "CoinAmountText", "100 Coin", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(240f, 40f), 26f, TextAlignmentOptions.Center);

            var buyButtonObject = new GameObject("BuyButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buyButtonObject.transform.SetParent(go.transform, false);
            RectTransform buyRect = buyButtonObject.GetComponent<RectTransform>();
            buyRect.anchorMin = new Vector2(0.5f, 1f);
            buyRect.anchorMax = new Vector2(0.5f, 1f);
            buyRect.pivot = new Vector2(0.5f, 1f);
            buyRect.anchoredPosition = new Vector2(0f, -100f);
            buyRect.sizeDelta = new Vector2(220f, 64f);
            buyButtonObject.GetComponent<Image>().color = AccentGreen;
            Button buyButton = buyButtonObject.GetComponent<Button>();
            TMP_Text gemCostText = BuildAnchoredText(buyButtonObject.transform, "CostText", "0", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 64f), 24f, TextAlignmentOptions.Center);

            var view = go.AddComponent<ShopExchangeCardView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_coinAmountText").objectReferenceValue = coinAmountText;
            serialized.FindProperty("_gemCostText").objectReferenceValue = gemCostText;
            serialized.FindProperty("_buyButton").objectReferenceValue = buyButton;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            return prefabAsset.GetComponent<ShopExchangeCardView>();
        }

        /// <summary>Visible but permanently non-interactable: buying Gems with real money needs a real store/IAP
        /// integration (Google Play Billing / Apple StoreKit) this project has no account or SDK for. This keeps
        /// the layout complete without faking a payment flow — see ShopScreenView's doc comment.</summary>
        private static void BuildRealMoneyPlaceholderSection(Transform safeArea)
        {
            LevelSceneScaffolder.CreateTMPText(safeArea, "IapSectionLabel", "Buy Gems (requires real payment integration)", -260f, 40f, 24f, TextAlignmentOptions.Center);

            var row = new GameObject("IapCardRow", typeof(RectTransform));
            row.transform.SetParent(safeArea, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 1f);
            rowRect.anchorMax = new Vector2(0.5f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -530f);
            rowRect.sizeDelta = new Vector2(940f, 220f);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            string[] iapLabels = { "100 Gem", "500 Gem", "1200 Gem" };
            foreach (string label in iapLabels)
            {
                var card = new GameObject("IapCard_" + label, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                card.transform.SetParent(row.transform, false);
                card.GetComponent<Image>().color = DisabledColor;
                var layoutElement = card.GetComponent<LayoutElement>();
                layoutElement.preferredWidth = 280f;
                layoutElement.preferredHeight = 200f;

                BuildAnchoredText(card.transform, "Label", label, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(240f, 40f), 24f, TextAlignmentOptions.Center);
                BuildAnchoredText(card.transform, "Notice", "Not available", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(240f, 30f), 16f, TextAlignmentOptions.Center);

                Button disabledButton = card.AddComponent<Button>();
                disabledButton.interactable = false;
            }
        }
    }
}
