using AlienDefense.Core;
using AlienDefense.Data;
using AlienDefense.Enemies;
using AlienDefense.UI;
using AlienDefense.Waves;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>Editor setup for the shared level start flow and Level_02's continuous invasion. Both menu items are
    /// idempotent: running them again updates what they created instead of adding duplicates.
    ///
    /// - Add Level Start Countdown: puts the big 3-2-1 (LevelStartCountdown + a TMP label using the project's
    ///   Fredoka title style) under Canvas/SafeArea of the open level scene and assigns it to LevelCompositionRoot.
    /// - Setup Level 02 Continuous Invasion: creates Data/Waves/Level02/Level02_Invasion and Level02_BossEncounter
    ///   (the Level_01 boss, data kept separate so tuning Level 2 never touches Level 1) and points
    ///   Level_02_Definition at them (no waves - so no Enemy_Tank - and max 30 alive).</summary>
    public static class LevelFlowSetup
    {
        private const string FontPath = "Assets/_Game/Font/Fredoka-Bold SDF.asset";
        private const string FontMaterialPath = "Assets/_Game/Font/Materials/Fredoka_Title.mat";
        private const string EnemiesFolder = "Assets/_Game/Data/Enemies/";
        private const string Level02WavesFolder = "Assets/_Game/Data/Waves/Level02";
        private const string InvasionPath = Level02WavesFolder + "/Level02_Invasion.asset";
        private const string BossEncounterPath = Level02WavesFolder + "/Level02_BossEncounter.asset";
        private const string Level02DefinitionPath = "Assets/_Game/Data/Levels/Level_02_Definition.asset";

        [MenuItem("Tools/Tower Defense/Levels/Add Level Start Countdown (open scene)")]
        public static void AddCountdownToActiveSceneFromMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            Debug.Log(AddCountdown(scene));
            EditorSceneManager.MarkSceneDirty(scene);
        }

        [MenuItem("Tools/Tower Defense/Levels/Setup Level 02 Continuous Invasion")]
        public static void SetupLevel02FromMenu()
        {
            Debug.Log(SetupLevel02Data());
        }

        // ------------------------------------------------------------------ countdown

        public static string AddCountdown(Scene scene)
        {
            LevelCompositionRoot compositionRoot = null;
            Transform safeArea = null;
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (compositionRoot == null)
                {
                    compositionRoot = go.GetComponentInChildren<LevelCompositionRoot>(true);
                }

                if (safeArea == null && go.name == "Canvas")
                {
                    safeArea = go.transform.Find("SafeArea");
                    if (safeArea == null)
                    {
                        safeArea = go.transform;
                    }
                }
            }

            if (compositionRoot == null || safeArea == null)
            {
                return "[LevelFlowSetup] " + scene.name + ": no LevelCompositionRoot or Canvas - nothing added.";
            }

            Transform existing = safeArea.Find("LevelStartCountdown");
            GameObject rootObject = existing != null ? existing.gameObject : new GameObject("LevelStartCountdown", typeof(RectTransform));
            var rootRect = (RectTransform)rootObject.transform;
            rootRect.SetParent(safeArea, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.SetAsLastSibling();
            rootObject.layer = safeArea.gameObject.layer;
            rootObject.SetActive(true);

            Transform labelTransform = rootRect.Find("Label");
            GameObject labelObject = labelTransform != null
                ? labelTransform.gameObject
                : new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(rootRect, false);
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = new Vector2(0f, 330f); // above centre, so the digit sits over the road ahead of the UFO
            labelRect.sizeDelta = new Vector2(1000f, 900f);
            labelObject.layer = rootObject.layer;

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(FontMaterialPath);
            if (material != null)
            {
                label.fontSharedMaterial = material; // black outline + soft underlay, the project's title style
            }

            label.text = "3";
            label.fontSize = 620f;
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = new Color(1f, 0.878f, 0.29f); // bright yellow #FFE04A
            label.raycastTarget = false;
            labelObject.SetActive(false);

            var countdown = rootObject.GetComponent<LevelStartCountdown>();
            if (countdown == null) // (Unity's fake-null: never use ?? on a GetComponent result)
            {
                countdown = rootObject.AddComponent<LevelStartCountdown>();
            }
            var countdownSo = new SerializedObject(countdown);
            countdownSo.FindProperty("_label").objectReferenceValue = label;
            countdownSo.FindProperty("_root").objectReferenceValue = labelObject;
            countdownSo.ApplyModifiedPropertiesWithoutUndo();

            var rootSo = new SerializedObject(compositionRoot);
            rootSo.FindProperty("_levelStartCountdown").objectReferenceValue = countdown;
            rootSo.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            return "[LevelFlowSetup] " + scene.name + ": LevelStartCountdown at " + safeArea.name + "/LevelStartCountdown, assigned to " + compositionRoot.name + "._levelStartCountdown.";
        }

        // ------------------------------------------------------------------ Level 02 data

        public static string SetupLevel02Data()
        {
            if (!AssetDatabase.IsValidFolder(Level02WavesFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Data/Waves", "Level02");
            }

            EnemyDefinition normal = LoadEnemy("Normal");
            EnemyDefinition runner = LoadEnemy("Runner");
            EnemyDefinition armored = LoadEnemy("Armored");
            EnemyDefinition shield = LoadEnemy("Shield");
            EnemyDefinition boss = LoadEnemy("Boss");
            if (normal == null || runner == null || armored == null || shield == null || boss == null)
            {
                return "[LevelFlowSetup] Missing an EnemyDefinition under " + EnemiesFolder + " - nothing changed.";
            }

            // Continuous invasion: 240 s, 1.15 -> 0.78 s per enemy, four types only (no Enemy_Tank), six mini bosses.
            var invasion = LoadOrCreate<ContinuousInvasionDefinition>(InvasionPath);
            var so = new SerializedObject(invasion);
            so.FindProperty("_bossCountdown").floatValue = 240f;
            so.FindProperty("_spawnIntervalStart").floatValue = 1.15f;
            so.FindProperty("_spawnIntervalEnd").floatValue = 0.78f;
            so.FindProperty("_debugLogs").boolValue = true;

            SerializedProperty enemies = so.FindProperty("_enemies");
            enemies.arraySize = 4;
            SetEnemy(enemies.GetArrayElementAtIndex(0), normal, 65f, 30f, 0f, 4, 1);
            SetEnemy(enemies.GetArrayElementAtIndex(1), runner, 30f, 25f, 0f, 3, 1);
            SetEnemy(enemies.GetArrayElementAtIndex(2), armored, 5f, 25f, 30f, 2, 2);
            SetEnemy(enemies.GetArrayElementAtIndex(3), shield, 0f, 20f, 60f, 2, 2);

            SerializedProperty miniBosses = so.FindProperty("_miniBosses");
            miniBosses.arraySize = 6;
            SetMiniBoss(miniBosses.GetArrayElementAtIndex(0), 35f, normal, 1.25f, 3f, 0.95f, 2);
            SetMiniBoss(miniBosses.GetArrayElementAtIndex(1), 70f, runner, 1.25f, 4f, 0.90f, 2);
            SetMiniBoss(miniBosses.GetArrayElementAtIndex(2), 105f, armored, 1.30f, 3.5f, 0.90f, 3);
            SetMiniBoss(miniBosses.GetArrayElementAtIndex(3), 140f, shield, 1.30f, 4f, 0.90f, 3);
            SetMiniBoss(miniBosses.GetArrayElementAtIndex(4), 175f, armored, 1.35f, 5f, 0.90f, 4);
            SetMiniBoss(miniBosses.GetArrayElementAtIndex(5), 210f, shield, 1.40f, 6f, 0.85f, 4);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(invasion);

            // Final boss: the Level_01 boss, in Level_02's own encounter asset. The invasion's 240 s timer brings it;
            // no escorts (whatever is still alive at 4:00 stays on the road with it).
            var encounter = LoadOrCreate<BossEncounterDefinition>(BossEncounterPath);
            var encounterSo = new SerializedObject(encounter);
            encounterSo.FindProperty("_bossDefinition").objectReferenceValue = boss;
            encounterSo.FindProperty("_bossSlot.DistanceAlongPath").floatValue = 12f;
            encounterSo.FindProperty("_bossSlot.LateralOffset").floatValue = 0f;
            encounterSo.FindProperty("_bossMinionsEnabled").boolValue = false;
            encounterSo.FindProperty("_escorts").arraySize = 0;
            encounterSo.FindProperty("_escortSlots").arraySize = 0;
            encounterSo.FindProperty("_delayAfterNormalWaves").floatValue = 0f;
            encounterSo.FindProperty("_bossCountdown").floatValue = 0f; // timed by Level02_Invasion instead
            encounterSo.FindProperty("_debugSkipNormalWaves").boolValue = false;
            encounterSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(encounter);

            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level02DefinitionPath);
            if (level == null)
            {
                return "[LevelFlowSetup] " + Level02DefinitionPath + " not found.";
            }

            var levelSo = new SerializedObject(level);
            levelSo.FindProperty("_waves").arraySize = 0; // the old 10 waves (Enemy_Tank in waves 5-10) are no longer used
            levelSo.FindProperty("_bossEncounter").objectReferenceValue = encounter;
            levelSo.FindProperty("_continuousInvasion").objectReferenceValue = invasion;
            levelSo.FindProperty("_maxAliveEnemies").intValue = 30;
            levelSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
            return "[LevelFlowSetup] Level 02: " + InvasionPath + ", " + BossEncounterPath + ", Level_02_Definition updated (no waves, max alive 30).";
        }

        private static EnemyDefinition LoadEnemy(string name)
        {
            return AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemiesFolder + "EnemyDefinition_Enemy_" + name + ".asset");
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        private static void SetEnemy(SerializedProperty p, EnemyDefinition enemy, float startWeight, float endWeight, float unlock, int maxConsecutive, int baseDamage)
        {
            p.FindPropertyRelative("Enemy").objectReferenceValue = enemy;
            p.FindPropertyRelative("StartWeight").floatValue = startWeight;
            p.FindPropertyRelative("EndWeight").floatValue = endWeight;
            p.FindPropertyRelative("UnlockTime").floatValue = unlock;
            p.FindPropertyRelative("MaxConsecutive").intValue = maxConsecutive;
            p.FindPropertyRelative("BaseDamage").intValue = baseDamage;
        }

        private static void SetMiniBoss(SerializedProperty p, float time, EnemyDefinition enemy, float scale, float health, float speed, int baseDamage)
        {
            p.FindPropertyRelative("Time").floatValue = time;
            p.FindPropertyRelative("Enemy").objectReferenceValue = enemy;
            p.FindPropertyRelative("Scale").floatValue = scale;
            p.FindPropertyRelative("HealthMultiplier").floatValue = health;
            p.FindPropertyRelative("SpeedMultiplier").floatValue = speed;
            p.FindPropertyRelative("BaseDamage").intValue = baseDamage;
        }
    }
}
