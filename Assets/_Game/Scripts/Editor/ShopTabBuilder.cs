using System.Collections.Generic;
using AlienDefense.Shop;
using AlienDefense.UI;
using AlienDefense.UI.MainMenu;
using AlienDefense.UI.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds the Shop tab (2026-10-27): the shop data (ShopCatalog + one ShopItemData per product under
    /// Assets/_Game/Data/Shop), the reusable card prefabs (Assets/_Game/Prefabs/UI/Shop) and the ShopScreen in
    /// MainMenu: full-screen over the menu, own header with Crystal / Coin, a vertical content scroll with one
    /// container per category, a fixed bottom category bar (back button + horizontally scrolling tabs) and the popup
    /// layer. Wires MenuShellView's Shop panel and the ShopScreenPresenter. The old exchange ShopPanel is parked.
    ///
    /// Re-runnable: rebuilds its own ShopScreen and prefabs. Shop data is only created when missing (so tuning in the
    /// Inspector survives) unless the "overwrite data" menu item is used.</summary>
    public static class ShopTabBuilder
    {
        private const string MainMenuPath = "Assets/_Game/Scenes/Menu/MainMenu.unity";
        private const string DataFolder = "Assets/_Game/Data/Shop";
        private const string PrefabFolder = "Assets/_Game/Prefabs/UI/Shop";
        private const string FontPath = "Assets/_Game/Font/Fredoka-Bold SDF.asset";
        private const string TitleMaterialPath = "Assets/_Game/Font/Materials/Fredoka_Title.mat";
        private const string Art = "Assets/_Game/Art/Sprite/";
        private const string ScreenName = "ShopScreen";
        private const float ContentWidth = 1008f;

        private static TMP_FontAsset _font;
        private static Material _outlined;

        [MenuItem("AlienDefense/Setup/35. Build Shop Tab")]
        public static void BuildFromMenu()
        {
            Debug.Log(Build(false));
        }

        [MenuItem("AlienDefense/Setup/35b. Build Shop Tab (overwrite shop data)")]
        public static void BuildOverwriteFromMenu()
        {
            Debug.Log(Build(true));
        }

        public static string Build(bool overwriteData)
        {
            if (EditorApplication.isPlaying)
            {
                return "[ShopTabBuilder] Stop Play mode first.";
            }

            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            _outlined = AssetDatabase.LoadAssetAtPath<Material>(TitleMaterialPath);
            Scene scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);

            ShopArtBuilder.BuildAll();
            GameObject baseWorld = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "BaseWorld")
                {
                    baseWorld = root;
                }
            }

            Sprite hero = ShopArtBuilder.RenderConstructionHero(baseWorld);
            ShopCatalog catalog = BuildData(overwriteData, hero, out string dataReport);
            var prefabs = BuildPrefabs();

            Transform canvas = FindNamed(scene, "Canvas");
            Transform safeArea = canvas != null ? canvas.Find("SafeArea") : null;
            var shellView = Object.FindFirstObjectByType<MenuShellView>(FindObjectsInactive.Include);
            var shell = Object.FindFirstObjectByType<MenuShellPresenter>(FindObjectsInactive.Include);
            if (canvas == null || safeArea == null || shellView == null || shell == null)
            {
                return "[ShopTabBuilder] Canvas / SafeArea / MenuShellView / MenuShellPresenter not found in MainMenu.";
            }

            Transform old = canvas.Find(ScreenName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            // The old exchange-only presenter is replaced by the one on the new screen.
            string removed = "";
            foreach (ShopScreenPresenter presenter in Object.FindObjectsByType<ShopScreenPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                removed += presenter.gameObject.name + " ";
                Object.DestroyImmediate(presenter);
            }

            ShopScreenView view = BuildScreen(canvas, safeArea, prefabs, catalog, out GameObject screen);
            var newPresenter = screen.AddComponent<ShopScreenPresenter>();
            var so = new SerializedObject(newPresenter);
            so.FindProperty("_view").objectReferenceValue = view;
            so.FindProperty("_catalog").objectReferenceValue = catalog;
            so.FindProperty("_menuShell").objectReferenceValue = shell;
            so.ApplyModifiedPropertiesWithoutUndo();

            var shellSo = new SerializedObject(shell);
            shellSo.FindProperty("_shopPresenter").objectReferenceValue = newPresenter;
            shellSo.ApplyModifiedPropertiesWithoutUndo();
            var viewSo = new SerializedObject(shellView);
            viewSo.FindProperty("_shopPanel").objectReferenceValue = screen;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            Transform legacy = FindNamed(scene, "ShopPanel");
            if (legacy != null)
            {
                legacy.gameObject.SetActive(false);
            }

            screen.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "[ShopTabBuilder] Shop built. Replaced presenter on: " + removed + ". " + dataReport;
        }

        // ================================================================== data

        private struct Def
        {
            public string File, Id, Name, Subtitle, Description, Benefits, Unlock, Locked;
            public ShopCategory Category;
            public ShopCardLayout Layout;
            public Sprite Art, Art2;
            public string Theme, ThemeDark;
            public int Price, Original, Discount, Limit;
            public bool OneTime, Limited, Double;
            public ShopRewardData[] Rewards;
        }

        private static ShopRewardData R(ShopRewardType type, int amount, string item = null, string tag = null)
        {
            return new ShopRewardData { Type = type, Amount = amount, ItemId = item, Tag = tag };
        }

        private static ShopRewardData Gems(int amount) => R(ShopRewardType.Gems, amount);
        private static ShopRewardData Coins(int amount) => R(ShopRewardType.Coins, amount);
        private static ShopRewardData Item(string id, int amount) => R(ShopRewardType.Item, amount, id);

        private static ShopCatalog BuildData(bool overwrite, Sprite hero, out string report)
        {
            EnsureFolder("Assets/_Game/Data/Shop");
            Sprite avatar = Sprite(Art + "MainMenu/Avatar/Avatar.png");
            Sprite tower = Sprite(Art + "MainMenu/Tab/tower.png");
            Sprite gemsBanner = ShopArtBuilder.Load("T_Shop_Gems_Banner");
            var defs = new List<Def>
            {
                new Def { File = "ConstructionQueue", Id = "alien.construction_queue", Category = ShopCategory.ConstructionQueue, Layout = ShopCardLayout.LargeFeature,
                    Name = "Construction Queue Pack", Description = "Permanently unlock an extra Construction Queue to develop your Base with efficiency!",
                    Art = hero, Theme = "#8CC6F0", ThemeDark = "#F6B93E", Price = 99000, Discount = 3500, OneTime = true, Limit = 1,
                    Rewards = new[] { Gems(250), R(ShopRewardType.ConstructionQueue, 1), Item("wood", 5000), Coins(25000), Item("volt_drip", 500) } },

                Campaign("CampaignPack01", "alien.campaign_pack_01", "Campaign 1 Gift Pack", "#F08A2C", "#A8420C", 59000, 472000, 800, "level_01", null,
                    avatar, gemsBanner, Gems(200), Coins(50000), Item("microcircuit", 10)),
                Campaign("CampaignPack02", "alien.campaign_pack_02", "Campaign 2 Gift Pack", "#2E8BEB", "#1747A6", 99000, 792000, 750, "level_02", "Unlocks with Campaign 2",
                    avatar, gemsBanner, Gems(300), Coins(75000), Item("rare_atom", 5)),
                Campaign("CampaignPack03", "alien.campaign_pack_03", "Campaign 3 Gift Pack", "#3DB04A", "#1B7228", 119000, 952000, 750, "level_03", "Unlocks with Campaign 3",
                    avatar, gemsBanner, Gems(500), Coins(90000), Item("rare_atom", 8)),
                Campaign("CampaignPack04", "alien.campaign_pack_04", "Campaign 4 Gift Pack", "#8A3FE0", "#4C1E98", 149000, 894000, 600, "level_04", "Coming with Campaign 4",
                    avatar, gemsBanner, Gems(800), Coins(120000), Item("rare_atom", 12)),

                new Def { File = "Vip10Day", Id = "alien.vip_10days", Category = ShopCategory.Vip, Layout = ShopCardLayout.Vip, Name = "VIP 10 Days",
                    Price = 199000, Theme = "#7A2FD0", ThemeDark = "#4A1590", Rewards = new[] { Gems(300), R(ShopRewardType.VipDays, 10) } },
                new Def { File = "Vip30Day", Id = "alien.vip_30days", Category = ShopCategory.Vip, Layout = ShopCardLayout.Vip, Name = "VIP 30 Days",
                    Price = 499000, Theme = "#7A2FD0", ThemeDark = "#4A1590", Rewards = new[] { Gems(600), R(ShopRewardType.VipDays, 30) } },

                new Def { File = "CombatMonthly", Id = "alien.monthly_combat", Category = ShopCategory.WeeklyMonthlyCards, Layout = ShopCardLayout.Privilege,
                    Name = "Combat Monthly Card", Subtitle = "Purchase and get:", Benefits = "• Free Towers in Every Battle +3\n• Battle Speed-Up x2",
                    Art = avatar, Art2 = tower, Theme = "#22A88F", ThemeDark = "#0F5F55", Price = 119000,
                    Rewards = new[] { Gems(500), R(ShopRewardType.PrivilegeDays, 30) } },

                Resource("CityConstructionPack", "alien.resource_city_construction", "City Construction Pack", "#2C86E6", "#164EAE", 940,
                    Sprite(Art + "MainMenu/Tab/base.png"), Item("wood", 3000), Coins(30000), Item("volt_drip", 300), R(ShopRewardType.Item, 20, "microcircuit")),
                Resource("TowerWrenchPack", "alien.resource_tower_wrench", "Tower Wrench Pack", "#8A3FE0", "#5420A6", 995,
                    tower, Item("card_turret", 30), Item("card_ice_dagger", 30), Item("card_mortar", 30), Coins(20000)),
                Resource("VoltDripPack", "alien.resource_volt_drip", "Volt Drip Pack", "#E0782A", "#8C3C12", 800,
                    Sprite(Art + "MainMenu/Reward/voltdripicon.png"), Item("volt_drip", 2500), Coins(20000), Item("wood", 1000)),
                Resource("EvolutionCoinsPack", "alien.resource_evolution", "Evolution Coins Pack", "#2A9BE0", "#145FA0", 750,
                    Sprite(Art + "MainMenu/Reward/evolutioncoinicon.png"), Item("rare_atom", 10), Coins(20000), Item("wood", 1000)),
                Resource("BaseCardsPack", "alien.resource_base_cards", "Base Cards Pack", "#9440D8", "#56209A", 915,
                    ItemIcon("card_ufo"), Item("card_ufo", 300), Coins(20000), Item("wood", 1000)),
                Resource("CityLifelinePack", "alien.resource_city_lifeline", "City Lifeline Pack", "#2C86E6", "#164EAE", 1055,
                    Sprite(Art + "MainMenu/Event/uudai.png"), Item("wood", 5000), Coins(50000), Item("volt_drip", 1000), Item("microcircuit", 40)),

                Crystal("Crystal160", "alien.crystals_160", "Handful of Crystals", 59000, 160, false, "T_Shop_Gems_Handful"),
                Crystal("Crystal1000", "alien.crystals_1000", "Pile of Crystals", 119000, 500, true, "T_Shop_Gems_Pile"),
                Crystal("Crystal2400", "alien.crystals_2400", "Suitcase of Crystals", 299000, 1200, true, "T_Shop_Gems_Suitcase"),
                Crystal("Crystal5000", "alien.crystals_5000", "Chest of Crystals", 499000, 2500, true, "T_Shop_Gems_Chest"),
                Crystal("Crystal13000", "alien.crystals_13000", "Safe of Crystals", 1299000, 6500, true, "T_Shop_Gems_Safe"),
                Crystal("Crystal28000", "alien.crystals_28000", "Mountain of Crystals", 2999000, 14000, true, "T_Shop_Gems_Mountain"),
            };

            int created = 0, updated = 0;
            var items = new List<ShopItemData>();
            foreach (Def def in defs)
            {
                string path = DataFolder + "/" + def.File + ".asset";
                var item = AssetDatabase.LoadAssetAtPath<ShopItemData>(path);
                bool isNew = item == null;
                if (isNew)
                {
                    item = ScriptableObject.CreateInstance<ShopItemData>();
                    AssetDatabase.CreateAsset(item, path);
                    created++;
                }

                if (isNew || overwrite)
                {
                    WriteItem(item, def);
                    if (!isNew) updated++;
                }
                else
                {
                    // Keep authored numbers; only fill art that is still missing.
                    var so = new SerializedObject(item);
                    if (so.FindProperty("_art").objectReferenceValue == null && def.Art != null)
                    {
                        so.FindProperty("_art").objectReferenceValue = def.Art;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                items.Add(item);
            }

            string catalogPath = DataFolder + "/ShopCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ShopCatalog>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
                created++;
            }

            var cso = new SerializedObject(catalog);
            SerializedProperty list = cso.FindProperty("_items");
            list.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }

            cso.FindProperty("_gemIcon").objectReferenceValue = Sprite(Art + "MainMenu/Avatar/diamondicon.png");
            cso.FindProperty("_coinIcon").objectReferenceValue = Sprite(Art + "MainMenu/Avatar/coinicon.png");
            cso.FindProperty("_constructionQueueIcon").objectReferenceValue = Sprite("Assets/_Game/Art/Base/Icons/T_Icon_Hammer.png");
            cso.FindProperty("_vipIcon").objectReferenceValue = Sprite(Art + "MainMenu/Event/vip.png");
            cso.FindProperty("_privilegeIcon").objectReferenceValue = Sprite(Art + "MainMenu/Event/daily.png");
            string[] iconIds = { "wood", "volt_drip", "rare_atom", "microcircuit", "card_turret", "card_ice_dagger", "card_mortar", "card_tesla", "card_ufo" };
            SerializedProperty icons = cso.FindProperty("_itemIcons");
            icons.arraySize = iconIds.Length;
            for (int i = 0; i < iconIds.Length; i++)
            {
                SerializedProperty e = icons.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("ItemId").stringValue = iconIds[i];
                e.FindPropertyRelative("Icon").objectReferenceValue = ItemIcon(iconIds[i]);
                e.FindPropertyRelative("Name").stringValue = ItemName(iconIds[i]);
            }

            cso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            report = $"Shop data: {items.Count} products ({created} created, {updated} overwritten).";
            return catalog;
        }

        private static Def Campaign(string file, string id, string name, string theme, string dark, int price, int original, int discount,
            string unlock, string locked, Sprite art, Sprite art2, params ShopRewardData[] rewards)
        {
            return new Def { File = file, Id = id, Category = ShopCategory.CampaignPacks, Layout = ShopCardLayout.Campaign, Name = name,
                Subtitle = "One-time only", Theme = theme, ThemeDark = dark, Price = price, Original = original, Discount = discount,
                OneTime = true, Limit = 1, Unlock = unlock, Locked = locked, Art = art, Art2 = art2, Rewards = rewards };
        }

        private static Def Resource(string file, string id, string name, string theme, string dark, int discount, Sprite art, params ShopRewardData[] rewards)
        {
            var all = new List<ShopRewardData> { Gems(500) };
            all.AddRange(rewards);
            return new Def { File = file, Id = id, Category = ShopCategory.ResourcePacks, Layout = ShopCardLayout.Resource, Name = name,
                Theme = theme, ThemeDark = dark, Price = 119000, Discount = discount, Limited = true, Limit = 1, Art = art, Rewards = all.ToArray() };
        }

        private static Def Crystal(string file, string id, string name, int price, int gems, bool doubled, string art)
        {
            return new Def { File = file, Id = id, Category = ShopCategory.Crystals, Layout = ShopCardLayout.Crystal, Name = name,
                Theme = "#35C3EA", ThemeDark = "#1A6FA8", Price = price, Double = doubled, Art = ShopArtBuilder.Load(art), Rewards = new[] { Gems(gems) } };
        }

        private static void WriteItem(ShopItemData item, Def d)
        {
            var so = new SerializedObject(item);
            so.FindProperty("_productId").stringValue = d.Id;
            so.FindProperty("_category").enumValueIndex = (int)d.Category;
            so.FindProperty("_layout").enumValueIndex = (int)d.Layout;
            so.FindProperty("_displayName").stringValue = d.Name;
            so.FindProperty("_subtitle").stringValue = d.Subtitle ?? "";
            so.FindProperty("_description").stringValue = d.Description ?? "";
            so.FindProperty("_benefits").stringValue = d.Benefits ?? "";
            so.FindProperty("_art").objectReferenceValue = d.Art;
            so.FindProperty("_secondaryArt").objectReferenceValue = d.Art2;
            so.FindProperty("_theme").colorValue = Hex(d.Theme ?? "#2E8BEB");
            so.FindProperty("_themeDark").colorValue = Hex(d.ThemeDark ?? "#1747A6");
            so.FindProperty("_priceVnd").intValue = d.Price;
            so.FindProperty("_originalPriceVnd").intValue = d.Original;
            so.FindProperty("_discountPercent").intValue = d.Discount;
            so.FindProperty("_purchaseLimit").intValue = d.Limit;
            so.FindProperty("_oneTime").boolValue = d.OneTime;
            so.FindProperty("_limitedTime").boolValue = d.Limited;
            so.FindProperty("_firstPurchaseDouble").boolValue = d.Double;
            so.FindProperty("_unlockLevelId").stringValue = d.Unlock ?? "";
            so.FindProperty("_lockedLabel").stringValue = d.Locked ?? "";
            SerializedProperty rewards = so.FindProperty("_rewards");
            rewards.arraySize = d.Rewards.Length;
            for (int i = 0; i < d.Rewards.Length; i++)
            {
                SerializedProperty r = rewards.GetArrayElementAtIndex(i);
                r.FindPropertyRelative("Type").enumValueIndex = (int)d.Rewards[i].Type;
                r.FindPropertyRelative("ItemId").stringValue = d.Rewards[i].ItemId ?? "";
                r.FindPropertyRelative("Amount").intValue = d.Rewards[i].Amount;
                r.FindPropertyRelative("Icon").objectReferenceValue = null;
                r.FindPropertyRelative("Tag").stringValue = d.Rewards[i].Tag ?? "";
                r.FindPropertyRelative("Label").stringValue = "";
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static Sprite ItemIcon(string id)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Game/Data/Meta/Items/MetaItem_" + id + ".asset");
            return asset != null ? new SerializedObject(asset).FindProperty("_icon").objectReferenceValue as Sprite : null;
        }

        private static string ItemName(string id)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Game/Data/Meta/Items/MetaItem_" + id + ".asset");
            return asset != null ? new SerializedObject(asset).FindProperty("_displayName").stringValue : id;
        }

        // ================================================================== prefabs

        private sealed class Prefabs
        {
            public ShopRewardIconView Reward;
            public GameObject Ribbon;
            public ShopPackCardView Large, Campaign, Resource, Privilege;
            public ShopCrystalCardView Crystal;
            public ShopVipView Vip;
        }

        private static Prefabs BuildPrefabs()
        {
            EnsureFolder("Assets/_Game/Prefabs/UI");
            EnsureFolder(PrefabFolder);
            var p = new Prefabs();
            p.Reward = Save(BuildRewardIcon(), "ShopRewardIcon").GetComponent<ShopRewardIconView>();
            p.Ribbon = Save(BuildRibbon(), "ShopDiscountRibbon");
            p.Large = Save(BuildLargeCard(p), "ShopLargePackCard").GetComponent<ShopPackCardView>();
            p.Campaign = Save(BuildCampaignCard(p), "ShopCampaignPackCard").GetComponent<ShopPackCardView>();
            p.Resource = Save(BuildResourceCard(p), "ShopHorizontalPackCard").GetComponent<ShopPackCardView>();
            p.Privilege = Save(BuildPrivilegeCard(p), "ShopPrivilegeCard").GetComponent<ShopPackCardView>();
            p.Crystal = Save(BuildCrystalCard(), "ShopCrystalPackCard").GetComponent<ShopCrystalCardView>();
            p.Vip = Save(BuildVipPanel(), "ShopVIPPanel").GetComponent<ShopVipView>();
            return p;
        }

        private static GameObject Save(GameObject root, string name)
        {
            string path = PrefabFolder + "/" + name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static RectTransform NewRoot(string name, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, height);
            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = element.minHeight = height;
            return rect;
        }

        private static ShopRewardIconView InstanceReward(Prefabs p, Transform parent, float size)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p.Reward.gameObject, parent);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = element.minWidth = size;
            element.preferredHeight = element.minHeight = size;
            go.SetActive(false);
            return go.GetComponent<ShopRewardIconView>();
        }

        private static (GameObject root, TMP_Text text) InstanceRibbon(Prefabs p, Transform parent, float size)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p.Ribbon, parent);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(8f, 8f);
            rect.sizeDelta = new Vector2(size, size);
            return (go, go.GetComponentInChildren<TMP_Text>(true));
        }

        private static GameObject BuildRewardIcon()
        {
            RectTransform root = NewRoot("ShopRewardIcon", 130f, 130f);
            var view = root.gameObject.AddComponent<ShopRewardIconView>();
            Image tile = Img("Tile", root, ShopArtBuilder.Load("T_Shop_Panel"), true, Color.white);
            Stretch(tile.rectTransform);
            Image icon = Img("Icon", tile.rectTransform, null, false, Color.white);
            icon.preserveAspect = true;
            Stretch(icon.rectTransform, 12f, 12f);
            TMP_Text amount = Txt("Amount", tile.rectTransform, "0", 40f, Color.white, TextAlignmentOptions.BottomRight, true);
            Stretch(amount.rectTransform, 8f, 2f);
            RectTransform tagRoot = Box("Tag", tile.rectTransform);
            Place(tagRoot, 0.5f, 0f, -26f, 96f, 40f);
            Image tagBg = Img("Bg", tagRoot, ShopArtBuilder.Load("T_Shop_Pill"), true, new Color(1f, 1f, 1f, 0.9f));
            Stretch(tagBg.rectTransform);
            TMP_Text tag = Txt("Text", tagRoot, "1K", 28f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(tag.rectTransform, 6f, 0f);
            tagRoot.gameObject.SetActive(false);
            TMP_Text label = Txt("Name", root, "", 28f, Color.white, TextAlignmentOptions.Top, true);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 0f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -4f);
            label.rectTransform.sizeDelta = new Vector2(30f, 70f);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.gameObject.SetActive(false);
            Wire(view, ("_background", tile), ("_icon", icon), ("_amount", amount), ("_tagRoot", tagRoot.gameObject), ("_tag", tag), ("_name", label));
            return root.gameObject;
        }

        private static GameObject BuildRibbon()
        {
            var go = new GameObject("ShopDiscountRibbon", typeof(RectTransform));
            go.layer = 5;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(230f, 230f);
            var image = go.AddComponent<Image>();
            image.sprite = ShopArtBuilder.Load("T_Shop_Ribbon");
            image.raycastTarget = false;
            TMP_Text text = Txt("Text", rect, "800%", 46f, Color.white, TextAlignmentOptions.Center, true);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            // The band's centre line runs 120 px in from the corner of a 256 px sprite.
            float k = 120f / 1.41421f / 256f;
            text.rectTransform.anchoredPosition = new Vector2(230f * (0.5f - k), 230f * (0.5f - k));
            text.rectTransform.sizeDelta = new Vector2(190f, 56f);
            text.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            return go;
        }

        private static GameObject BuildLargeCard(Prefabs p)
        {
            RectTransform root = NewRoot("ShopLargePackCard", ContentWidth, 1430f);
            var view = root.gameObject.AddComponent<ShopPackCardView>();
            Image frame = Img("Frame", root, ShopArtBuilder.Load("T_Shop_PanelCyan"), true, Hex("#8CC6F0"));
            Stretch(frame.rectTransform);
            TMP_Text title = Txt("Title", root, "Pack", 60f, Color.white, TextAlignmentOptions.Left, true);
            Place(title.rectTransform, 0f, 44f, 34f, 920f, 76f);
            TMP_Text desc = Txt("Description", root, "", 36f, Color.white, TextAlignmentOptions.TopLeft, true);
            Place(desc.rectTransform, 0f, 44f, 116f, 920f, 100f);
            desc.textWrappingMode = TextWrappingModes.Normal;
            Image artBg = Img("ArtFrame", root, ShopArtBuilder.Load("T_Shop_Panel"), true, Hex("#16284F"));
            Place(artBg.rectTransform, 0.5f, 0f, 232f, 944f, 540f);
            Image art = Img("Art", artBg.rectTransform, null, false, Color.white);
            Stretch(art.rectTransform, 8f, 8f);
            art.preserveAspect = false;

            Image strip = Img("ValueStrip", root, ShopArtBuilder.Load("T_Shop_Panel"), true, Hex("#F6B93E"));
            Place(strip.rectTransform, 0.5f, 0f, 800f, 944f, 360f);
            Image chest = Img("ValueArt", strip.rectTransform, Sprite(Art + "MainMenu/Event/uudai.png"), false, Color.white);
            chest.preserveAspect = true;
            Place(chest.rectTransform, 0f, 20f, 8f, 210f, 190f);
            TMP_Text gemValue = Txt("GemValue", strip.rectTransform, "250", 96f, Color.white, TextAlignmentOptions.Right, true);
            Place(gemValue.rectTransform, 0.5f, -40f, 34f, 330f, 120f);
            Image gemIcon = Img("GemIcon", strip.rectTransform, Sprite(Art + "MainMenu/Avatar/diamondicon.png"), false, Color.white);
            gemIcon.preserveAspect = true;
            Place(gemIcon.rectTransform, 0.5f, 190f, 40f, 110f, 100f);
            var (ribbon, ribbonText) = InstanceRibbon(p, strip.rectTransform, 210f);
            RectTransform row = Row(strip.rectTransform, "Rewards", 20f, 200f, 904f, 146f, 18f);
            ShopRewardIconView template = InstanceReward(p, row, 146f);

            TMP_Text limit = Txt("Limit", root, "Purchase Limit: 1", 40f, Hex("#24365A"), TextAlignmentOptions.Center, false);
            Place(limit.rectTransform, 0.5f, 0f, 1180f, 700f, 56f);
            (Button buy, Image buyImage, TMP_Text buyLabel) = GreenButton(root, "BuyButton", 500f, 140f, 62f);
            Place((RectTransform)buy.transform, 0.5f, 0f, 1246f, 500f, 140f);

            Wire(view, ("_title", title), ("_description", desc), ("_themeImage", frame), ("_themeDarkImage", strip), ("_art", art),
                ("_gemValue", gemValue), ("_rewardRow", row), ("_rewardTemplate", template), ("_ribbon", ribbon), ("_ribbonText", ribbonText),
                ("_buyButton", buy), ("_buyImage", buyImage), ("_buyLabel", buyLabel), ("_limitText", limit));
            SetBool(view, "_gemsInRow", false);
            SetString(view, "_limitFormat", "Purchase Limit: {0}");
            return root.gameObject;
        }

        private static GameObject BuildCampaignCard(Prefabs p)
        {
            RectTransform root = NewRoot("ShopCampaignPackCard", ContentWidth, 430f);
            var view = root.gameObject.AddComponent<ShopPackCardView>();
            Image bg = Img("Background", root, ShopArtBuilder.Load("T_Shop_PanelCyan"), true, Hex("#F08A2C"));
            Stretch(bg.rectTransform);
            Image pattern = Img("Pattern", root, ShopArtBuilder.Load("T_Shop_Pattern"), false, new Color(1f, 1f, 1f, 0.09f));
            pattern.type = Image.Type.Tiled;
            Stretch(pattern.rectTransform, 14f, 14f);
            Image pile = Img("Crystals", root, null, false, Color.white);
            pile.preserveAspect = true;
            Place(pile.rectTransform, 1f, -6f, 200f, 360f, 240f);
            Image alien = Img("Character", root, null, false, Color.white);
            alien.preserveAspect = true;
            Place(alien.rectTransform, 1f, -40f, 40f, 330f, 330f);
            TMP_Text title = Txt("Title", root, "Campaign Gift Pack", 58f, Color.white, TextAlignmentOptions.Left, true);
            Place(title.rectTransform, 0f, 50f, 26f, 620f, 72f);
            TMP_Text subtitle = Txt("Subtitle", root, "One-time only", 38f, Hex("#FFD84A"), TextAlignmentOptions.Center, true);
            Place(subtitle.rectTransform, 0f, 50f, 98f, 560f, 48f);
            RectTransform row = Row(root, "Rewards", 0f, 150f, 0f, 150f, 22f);
            Place(row, 0f, 60f, 150f, 540f, 128f);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            ShopRewardIconView template = InstanceReward(p, row, 128f);
            TMP_Text original = Txt("OriginalPrice", root, "", 36f, Hex("#FF6B5E"), TextAlignmentOptions.Center, true);
            Place(original.rectTransform, 0f, 60f, 286f, 540f, 44f);
            Button price = Btn("PriceBar", root, ShopArtBuilder.Load("T_Shop_Panel"), new Color(0f, 0f, 0f, 0.28f));
            Place((RectTransform)price.transform, 0f, 50f, 332f, 560f, 82f);
            TMP_Text priceLabel = Txt("Label", price.transform, "59,000 VND", 50f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(priceLabel.rectTransform, 10f, 4f);
            var (ribbon, ribbonText) = InstanceRibbon(p, root, 200f);
            (GameObject lockRoot, TMP_Text lockText) = LockOverlay(root);
            Wire(view, ("_title", title), ("_subtitle", subtitle), ("_themeImage", bg), ("_art", alien), ("_secondaryArt", pile),
                ("_rewardRow", row), ("_rewardTemplate", template), ("_ribbon", ribbon), ("_ribbonText", ribbonText),
                ("_originalPrice", original), ("_buyButton", price), ("_buyImage", (Image)price.targetGraphic), ("_buyLabel", priceLabel),
                ("_lockOverlay", lockRoot), ("_lockText", lockText));
            SetBool(view, "_gemsInRow", true);
            return root.gameObject;
        }

        private static GameObject BuildResourceCard(Prefabs p)
        {
            RectTransform root = NewRoot("ShopHorizontalPackCard", ContentWidth, 430f);
            var view = root.gameObject.AddComponent<ShopPackCardView>();
            Image bg = Img("Background", root, ShopArtBuilder.Load("T_Shop_PanelCyan"), true, Hex("#2C86E6"));
            Stretch(bg.rectTransform);
            Image pattern = Img("Pattern", root, ShopArtBuilder.Load("T_Shop_Pattern"), false, new Color(1f, 1f, 1f, 0.08f));
            pattern.type = Image.Type.Tiled;
            Stretch(pattern.rectTransform, 14f, 14f);
            TMP_Text title = Txt("Title", root, "Resource Pack", 50f, Color.white, TextAlignmentOptions.Left, true);
            Place(title.rectTransform, 0f, 40f, 26f, 620f, 64f);
            RectTransform timer = Box("Timer", root);
            Place(timer, 0f, 40f, 96f, 290f, 58f);
            Image timerBg = Img("Bg", timer, ShopArtBuilder.Load("T_Shop_Pill"), true, new Color(1f, 1f, 1f, 0.75f));
            Stretch(timerBg.rectTransform);
            Image clock = Img("Clock", timer, ShopArtBuilder.Load("T_Shop_Clock"), false, Color.white);
            Place(clock.rectTransform, 0f, 14f, 9f, 40f, 40f);
            TMP_Text timerText = Txt("Text", timer, "5d 00:00:00", 34f, Color.white, TextAlignmentOptions.Left, true);
            Place(timerText.rectTransform, 0f, 62f, 4f, 220f, 50f);
            Image band = Img("GemBand", root, null, false, Hex("#FFFFFF"));
            band.color = new Color(1f, 1f, 1f, 0.16f);
            Place(band.rectTransform, 0f, 14f, 166f, 660f, 96f);
            Image gem = Img("GemIcon", band.rectTransform, Sprite(Art + "MainMenu/Avatar/diamondicon.png"), false, Color.white);
            gem.preserveAspect = true;
            Place(gem.rectTransform, 0f, 40f, 4f, 100f, 88f);
            TMP_Text gemValue = Txt("GemValue", band.rectTransform, "500", 80f, Color.white, TextAlignmentOptions.Left, true);
            Place(gemValue.rectTransform, 0f, 152f, 0f, 400f, 96f);
            RectTransform row = Row(root, "Rewards", 0f, 0f, 0f, 120f, 16f);
            Place(row, 0f, 36f, 280f, 620f, 120f);
            ShopRewardIconView template = InstanceReward(p, row, 118f);
            Image art = Img("Art", root, null, false, Color.white);
            art.preserveAspect = true;
            Place(art.rectTransform, 1f, -60f, 40f, 260f, 210f);
            (Button buy, Image buyImage, TMP_Text buyLabel) = GreenButton(root, "BuyButton", 330f, 110f, 46f);
            Place((RectTransform)buy.transform, 1f, -24f, 252f, 330f, 110f);
            TMP_Text remaining = Txt("Remaining", root, "Remaining: 1", 34f, Hex("#FFD84A"), TextAlignmentOptions.Center, true);
            Place(remaining.rectTransform, 1f, -24f, 364f, 330f, 46f);
            var (ribbon, ribbonText) = InstanceRibbon(p, root, 200f);
            Wire(view, ("_title", title), ("_themeImage", bg), ("_art", art), ("_gemValue", gemValue), ("_rewardRow", row),
                ("_rewardTemplate", template), ("_ribbon", ribbon), ("_ribbonText", ribbonText), ("_buyButton", buy), ("_buyImage", buyImage),
                ("_buyLabel", buyLabel), ("_limitText", remaining), ("_timerRoot", timer.gameObject), ("_timerText", timerText));
            SetBool(view, "_gemsInRow", false);
            return root.gameObject;
        }

        private static GameObject BuildPrivilegeCard(Prefabs p)
        {
            RectTransform root = NewRoot("ShopPrivilegeCard", ContentWidth, 620f);
            var view = root.gameObject.AddComponent<ShopPackCardView>();
            Image bg = Img("Background", root, ShopArtBuilder.Load("T_Shop_PanelCyan"), true, Hex("#22A88F"));
            Stretch(bg.rectTransform);
            Image tower = Img("Tower", root, null, false, Color.white);
            tower.preserveAspect = true;
            Place(tower.rectTransform, 1f, -170f, 70f, 260f, 260f);
            Image alien = Img("Character", root, null, false, Color.white);
            alien.preserveAspect = true;
            Place(alien.rectTransform, 1f, -20f, 150f, 330f, 330f);
            RectTransform x2 = Box("X2", root);
            Place(x2, 1f, -26f, 140f, 120f, 100f);
            x2.localRotation = Quaternion.Euler(0f, 0f, 10f);
            Image x2Bg = Img("Bg", x2, ShopArtBuilder.Load("T_Shop_Panel"), true, Hex("#E2382F"));
            Stretch(x2Bg.rectTransform);
            TMP_Text x2Text = Txt("Text", x2, "x2", 58f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(x2Text.rectTransform);
            TMP_Text title = Txt("Title", root, "Monthly Card", 66f, Color.white, TextAlignmentOptions.Left, true);
            Place(title.rectTransform, 0f, 44f, 34f, 900f, 80f);
            TMP_Text subtitle = Txt("Subtitle", root, "Purchase and get:", 40f, Hex("#FFC93E"), TextAlignmentOptions.Left, true);
            Place(subtitle.rectTransform, 0f, 44f, 128f, 600f, 52f);
            Image gemTile = Img("GemTile", root, ShopArtBuilder.Load("T_Shop_Panel"), true, Hex("#2A8DEB"));
            Place(gemTile.rectTransform, 0f, 44f, 190f, 108f, 108f);
            Image gem = Img("Gem", gemTile.rectTransform, Sprite(Art + "MainMenu/Avatar/diamondicon.png"), false, Color.white);
            gem.preserveAspect = true;
            Stretch(gem.rectTransform, 12f, 12f);
            TMP_Text gemValue = Txt("GemValue", root, "500", 70f, Color.white, TextAlignmentOptions.Left, true);
            Place(gemValue.rectTransform, 0f, 172f, 196f, 300f, 96f);
            TMP_Text benefits = Txt("Benefits", root, "", 40f, Color.white, TextAlignmentOptions.TopLeft, true);
            Place(benefits.rectTransform, 0f, 50f, 320f, 640f, 120f);
            benefits.lineSpacing = 8f;
            (Button buy, Image buyImage, TMP_Text buyLabel) = GreenButton(root, "BuyButton", 420f, 124f, 56f);
            Place((RectTransform)buy.transform, 0f, 200f, 448f, 420f, 124f);
            TMP_Text foot = Txt("Footnote", root, "30-day privilege", 38f, Color.white, TextAlignmentOptions.Center, true);
            Place(foot.rectTransform, 0f, 200f, 570f, 420f, 46f);
            RectTransform status = Box("Status", root);
            Place(status, 0f, 120f, 452f, 580f, 110f);
            Image statusBg = Img("Bg", status, ShopArtBuilder.Load("T_Shop_Pill"), true, new Color(1f, 1f, 1f, 0.85f));
            Stretch(statusBg.rectTransform);
            TMP_Text statusText = Txt("Text", status, "ACTIVE", 40f, Hex("#7CFF6B"), TextAlignmentOptions.Center, true);
            Stretch(statusText.rectTransform, 20f, 6f);
            status.gameObject.SetActive(false);
            var (ribbon, ribbonText) = InstanceRibbon(p, root, 200f);
            Wire(view, ("_title", title), ("_subtitle", subtitle), ("_benefits", benefits), ("_themeImage", bg), ("_art", alien),
                ("_secondaryArt", tower), ("_gemValue", gemValue), ("_ribbon", ribbon), ("_ribbonText", ribbonText), ("_buyButton", buy),
                ("_buyImage", buyImage), ("_buyLabel", buyLabel), ("_footnote", foot), ("_statusRoot", status.gameObject), ("_statusText", statusText));
            SetBool(view, "_gemsInRow", false);
            return root.gameObject;
        }

        private static GameObject BuildCrystalCard()
        {
            RectTransform root = NewRoot("ShopCrystalPackCard", 316f, 480f);
            var view = root.gameObject.AddComponent<ShopCrystalCardView>();
            Image bg = Img("Background", root, ShopArtBuilder.Load("T_Shop_PanelCyan"), true, Hex("#2FB8E6"));
            bg.raycastTarget = true;
            Stretch(bg.rectTransform);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.None;
            root.gameObject.AddComponent<UiPressScale>();
            TMP_Text amount = Txt("Amount", root, "160", 58f, Color.white, TextAlignmentOptions.Center, true);
            Place(amount.rectTransform, 0.5f, -28f, 22f, 220f, 70f);
            TMP_Text old = Txt("OldAmount", root, "", 34f, Hex("#FF5B4F"), TextAlignmentOptions.Center, true);
            Place(old.rectTransform, 0.5f, -28f, 88f, 200f, 40f);
            Image art = Img("Art", root, null, false, Color.white);
            art.preserveAspect = true;
            Place(art.rectTransform, 0.5f, 0f, 118f, 280f, 210f);
            TMP_Text name = Txt("Name", root, "Crystals", 36f, Color.white, TextAlignmentOptions.Center, true);
            Place(name.rectTransform, 0.5f, 0f, 314f, 290f, 76f);
            name.textWrappingMode = TextWrappingModes.Normal;
            Image priceBg = Img("PriceBar", root, ShopArtBuilder.Load("T_Shop_Panel"), true, Hex("#17639A"));
            Place(priceBg.rectTransform, 0.5f, 0f, 396f, 286f, 70f);
            TMP_Text price = Txt("Price", priceBg.rectTransform, "59,000 VND", 40f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(price.rectTransform, 10f, 4f);
            RectTransform badge = Box("FirstTimeBadge", root);
            Place(badge, 1f, 30f, -26f, 106f, 112f);
            badge.localRotation = Quaternion.Euler(0f, 0f, -8f);
            Image badgeBg = Img("Bg", badge, ShopArtBuilder.Load("T_Shop_Panel"), true, Hex("#FF6A1F"));
            Stretch(badgeBg.rectTransform);
            TMP_Text x2 = Txt("X2", badge, "x2", 52f, Color.white, TextAlignmentOptions.Center, true);
            Place(x2.rectTransform, 0.5f, 0f, 2f, 110f, 56f);
            TMP_Text first = Txt("First", badge, "1st\ntime", 26f, Hex("#FFF2C2"), TextAlignmentOptions.Center, true);
            Place(first.rectTransform, 0.5f, 0f, 56f, 110f, 60f);
            first.enableAutoSizing = false;
            Wire(view, ("_button", button), ("_amount", amount), ("_oldAmount", old), ("_art", art), ("_name", name), ("_price", price),
                ("_bonusBadge", badge.gameObject));
            return root.gameObject;
        }

        private static GameObject BuildVipPanel()
        {
            RectTransform root = NewRoot("ShopVIPPanel", ContentWidth, 1330f);
            var view = root.gameObject.AddComponent<ShopVipView>();
            Image frame = Img("Frame", root, ShopArtBuilder.Load("T_Shop_PanelCyan"), true, Hex("#7A2FD0"));
            Place(frame.rectTransform, 0.5f, 0f, 60f, ContentWidth, 1270f);
            Image pattern = Img("Pattern", frame.rectTransform, ShopArtBuilder.Load("T_Shop_Pattern"), false, new Color(1f, 1f, 1f, 0.08f));
            pattern.type = Image.Type.Tiled;
            Stretch(pattern.rectTransform, 14f, 14f);

            Image banner = Img("TitleBanner", root, ShopArtBuilder.Load("T_Shop_TabOrange"), true, Color.white);
            Place(banner.rectTransform, 0.5f, 0f, 0f, 560f, 120f);
            TMP_Text title = Txt("Title", banner.rectTransform, "VIP Account", 64f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(title.rectTransform, 20f, 8f);
            TMP_Text sub = Txt("Subtitle", root, "VIP gives\ndaily rewards", 46f, Color.white, TextAlignmentOptions.Center, true);
            Place(sub.rectTransform, 0.5f, 0f, 140f, 560f, 120f);

            Image avatar = Img("Avatar", root, Sprite(Art + "MainMenu/Event/vip.png"), false, Color.white);
            avatar.preserveAspect = true;
            Place(avatar.rectTransform, 0f, 30f, 110f, 210f, 210f);
            TMP_Text status = Txt("Status", root, "Deactivated", 36f, Color.white, TextAlignmentOptions.Center, true);
            Place(status.rectTransform, 0f, 20f, 76f, 240f, 46f);
            status.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 6f);
            TMP_Text zzz = Txt("Zzz", root, "Zzz", 44f, Hex("#E9E2FF"), TextAlignmentOptions.Center, true);
            Place(zzz.rectTransform, 0f, 160f, 150f, 100f, 60f);
            TMP_Text timer = Txt("Remaining", root, "Remaining: 9d 23h", 32f, Hex("#7CFF6B"), TextAlignmentOptions.Center, true);
            Place(timer.rectTransform, 0f, 10f, 322f, 280f, 44f);

            Button gift = Btn("GiftButton", root, Sprite(Art + "MainMenu/Reward/chest.png"), Color.white);
            ((Image)gift.targetGraphic).preserveAspect = true;
            ((Image)gift.targetGraphic).type = Image.Type.Simple;
            Place((RectTransform)gift.transform, 1f, -40f, 130f, 170f, 170f);
            GameObject giftBadge = Badge(gift.transform);

            float y = 380f;
            BenefitRow(root, "AdFree", y, Hex("#F7CF34"), ShopArtBuilder.Load("T_Shop_NoAds"), "Ad-Free", out _);
            BenefitRow(root, "BossPath", y + 160f, Hex("#F7CF34"), Sprite(Art + "MainMenu/Level/Map1.png"), "Boss Path", out _);
            BenefitRow(root, "DailyReward", y + 320f, Hex("#A23CE6"), Sprite(Art + "MainMenu/Avatar/diamondicon.png"), "Daily reward", out TMP_Text dailyCount);

            TMP_Text buyNow = Txt("PurchaseNow", root, "Purchase now and get!", 44f, Color.white, TextAlignmentOptions.Center, true);
            Place(buyNow.rectTransform, 0.5f, 0f, 846f, 800f, 60f);

            var plans = new ShopVipView.PlanSlot[2];
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? 50f : 534f;
                RectTransform plan = Box("Plan" + (i + 1), root);
                Place(plan, 0f, x, 924f, 424f, 360f);
                Image info = Img("Info", plan, ShopArtBuilder.Load("T_Shop_Panel"), true, Hex("#5B1FA8"));
                Place(info.rectTransform, 0.5f, 0f, 0f, 424f, 116f);
                Image tile = Img("GemTile", info.rectTransform, ShopArtBuilder.Load("T_Shop_Panel"), true, Hex("#2A8DEB"));
                Place(tile.rectTransform, 0f, 12f, 12f, 92f, 92f);
                Image gem = Img("Gem", tile.rectTransform, Sprite(Art + "MainMenu/Avatar/diamondicon.png"), false, Color.white);
                gem.preserveAspect = true;
                Stretch(gem.rectTransform, 10f, 14f);
                TMP_Text gems = Txt("Gems", tile.rectTransform, "300", 32f, Color.white, TextAlignmentOptions.Bottom, true);
                Stretch(gems.rectTransform, 2f, 0f);
                TMP_Text label = Txt("Label", info.rectTransform, "Reward\nfor 10 days", 34f, Color.white, TextAlignmentOptions.Left, true);
                Place(label.rectTransform, 0f, 120f, 8f, 290f, 100f);
                (Button button, Image _, TMP_Text price) = GreenButton(plan, "BuyButton", 424f, 180f, 54f);
                Place((RectTransform)button.transform, 0.5f, 0f, 140f, 424f, 180f);
                Place(price.rectTransform, 0.5f, 0f, 20f, 400f, 80f);
                TMP_Text time = Txt("Time", button.transform, "Time: 10d 0h", 36f, Color.white, TextAlignmentOptions.Center, true);
                Place(time.rectTransform, 0.5f, 0f, 102f, 380f, 50f);
                plans[i] = new ShopVipView.PlanSlot { Button = button, Price = price, Time = time, Gems = gems, Label = label };
            }

            Wire(view, ("_statusLabel", status), ("_statusTimer", timer), ("_avatar", avatar), ("_sleepMark", zzz.gameObject),
                ("_giftButton", gift), ("_giftBadge", giftBadge), ("_dailyGems", dailyCount));
            var so = new SerializedObject(view);
            SerializedProperty slots = so.FindProperty("_plans");
            slots.arraySize = plans.Length;
            for (int i = 0; i < plans.Length; i++)
            {
                SerializedProperty s = slots.GetArrayElementAtIndex(i);
                s.FindPropertyRelative("Button").objectReferenceValue = plans[i].Button;
                s.FindPropertyRelative("Price").objectReferenceValue = plans[i].Price;
                s.FindPropertyRelative("Time").objectReferenceValue = plans[i].Time;
                s.FindPropertyRelative("Gems").objectReferenceValue = plans[i].Gems;
                s.FindPropertyRelative("Label").objectReferenceValue = plans[i].Label;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return root.gameObject;
        }

        private static void BenefitRow(RectTransform root, string name, float y, Color color, Sprite icon, string label, out TMP_Text count)
        {
            Image bar = Img(name, root, ShopArtBuilder.Load("T_Shop_Panel"), true, color);
            Place(bar.rectTransform, 0.5f, 0f, y, 880f, 136f);
            Image image = Img("Icon", bar.rectTransform, icon, false, Color.white);
            image.preserveAspect = true;
            Place(image.rectTransform, 0f, 30f, 14f, 108f, 108f);
            count = null;
            if (name == "DailyReward")
            {
                count = Txt("Count", bar.rectTransform, "100", 44f, Color.white, TextAlignmentOptions.Center, true);
                Place(count.rectTransform, 0f, 24f, 86f, 120f, 50f);
            }

            TMP_Text text = Txt("Label", bar.rectTransform, label, 54f, Color.white, TextAlignmentOptions.Left, true);
            Place(text.rectTransform, 0f, 170f, 28f, 660f, 80f);
        }

        private static (GameObject, TMP_Text) LockOverlay(RectTransform root)
        {
            Image dim = Img("LockOverlay", root, ShopArtBuilder.Load("T_Shop_Panel"), true, new Color(0.05f, 0.05f, 0.12f, 0.62f));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            Image lockIcon = Img("Lock", dim.rectTransform, Sprite(Art + "MainMenu/Level/LockIcon.png"), false, Color.white);
            lockIcon.preserveAspect = true;
            Place(lockIcon.rectTransform, 0.5f, 0f, 110f, 120f, 120f);
            TMP_Text text = Txt("Text", dim.rectTransform, "Locked", 46f, Color.white, TextAlignmentOptions.Center, true);
            Place(text.rectTransform, 0.5f, 0f, 236f, 800f, 70f);
            dim.gameObject.SetActive(false);
            return (dim.gameObject, text);
        }

        // ================================================================== screen

        private static ShopScreenView BuildScreen(Transform canvas, Transform safeAreaSibling, Prefabs prefabs, ShopCatalog catalog, out GameObject screenObject)
        {
            RectTransform screen = Box(ScreenName, canvas);
            screen.SetSiblingIndex(safeAreaSibling.GetSiblingIndex() + 1);
            Stretch(screen);
            screenObject = screen.gameObject;
            var view = screen.gameObject.AddComponent<ShopScreenView>();
            var timer = screen.gameObject.AddComponent<ShopTimer>();

            Image gradient = Img("Background", screen, ShopArtBuilder.Load("T_Shop_Gradient"), false, Color.white);
            gradient.raycastTarget = true; // swallow taps meant for the menu underneath
            Stretch(gradient.rectTransform);
            Image pattern = Img("Pattern", screen, ShopArtBuilder.Load("T_Shop_Pattern"), false, new Color(1f, 1f, 1f, 0.055f));
            pattern.type = Image.Type.Tiled;
            Stretch(pattern.rectTransform);

            RectTransform safe = Box("SafeArea", screen);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            const float headerHeight = 196f, barHeight = 236f;

            // Header.
            RectTransform header = Box("Header", safe);
            TopBox(header, 0f, headerHeight);
            Image headerBg = Img("Bg", header, null, false, Hex("#3A96E6"));
            headerBg.rectTransform.anchorMin = Vector2.zero;
            headerBg.rectTransform.anchorMax = Vector2.one;
            headerBg.rectTransform.offsetMin = new Vector2(0f, 0f);
            headerBg.rectTransform.offsetMax = new Vector2(0f, 400f); // reach up into the notch area
            Image headerShine = Img("Shine", header, null, false, new Color(1f, 1f, 1f, 0.12f));
            headerShine.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            headerShine.rectTransform.anchorMax = Vector2.one;
            headerShine.rectTransform.offsetMin = Vector2.zero;
            headerShine.rectTransform.offsetMax = new Vector2(0f, 400f);
            Image headerLine = Img("Line", header, null, false, Hex("#5BE4FF"));
            headerLine.rectTransform.anchorMin = new Vector2(0f, 0f);
            headerLine.rectTransform.anchorMax = new Vector2(1f, 0f);
            headerLine.rectTransform.sizeDelta = new Vector2(0f, 8f);
            headerLine.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            Image shopIcon = Img("ShopIcon", header, Sprite(Art + "MainMenu/Tab/shop.png"), false, Color.white);
            shopIcon.preserveAspect = true;
            Place(shopIcon.rectTransform, 0f, 26f, 26f, 150f, 150f);
            TMP_Text title = Txt("Title", header, "Shop", 80f, Color.white, TextAlignmentOptions.Left, true);
            Place(title.rectTransform, 0f, 186f, 50f, 260f, 100f);
            TMP_Text coins = Currency(header, "Coins", Sprite(Art + "MainMenu/Avatar/coinicon.png"), -24f);
            TMP_Text gems = Currency(header, "Gems", Sprite(Art + "MainMenu/Avatar/diamondicon.png"), -318f);

            // Bottom category bar.
            RectTransform bar = Box("BottomCategoryBar", safe);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(0f, barHeight);
            Image barBg = Img("Bg", bar, null, false, Hex("#1C5DC0"));
            barBg.raycastTarget = true;
            barBg.rectTransform.anchorMin = Vector2.zero;
            barBg.rectTransform.anchorMax = Vector2.one;
            barBg.rectTransform.offsetMin = new Vector2(0f, -300f); // under the home indicator too
            barBg.rectTransform.offsetMax = Vector2.zero;
            Image barLine = Img("Line", bar, null, false, Hex("#DDF3FF"));
            barLine.rectTransform.anchorMin = new Vector2(0f, 1f);
            barLine.rectTransform.anchorMax = new Vector2(1f, 1f);
            barLine.rectTransform.pivot = new Vector2(0.5f, 1f);
            barLine.rectTransform.sizeDelta = new Vector2(0f, 10f);
            Button back = Btn("BackButton", bar, ShopArtBuilder.Load("T_Shop_TabBlue"), Color.white);
            RectTransform backRect = (RectTransform)back.transform;
            backRect.anchorMin = backRect.anchorMax = new Vector2(0f, 0.5f);
            backRect.pivot = new Vector2(0f, 0.5f);
            backRect.anchoredPosition = new Vector2(10f, -4f);
            backRect.sizeDelta = new Vector2(190f, 190f);
            Image arrow = Img("Arrow", backRect, ShopArtBuilder.Load("T_Shop_BackArrow"), false, Color.white);
            arrow.preserveAspect = true;
            Stretch(arrow.rectTransform, 30f, 30f);

            RectTransform scrollRect = Box("CategoryScroll", bar);
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(212f, 0f);
            scrollRect.offsetMax = new Vector2(0f, -10f);
            var categoryScroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            categoryScroll.vertical = false;
            categoryScroll.horizontal = true;
            categoryScroll.movementType = ScrollRect.MovementType.Elastic;
            categoryScroll.scrollSensitivity = 30f;
            RectTransform tabViewport = Box("Viewport", scrollRect);
            Stretch(tabViewport);
            tabViewport.gameObject.AddComponent<RectMask2D>();
            Image tabViewportImage = tabViewport.gameObject.AddComponent<Image>();
            tabViewportImage.color = Color.clear;
            RectTransform tabContent = Box("Content", tabViewport);
            tabContent.anchorMin = new Vector2(0f, 0f);
            tabContent.anchorMax = new Vector2(0f, 1f);
            tabContent.pivot = new Vector2(0f, 0.5f);
            tabContent.offsetMin = tabContent.offsetMax = Vector2.zero;
            var tabLayout = tabContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.padding = new RectOffset(6, 18, 12, 18);
            tabLayout.spacing = 12f;
            tabLayout.childAlignment = TextAnchor.MiddleLeft;
            tabLayout.childControlWidth = tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = tabLayout.childForceExpandHeight = false;
            tabContent.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            categoryScroll.viewport = tabViewport;
            categoryScroll.content = tabContent;

            (string label, string icon)[] tabs =
            {
                ("Construction\nQueue", Art + "MainMenu/Tab/base.png"),
                ("Campaign\nPacks", Art + "MainMenu/Event/rocket.png"),
                ("VIP", Art + "MainMenu/Event/vip.png"),
                ("Weekly/Monthly\nCards", Art + "MainMenu/Event/daily.png"),
                ("Resources\nPacks", Art + "MainMenu/Reward/chest.png"),
                ("Crystals", Art + "MainMenu/Avatar/diamondicon.png"),
            };
            var buttons = new ShopCategoryButtonView[tabs.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                buttons[i] = CategoryButton(tabContent, ((ShopCategory)i).ToString(), tabs[i].label, Sprite(tabs[i].icon));
            }

            // Content scroll between header and bar.
            RectTransform main = Box("MainContent", safe);
            main.anchorMin = Vector2.zero;
            main.anchorMax = Vector2.one;
            main.offsetMin = new Vector2(0f, barHeight);
            main.offsetMax = new Vector2(0f, -headerHeight);
            var contentGroup = main.gameObject.AddComponent<CanvasGroup>();
            var contentScroll = main.gameObject.AddComponent<ScrollRect>();
            contentScroll.horizontal = false;
            contentScroll.vertical = true;
            contentScroll.movementType = ScrollRect.MovementType.Elastic;
            contentScroll.scrollSensitivity = 40f;
            RectTransform viewport = Box("CategoryViewport", main);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;
            // A fixed 1080 design column, centred; ShopContentWidthFitter scales it down on narrower (tall) phones.
            RectTransform content = Box("CategoryContentRoot", viewport);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(1080f, 0f);
            var widthFitter = viewport.gameObject.AddComponent<ShopContentWidthFitter>();
            Wire(widthFitter, ("_content", content));
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 36, 70);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentScroll.viewport = viewport;
            contentScroll.content = content;

            var roots = new RectTransform[6];
            Transform crystalGrid = null;
            for (int i = 0; i < roots.Length; i++)
            {
                RectTransform r = Box("Category_" + (ShopCategory)i, content);
                var v = r.gameObject.AddComponent<VerticalLayoutGroup>();
                v.spacing = 34f;
                v.childAlignment = TextAnchor.UpperCenter;
                v.childControlWidth = v.childControlHeight = true;
                v.childForceExpandWidth = false;
                v.childForceExpandHeight = false;
                roots[i] = r;
                r.gameObject.SetActive(i == 0);
            }

            // VIP: the panel prefab, placed once.
            var vipObject = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.Vip.gameObject, roots[(int)ShopCategory.Vip]);
            ShopVipView vipView = vipObject.GetComponent<ShopVipView>();

            // Crystals: banner + 3-column grid.
            RectTransform banner = NewRoot("CrystalBonusBanner", ContentWidth, 300f);
            banner.SetParent(roots[(int)ShopCategory.Crystals], false);
            Image bannerBg = Img("Bg", banner, ShopArtBuilder.Load("T_Shop_PanelCyan"), true, Hex("#2BAE57"));
            Stretch(bannerBg.rectTransform);
            Image bannerArt = Img("Crystals", banner, ShopArtBuilder.Load("T_Shop_Gems_Banner"), false, Color.white);
            bannerArt.preserveAspect = true;
            Place(bannerArt.rectTransform, 1f, 10f, -20f, 420f, 340f);
            Image bannerTag = Img("TitleTag", banner, ShopArtBuilder.Load("T_Shop_Panel"), true, Hex("#A53CE6"));
            Place(bannerTag.rectTransform, 0f, 36f, 36f, 520f, 100f);
            TMP_Text bannerTitle = Txt("Title", bannerTag.rectTransform, "2X Crystal bonus", 58f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(bannerTitle.rectTransform, 16f, 6f);
            TMP_Text bannerText = Txt("Text", banner, "Double rewards on first purchase\nof crystal packs", 38f, Color.white, TextAlignmentOptions.TopLeft, true);
            Place(bannerText.rectTransform, 0f, 40f, 160f, 600f, 110f);
            RectTransform grid = Box("CrystalGrid", roots[(int)ShopCategory.Crystals]);
            var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(316f, 480f);
            gridLayout.spacing = new Vector2(30f, 34f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 3;
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            var gridElement = grid.gameObject.AddComponent<LayoutElement>();
            gridElement.preferredWidth = ContentWidth;
            crystalGrid = grid;

            ShopPopupView popup = BuildPopup(safe, prefabs);

            Wire(view, ("_gemText", gems), ("_coinText", coins), ("_backButton", back), ("_categoryScroll", categoryScroll),
                ("_contentScroll", contentScroll), ("_contentGroup", contentGroup), ("_crystalGrid", crystalGrid), ("_vipView", vipView),
                ("_popup", popup), ("_timer", timer), ("_largeCardPrefab", prefabs.Large), ("_campaignCardPrefab", prefabs.Campaign),
                ("_resourceCardPrefab", prefabs.Resource), ("_privilegeCardPrefab", prefabs.Privilege), ("_crystalCardPrefab", prefabs.Crystal));
            var so = new SerializedObject(view);
            SetArray(so.FindProperty("_categoryButtons"), buttons);
            SetArray(so.FindProperty("_categoryRoots"), roots);
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static TMP_Text Currency(RectTransform header, string name, Sprite icon, float right)
        {
            RectTransform pill = Box(name, header);
            pill.anchorMin = pill.anchorMax = pill.pivot = new Vector2(1f, 0.5f);
            pill.anchoredPosition = new Vector2(right, 0f);
            pill.sizeDelta = new Vector2(268f, 84f);
            Image bg = Img("Bg", pill, ShopArtBuilder.Load("T_Shop_Pill"), true, Color.white);
            Stretch(bg.rectTransform);
            Image image = Img("Icon", pill, icon, false, Color.white);
            image.preserveAspect = true;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            image.rectTransform.anchoredPosition = new Vector2(30f, 0f);
            image.rectTransform.sizeDelta = new Vector2(112f, 112f);
            TMP_Text text = Txt("Amount", pill, "0", 52f, Color.white, TextAlignmentOptions.Right, true);
            Stretch(text.rectTransform, 0f, 4f);
            text.rectTransform.offsetMin = new Vector2(96f, 4f);
            text.rectTransform.offsetMax = new Vector2(-22f, -4f);
            return text;
        }

        private static ShopCategoryButtonView CategoryButton(RectTransform parent, string name, string label, Sprite icon)
        {
            RectTransform root = Box("Btn_" + name, parent);
            var element = root.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = element.minWidth = 196f;
            element.preferredHeight = element.minHeight = 190f;
            Image glow = Img("Glow", root, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Textures/UI/T_ChestAuraGlow.png"), false, new Color(1f, 0.82f, 0.3f, 0.55f));
            Stretch(glow.rectTransform, -26f, -26f);
            glow.gameObject.SetActive(false);
            Image bg = Img("Background", root, ShopArtBuilder.Load("T_Shop_TabBlue"), true, Color.white);
            bg.raycastTarget = true;
            Stretch(bg.rectTransform);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.None;
            var press = root.gameObject.AddComponent<UiPressScale>();
            SetFloat(press, "_pressedScale", 0.95f);
            RectTransform iconRoot = Box("IconRoot", root);
            Place(iconRoot, 0.5f, 0f, 6f, 130f, 112f);
            Image image = Img("Icon", iconRoot, icon, false, Color.white);
            image.preserveAspect = true;
            Stretch(image.rectTransform);
            TMP_Text text = Txt("Label", root, label, 31f, Color.white, TextAlignmentOptions.Center, true);
            Place(text.rectTransform, 0.5f, 0f, 108f, 190f, 76f);
            text.lineSpacing = -18f;
            GameObject badge = Badge(root);
            var view = root.gameObject.AddComponent<ShopCategoryButtonView>();
            Wire(view, ("_button", button), ("_background", bg), ("_glow", glow.gameObject), ("_iconRoot", iconRoot), ("_label", text), ("_badge", badge));
            var so = new SerializedObject(view);
            so.FindProperty("_normalSprite").objectReferenceValue = ShopArtBuilder.Load("T_Shop_TabBlue");
            so.FindProperty("_selectedSprite").objectReferenceValue = ShopArtBuilder.Load("T_Shop_TabOrange");
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static GameObject Badge(Transform parent)
        {
            Image badge = Img("Badge", parent, ShopArtBuilder.Load("T_Shop_Badge"), false, Color.white);
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(1f, 1f);
            badge.rectTransform.anchoredPosition = new Vector2(-14f, -14f);
            badge.rectTransform.sizeDelta = new Vector2(58f, 58f);
            TMP_Text mark = Txt("Mark", badge.rectTransform, "!", 40f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(mark.rectTransform);
            badge.gameObject.SetActive(false);
            return badge.gameObject;
        }

        private static ShopPopupView BuildPopup(RectTransform safe, Prefabs prefabs)
        {
            RectTransform layer = Box("PopupLayer", safe);
            Stretch(layer);
            var view = layer.gameObject.AddComponent<ShopPopupView>();
            Image dim = Img("Dim", layer, null, false, new Color(0f, 0f, 0f, 0.7f));
            dim.raycastTarget = true;
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = new Vector2(0f, -400f);
            dim.rectTransform.offsetMax = new Vector2(0f, 400f);

            // Confirm.
            RectTransform confirm = PopupPanel(layer, "PurchaseConfirm", 860f, 620f, "Confirm Purchase", out TMP_Text confirmTitle);
            TMP_Text message = Txt("Message", confirm, "Buy?", 46f, Color.white, TextAlignmentOptions.Center, true);
            Place(message.rectTransform, 0.5f, 0f, 160f, 760f, 140f);
            message.textWrappingMode = TextWrappingModes.Normal;
            TMP_Text note = Txt("Note", confirm, "Test store", 32f, Hex("#FFE35A"), TextAlignmentOptions.Center, true);
            Place(note.rectTransform, 0.5f, 0f, 310f, 760f, 46f);
            (Button buy, Image _, TMP_Text price) = GreenButton(confirm, "BuyButton", 380f, 130f, 50f);
            Place((RectTransform)buy.transform, 0.5f, 200f, 420f, 380f, 130f);
            Button cancel = Btn("CancelButton", confirm, ShopArtBuilder.Load("T_Shop_TabBlue"), Color.white);
            Place((RectTransform)cancel.transform, 0.5f, -210f, 420f, 340f, 130f);
            TMP_Text cancelLabel = Txt("Label", cancel.transform, "Cancel", 50f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(cancelLabel.rectTransform, 10f, 6f);

            // Success.
            RectTransform success = Box("PurchaseSuccess", layer);
            success.anchorMin = success.anchorMax = new Vector2(0.5f, 0.5f);
            success.sizeDelta = new Vector2(920f, 900f);
            Image rays = Img("Rays", success, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Textures/UI/T_ChestAuraRays.png"), false, new Color(1f, 0.9f, 0.5f, 0.45f));
            rays.rectTransform.anchorMin = rays.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rays.rectTransform.sizeDelta = new Vector2(1300f, 1300f);
            RectTransform successPanel = PopupPanel(success, "Panel", 920f, 820f, "Purchase Successful!", out TMP_Text successTitle);
            successPanel.anchoredPosition = new Vector2(0f, -40f);
            Image glow = Img("Glow", successPanel, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Textures/UI/T_ChestAuraGlow.png"), false, new Color(1f, 0.95f, 0.6f, 0.35f));
            glow.rectTransform.SetAsFirstSibling();
            Stretch(glow.rectTransform, -60f, -60f);
            RectTransform rewardGrid = Box("RewardGrid", successPanel);
            Place(rewardGrid, 0.5f, 0f, 170f, 840f, 440f);
            var gridLayout = rewardGrid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(180f, 210f);
            gridLayout.spacing = new Vector2(24f, 18f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 4;
            gridLayout.childAlignment = TextAnchor.MiddleCenter;
            var template = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.Reward.gameObject, rewardGrid);
            RectTransform tile = (RectTransform)template.transform.Find("Tile");
            tile.offsetMin = new Vector2(10f, 70f);
            tile.offsetMax = new Vector2(-10f, 0f);
            Transform nameText = template.transform.Find("Name");
            var nameRect = (RectTransform)nameText;
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.anchoredPosition = Vector2.zero;
            nameRect.sizeDelta = new Vector2(10f, 66f);
            template.SetActive(false);
            (Button collect, Image __, TMP_Text collectLabel) = GreenButton(successPanel, "CollectButton", 400f, 130f, 56f);
            collectLabel.text = "Collect";
            Place((RectTransform)collect.transform, 0.5f, 0f, 648f, 400f, 130f);

            // Error.
            RectTransform error = PopupPanel(layer, "ErrorPopup", 820f, 520f, "Oops!", out TMP_Text errorTitle);
            TMP_Text errorMessage = Txt("Message", error, "Something went wrong.", 42f, Color.white, TextAlignmentOptions.Center, true);
            Place(errorMessage.rectTransform, 0.5f, 0f, 160f, 740f, 160f);
            errorMessage.textWrappingMode = TextWrappingModes.Normal;
            (Button ok, Image ___, TMP_Text okLabel) = GreenButton(error, "OkButton", 340f, 120f, 54f);
            okLabel.text = "OK";
            Place((RectTransform)ok.transform, 0.5f, 0f, 350f, 340f, 120f);

            Wire(view, ("_dim", dim), ("_confirmPanel", confirm), ("_confirmTitle", confirmTitle), ("_confirmMessage", message),
                ("_confirmNote", note), ("_confirmButton", buy), ("_confirmPrice", price), ("_cancelButton", cancel),
                ("_successPanel", success), ("_successTitle", successTitle), ("_rays", rays.rectTransform), ("_rewardGrid", rewardGrid),
                ("_rewardTemplate", template.GetComponent<ShopRewardIconView>()), ("_collectButton", collect),
                ("_errorPanel", error), ("_errorMessage", errorMessage), ("_errorButton", ok));
            layer.gameObject.SetActive(false);
            return view;
        }

        private static RectTransform PopupPanel(Transform parent, string name, float width, float height, string title, out TMP_Text titleText)
        {
            RectTransform panel = Box(name, parent);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(width, height);
            Image frame = Img("Frame", panel, ShopArtBuilder.Load("T_Shop_PanelCyan"), true, Hex("#3C6FD8"));
            frame.raycastTarget = true;
            Stretch(frame.rectTransform);
            Image banner = Img("TitleBanner", panel, ShopArtBuilder.Load("T_Shop_TabOrange"), true, Color.white);
            Place(banner.rectTransform, 0.5f, 0f, -40f, Mathf.Min(width - 120f, 700f), 120f);
            titleText = Txt("Title", banner.rectTransform, title, 58f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(titleText.rectTransform, 24f, 10f);
            return panel;
        }

        // ================================================================== ui helpers

        private static (Button, Image, TMP_Text) GreenButton(Transform parent, string name, float w, float h, float fontSize)
        {
            Button button = Btn(name, parent, Sprite(Art + "MainMenu/Defense/btn_green.png"), Color.white);
            ((RectTransform)button.transform).sizeDelta = new Vector2(w, h);
            TMP_Text label = Txt("Label", button.transform, "99,000 VND", fontSize, Color.white, TextAlignmentOptions.Center, true);
            Stretch(label.rectTransform, 16f, 8f);
            return (button, (Image)button.targetGraphic, label);
        }

        private static RectTransform Row(RectTransform parent, string name, float x, float y, float w, float h, float spacing)
        {
            RectTransform row = Box(name, parent);
            Place(row, 0.5f, x, y, w, h);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return row;
        }

        private static RectTransform Box(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Anchors to the parent's top edge at horizontal anchor <paramref name="ax"/> (0 left, 0.5 centre,
        /// 1 right). x is measured from that anchor (positive right), y downwards from the top.</summary>
        private static void Place(RectTransform rect, float ax, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(ax, 1f);
            rect.pivot = new Vector2(ax, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }

        private static Image Img(string name, Transform parent, Sprite sprite, bool sliced, Color color)
        {
            RectTransform rect = Box(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Button Btn(string name, Transform parent, Sprite sprite, Color color)
        {
            Image image = Img(name, parent, sprite, true, color);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            var press = image.gameObject.AddComponent<UiPressScale>();
            SetFloat(press, "_pressedScale", 0.95f);
            return button;
        }

        private static TMP_Text Txt(string name, Transform parent, string content, float size, Color color, TextAlignmentOptions align, bool outlined)
        {
            RectTransform rect = Box(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null)
            {
                text.font = _font;
            }

            if (outlined && _outlined != null)
            {
                text.fontSharedMaterial = _outlined;
            }

            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = size * 0.45f;
            text.fontSizeMax = size;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static void Stretch(RectTransform rect, float horizontal = 0f, float vertical = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(horizontal, vertical);
            rect.offsetMax = new Vector2(-horizontal, -vertical);
        }

        private static void TopBox(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(0f, height);
        }

        private static void Wire(Object target, params (string field, Object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach ((string field, Object value) in values)
            {
                SerializedProperty property = so.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError("[ShopTabBuilder] " + target.GetType().Name + " has no field " + field);
                    continue;
                }

                property.objectReferenceValue = value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property != null)
            {
                property.floatValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static Sprite Sprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Transform FindNamed(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root.transform;
                }

                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name)
                    {
                        return t;
                    }
                }
            }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
            }
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }
    }
}
