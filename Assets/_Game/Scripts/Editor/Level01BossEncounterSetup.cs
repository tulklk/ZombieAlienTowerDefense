using System.Linq;
using AlienDefense.Building;
using AlienDefense.CameraSystem;
using AlienDefense.Core;
using AlienDefense.Data;
using AlienDefense.Enemies;
using AlienDefense.Level;
using AlienDefense.Player;
using AlienDefense.UI;
using AlienDefense.Waves;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Turns Level_01 into "one normal wave of 10, then a Boss + 10 escorts with an intro cinematic".
    /// Everything is Level_01-specific: its own wave asset and encounter asset (the shared Wave_01..10 assets used
    /// by Level_02/03 are not touched), the Enemy_Boss prefab's model/collider/anchors fixed to a playable size,
    /// and the Level_01 scene wired with BossIntroController, the boss banner and the screen boss health bar.
    /// Safe to re-run.</summary>
    internal static class Level01BossEncounterSetup
    {
        private const string LevelDefinitionPath = "Assets/_Game/Data/Levels/Level_01_Definition.asset";
        private const string DataFolder = "Assets/_Game/Data/Waves/Level01";
        private const string WavePath = DataFolder + "/Level01_Wave_01.asset";
        private const string EncounterPath = DataFolder + "/Level01_BossEncounter.asset";
        private const string BossPrefabPath = "Assets/_Game/Prefabs/Enemies/Enemy_Boss.prefab";
        private const string BossHealthBarPrefabPath = "Assets/_Game/Prefabs/UI/BossHealthBar.prefab";
        private const string NormalDefinitionPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Normal.asset";
        private const string RunnerDefinitionPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Runner.asset";
        private const string BossDefinitionPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Boss.asset";
        private const string Level01ScenePath = "Assets/_Game/Scenes/Levels/Level_01.unity";

        // Boss model fix: the imported boss rig is ~100x the zombies' scale. These values make it ~3x a normal
        // zombie (head bone ~3.8 m vs 1.38 m), feet on the ground.
        private const float BossModelScale = 0.073f;
        private const float BossModelY = -0.3f;

        [MenuItem("AlienDefense/Setup/Level 01/Setup Boss Encounter")]
        public static void SetupAll()
        {
            WaveDefinition wave = CreateOrUpdateNormalWave();
            BossEncounterDefinition encounter = CreateOrUpdateEncounter();
            AssignToLevel(wave, encounter);
            FixBossPrefab();
            AssetDatabase.SaveAssets();

            if (EditorSceneManager.GetActiveScene().path == Level01ScenePath)
            {
                SetupScene();
            }
            else
            {
                Debug.LogWarning("[Level01BossEncounterSetup] Open Level_01 and run again to wire the scene.");
            }

            Debug.Log("[Level01BossEncounterSetup] Level_01 boss encounter ready.");
        }

        // ------------------------------------------------------------------ data

        private static WaveDefinition CreateOrUpdateNormalWave()
        {
            EditorFolderUtility.EnsureFolder(DataFolder);
            var wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>(WavePath);
            if (wave == null)
            {
                wave = ScriptableObject.CreateInstance<WaveDefinition>();
                AssetDatabase.CreateAsset(wave, WavePath);
            }

            var so = new SerializedObject(wave);
            so.FindProperty("_id").stringValue = "level01_wave_01";
            so.FindProperty("_displayName").stringValue = "Wave 1";
            so.FindProperty("_preparationDurationOverride").floatValue = -1f;
            SerializedProperty groups = so.FindProperty("_spawnGroups");
            groups.arraySize = 1;
            SerializedProperty group = groups.GetArrayElementAtIndex(0);
            SerializedProperty entries = group.FindPropertyRelative("_entries");
            entries.arraySize = 1;
            SetEntry(entries.GetArrayElementAtIndex(0), NormalDefinitionPath, 10);
            group.FindPropertyRelative("_delayBeforeGroup").floatValue = 0f;
            group.FindPropertyRelative("_spawnInterval").floatValue = 1.3f;
            group.FindPropertyRelative("_interleaveEntries").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(wave);
            return wave;
        }

        private static BossEncounterDefinition CreateOrUpdateEncounter()
        {
            var encounter = AssetDatabase.LoadAssetAtPath<BossEncounterDefinition>(EncounterPath);
            if (encounter == null)
            {
                encounter = ScriptableObject.CreateInstance<BossEncounterDefinition>();
                AssetDatabase.CreateAsset(encounter, EncounterPath);
            }

            var so = new SerializedObject(encounter);
            so.FindProperty("_bossDefinition").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(BossDefinitionPath);
            SetSlot(so.FindProperty("_bossSlot"), 12f, 0f);
            so.FindProperty("_bossMinionsEnabled").boolValue = false;

            SerializedProperty escorts = so.FindProperty("_escorts");
            escorts.arraySize = 2;
            SetEntry(escorts.GetArrayElementAtIndex(0), NormalDefinitionPath, 8);
            SetEntry(escorts.GetArrayElementAtIndex(1), RunnerDefinitionPath, 2);

            // Path runs from its first waypoint toward the base (larger distance = nearer the base, i.e. in front
            // of the boss). The boss leads; the escorts fan out behind it, so the intro camera (placed in front of
            // the boss) sees the boss with its army at its back and nobody blocking its body.
            var slots = new (float d, float x)[]
            {
                (9.4f, -3.0f), (9.4f, 3.0f),         // shoulder pair (Normal)
                (7.3f, -1.6f), (7.3f, 1.6f),         // inner second row (Normal)
                (5.6f, -3.3f), (5.6f, 3.3f),         // outer second row (Normal)
                (3.6f, -1.7f), (3.6f, 1.7f),         // rear pair (Normal)
                (1.6f, -3.0f), (1.6f, 3.0f),         // runners at the back
            };
            SerializedProperty slotArray = so.FindProperty("_escortSlots");
            slotArray.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
            {
                SetSlot(slotArray.GetArrayElementAtIndex(i), slots[i].d, slots[i].x);
            }

            so.FindProperty("_delayAfterNormalWaves").floatValue = 0.75f;
            so.FindProperty("_bossCountdown").floatValue = 60f;
            so.FindProperty("_debugSkipNormalWaves").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(encounter);
            return encounter;
        }

        private static void AssignToLevel(WaveDefinition wave, BossEncounterDefinition encounter)
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelDefinitionPath);
            var so = new SerializedObject(level);
            SerializedProperty waves = so.FindProperty("_waves");
            waves.arraySize = 1;
            waves.GetArrayElementAtIndex(0).objectReferenceValue = wave;
            so.FindProperty("_bossEncounter").objectReferenceValue = encounter;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
        }

        private static void SetEntry(SerializedProperty entry, string definitionPath, int count)
        {
            entry.FindPropertyRelative("_enemyDefinition").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(definitionPath);
            entry.FindPropertyRelative("_count").intValue = count;
        }

        private static void SetSlot(SerializedProperty slot, float distance, float lateral)
        {
            slot.FindPropertyRelative("DistanceAlongPath").floatValue = distance;
            slot.FindPropertyRelative("LateralOffset").floatValue = lateral;
        }

        // ------------------------------------------------------------------ boss prefab

        private static void FixBossPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            try
            {
                Transform model = root.transform.Find("VisualRoot/Model");
                if (model != null)
                {
                    model.localScale = Vector3.one * BossModelScale;
                    model.localPosition = new Vector3(0f, BossModelY, 0f);
                }

                var capsule = root.GetComponent<CapsuleCollider>();
                if (capsule != null)
                {
                    capsule.center = new Vector3(0f, 2.4f, 0f);
                    capsule.radius = 1.05f;
                    capsule.height = 4.8f;
                }

                SetLocalY(root.transform, "TargetPoint", 3.0f);   // chest - where towers aim
                SetLocalY(root.transform, "HealthBarAnchor", 5.9f);

                Transform focus = root.transform.Find("BossFocusPoint");
                if (focus == null)
                {
                    focus = new GameObject("BossFocusPoint").transform;
                    focus.SetParent(root.transform, false);
                }

                focus.localPosition = new Vector3(0f, 3.5f, 0f);  // chest/head - the intro camera looks here
                focus.localRotation = Quaternion.identity;

                PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetLocalY(Transform root, string childName, float y)
        {
            Transform child = root.Find(childName);
            if (child != null)
            {
                child.localPosition = new Vector3(child.localPosition.x, y, child.localPosition.z);
            }
        }

        // ------------------------------------------------------------------ scene

        private static void SetupScene()
        {
            var compositionRoot = Object.FindFirstObjectByType<LevelCompositionRoot>();
            var waveController = Object.FindFirstObjectByType<WaveController>();
            var cameraController = Object.FindFirstObjectByType<TopDownCameraController>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var tractorBeam = Object.FindFirstObjectByType<UFOTractorBeamController>();
            var proximity = Object.FindFirstObjectByType<PlayerBuildNodeProximityController>();
            Canvas canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay);
            Transform safeArea = canvas.transform.Find("SafeArea");
            if (compositionRoot == null || waveController == null || cameraController == null || safeArea == null)
            {
                Debug.LogError("[Level01BossEncounterSetup] Level_01 scene is missing a required object.");
                return;
            }

            var rootSo = new SerializedObject(compositionRoot);
            Transform cameraTransform = rootSo.FindProperty("_cameraTransform").objectReferenceValue as Transform;

            var hudGroup = safeArea.GetComponent<CanvasGroup>();
            if (hudGroup == null)
            {
                hudGroup = safeArea.gameObject.AddComponent<CanvasGroup>();
            }

            BossHealthBarPresenter bossBarPresenter = EnsureBossHealthBar(safeArea);
            BossIntroPanelView panel = EnsureBossIntroPanel(canvas.transform);

            GameObject introGo = GameObject.Find("BossIntroController");
            if (introGo == null)
            {
                introGo = new GameObject("BossIntroController");
                Transform systems = GameObject.Find("Systems")?.transform;
                if (systems != null)
                {
                    introGo.transform.SetParent(systems, false);
                }
            }

            var intro = introGo.GetComponent<BossIntroController>();
            if (intro == null)
            {
                intro = introGo.AddComponent<BossIntroController>();
            }

            var introSo = new SerializedObject(intro);
            introSo.FindProperty("_cameraController").objectReferenceValue = cameraController;
            introSo.FindProperty("_cameraTransform").objectReferenceValue = cameraTransform;
            introSo.FindProperty("_player").objectReferenceValue = player;
            introSo.FindProperty("_tractorBeam").objectReferenceValue = tractorBeam;
            introSo.FindProperty("_buildProximity").objectReferenceValue = proximity;
            introSo.FindProperty("_joystickRoot").objectReferenceValue = safeArea.Find("BottomControls")?.gameObject;
            introSo.FindProperty("_hudCanvasGroup").objectReferenceValue = hudGroup;
            introSo.FindProperty("_panel").objectReferenceValue = panel;
            introSo.ApplyModifiedPropertiesWithoutUndo();

            rootSo.FindProperty("_bossIntroController").objectReferenceValue = intro;
            rootSo.FindProperty("_bossHealthBarPresenter").objectReferenceValue = bossBarPresenter;
            rootSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        private static BossHealthBarPresenter EnsureBossHealthBar(Transform safeArea)
        {
            Transform existing = safeArea.Find("BossHealthBar");
            GameObject bar;
            if (existing != null)
            {
                bar = existing.gameObject;
            }
            else
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossHealthBarPrefabPath);
                bar = (GameObject)PrefabUtility.InstantiatePrefab(prefab, safeArea);
                bar.name = "BossHealthBar";
            }

            // Just under the top HUD (level bar / resource badges), above the minimap.
            var rect = (RectTransform)bar.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            // Narrow enough to sit between the energy badge (left) and the UFO badge (right).
            rect.anchoredPosition = new Vector2(0f, -330f);
            rect.sizeDelta = new Vector2(560f, 90f);
            bar.transform.SetSiblingIndex(1);
            return bar.GetComponentInChildren<BossHealthBarPresenter>(true);
        }

        private static BossIntroPanelView EnsureBossIntroPanel(Transform canvas)
        {
            Transform existing = canvas.Find("BossIntroPanel");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            TMP_FontAsset font = AssetDatabase.FindAssets("Fredoka-Bold SDF t:TMP_FontAsset")
                .Select(g => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();

            var panelGo = new GameObject("BossIntroPanel", typeof(RectTransform), typeof(CanvasGroup));
            panelGo.transform.SetParent(canvas, false);
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            var group = panelGo.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // Tilted warning band in the lower third, clear of the boss's head and the notch.
            RectTransform band = CreateImage("Band", panelRect, new Color(0.78f, 0.06f, 0.06f, 0.96f));
            band.anchorMin = band.anchorMax = new Vector2(0.5f, 0.5f);
            band.anchoredPosition = new Vector2(0f, -700f);
            band.sizeDelta = new Vector2(1700f, 330f);
            band.localRotation = Quaternion.Euler(0f, 0f, 7f);

            RectTransform inner = CreateImage("Inner", band, new Color(0.07f, 0.35f, 0.75f, 0.97f));
            inner.anchorMin = new Vector2(0f, 0.5f);
            inner.anchorMax = new Vector2(1f, 0.5f);
            inner.sizeDelta = new Vector2(0f, 210f);
            inner.anchoredPosition = Vector2.zero;

            RectTransform tickerTop = CreateText("TickerTop", band, font, "DANGEROUS OPPONENT   !   DANGEROUS OPPONENT   !   DANGEROUS OPPONENT   !   DANGEROUS OPPONENT",
                30f, new Color(1f, 0.85f, 0.2f), FontStyles.Bold);
            tickerTop.anchorMin = tickerTop.anchorMax = new Vector2(0.5f, 1f);
            tickerTop.sizeDelta = new Vector2(2400f, 60f);
            tickerTop.anchoredPosition = new Vector2(0f, -30f);

            RectTransform tickerBottom = CreateText("TickerBottom", band, font, "ATTENTION   !   BOSS INCOMING   !   ATTENTION   !   BOSS INCOMING   !   ATTENTION",
                30f, new Color(1f, 0.85f, 0.2f), FontStyles.Bold);
            tickerBottom.anchorMin = tickerBottom.anchorMax = new Vector2(0.5f, 0f);
            tickerBottom.sizeDelta = new Vector2(2400f, 60f);
            tickerBottom.anchoredPosition = new Vector2(0f, 30f);

            RectTransform bossLabel = CreateText("BossLabel", inner, font, "BOSS", 58f, new Color(1f, 0.32f, 0.2f), FontStyles.Bold);
            bossLabel.anchorMin = bossLabel.anchorMax = new Vector2(0.5f, 0.5f);
            bossLabel.sizeDelta = new Vector2(900f, 70f);
            bossLabel.anchoredPosition = new Vector2(0f, 52f);

            RectTransform nameRect = CreateText("BossName", inner, font, "Boss", 96f, Color.white, FontStyles.Bold);
            nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.5f, 0.5f);
            nameRect.sizeDelta = new Vector2(1100f, 120f);
            nameRect.anchoredPosition = new Vector2(0f, -28f);
            var nameText = nameRect.GetComponent<TextMeshProUGUI>();
            nameText.outlineWidth = 0.22f;
            nameText.outlineColor = new Color32(20, 20, 40, 255);

            var view = panelGo.AddComponent<BossIntroPanelView>();
            var so = new SerializedObject(view);
            so.FindProperty("_canvasGroup").objectReferenceValue = group;
            so.FindProperty("_band").objectReferenceValue = band;
            so.FindProperty("_bossNameText").objectReferenceValue = nameText;
            SerializedProperty tickers = so.FindProperty("_tickers");
            tickers.arraySize = 2;
            tickers.GetArrayElementAtIndex(0).objectReferenceValue = tickerTop;
            tickers.GetArrayElementAtIndex(1).objectReferenceValue = tickerBottom;
            so.ApplyModifiedPropertiesWithoutUndo();

            panelGo.transform.SetAsLastSibling();
            panelGo.SetActive(false);
            return view;
        }

        private static RectTransform CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        private static RectTransform CreateText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                tmp.font = font;
            }

            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            return (RectTransform)go.transform;
        }
    }
}
