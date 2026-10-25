using System.Collections.Generic;
using AlienDefense.Core;
using AlienDefense.Enemies;
using AlienDefense.UI;
using AlienDefense.Waves;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Level 3's gameplay spawn flow and the "Base under attack!" warning, all as data + scene wiring:
    ///
    /// Level03_Invasion (ContinuousInvasionDefinition, 287 s = 4:47 of combat):
    ///   lanes   Left 0-60 s, Right 60-272 s, Left 160-284 s (left pauses 3 s before the boss so its entry reads)
    ///   mini bosses  40 s Left x1, 90 s Right x7 (2 s apart), 160 s Left x7 (2 s apart)
    ///   final boss   Level03_BossEncounter at 287 s, on the LEFT route (WaveController Boss Path)
    /// The regular mix (types, weights, unlocks) and the mini boss strength are Level 2's invasion values, reused
    /// unchanged; no enemy stat is edited. Level_03_Definition gets the invasion, no separate waves, and Level 2's
    /// Max Alive Enemies (30).
    ///
    /// The warning: WarningLayer under the HUD canvas (first child, so every HUD control draws over it) with a
    /// full-screen vignette Image and the TMP text, BaseThreatWarningView + BaseThreatWarningPresenter on it, and
    /// LevelCompositionRoot._baseThreatWarningPresenter pointing at it. Safe to run again.</summary>
    public static class Level03SpawnFlowSetup
    {
        private const string ScenePath = "Assets/_Game/Scenes/Levels/Level_03.unity";
        private const string InvasionPath = "Assets/_Game/Data/Waves/Level03/Level03_Invasion.asset";
        private const string Level2InvasionPath = "Assets/_Game/Data/Waves/Level02/Level02_Invasion.asset";
        private const string LevelDefinitionPath = "Assets/_Game/Data/Levels/Level_03_Definition.asset";
        private const string MiniBossPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Armored.asset";
        private const string NormalPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Normal.asset";
        private const string TankPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Tank.asset";
        private const int MiniBossLayers = 5;
        private const string ShaderName = "AlienDefense/UI/BaseWarningVignette";
        private const string MaterialPath = "Assets/_Game/Materials/UI/MAT_BaseWarningVignette.mat";
        private const string FontPath = "Assets/_Game/Font/Fredoka-Bold SDF.asset";
        private const string FontMaterialPath = "Assets/_Game/Font/Materials/Fredoka_HUD.mat";

        public const float BossTime = 287f;          // 4:47
        public const float RightStop = BossTime - 15f; // 4:32
        public const float LeftPause = BossTime - 3f;  // left lane goes quiet 3 s before the boss
        public const int MaxAlivePerLane = 40;           // regular enemies per route (both routes open: up to 80) - user asked for dense crowds, 2026-10-08

        [MenuItem("Tools/Tower Defense/Level 03/Setup Spawn Flow + Base Warning")]
        public static void SetupFromMenu()
        {
            Debug.Log(Setup());
        }

        public static string Setup()
        {
            if (EditorApplication.isPlaying)
            {
                return "[Level03SpawnFlowSetup] Stop Play mode first.";
            }

            var report = new List<string>();
            var invasion = EnsureInvasion(report);
            WireLevelDefinition(invasion, report);
            WireScene(report);
            AssetDatabase.SaveAssets();
            return "[Level03SpawnFlowSetup] Done.\n" + string.Join("\n", report);
        }

        private static ContinuousInvasionDefinition EnsureInvasion(List<string> report)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Data/Waves/Level03"))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Data/Waves", "Level03");
            }

            var invasion = AssetDatabase.LoadAssetAtPath<ContinuousInvasionDefinition>(InvasionPath);
            if (invasion == null)
            {
                AssetDatabase.CopyAsset(Level2InvasionPath, InvasionPath); // Level 2's regular mix, unchanged
                invasion = AssetDatabase.LoadAssetAtPath<ContinuousInvasionDefinition>(InvasionPath);
            }

            var so = new SerializedObject(invasion);
            so.FindProperty("_bossCountdown").floatValue = BossTime;
            so.FindProperty("_spawnIntervalStart").floatValue = 0.9f;
            so.FindProperty("_spawnIntervalEnd").floatValue = 0.7f;
            so.FindProperty("_maxAlivePerLane").intValue = MaxAlivePerLane;
            so.FindProperty("_packSizeMin").intValue = 2;   // crowds, as in the reference: 2-3 side by side per tick
            so.FindProperty("_packSizeMax").intValue = 3;
            so.FindProperty("_packStagger").floatValue = 0.8f;

            // Zombie looks per phase (user, 2026-10-09): the first left wave adds zombie 2 (Ghoul), the right lane
            // at 1:00 adds zombie 4 (Tank) as well, and from then on all of zombies 1, 2, 3, 4, 5, 7 come
            // (Normal, Ghoul, Runner, Tank, Shield, Armored). The phases follow each other in time, so unlock
            // times express this: Ghoul from 0 s, Tank from 60 s (Shield already unlocks at 60 s).
            SerializedProperty enemies = so.FindProperty("_enemies");
            EnsureEnemy(enemies, AssetDatabase.LoadAssetAtPath<EnemyDefinition>(Zombie2EnemySetup.DefinitionPath), 30f, 20f, 0f, 3, 1);
            EnsureEnemy(enemies, AssetDatabase.LoadAssetAtPath<EnemyDefinition>(TankPath), 8f, 15f, 60f, 2, 2);

            SerializedProperty lanes = so.FindProperty("_lanes");
            lanes.arraySize = 3;
            SetLane(lanes.GetArrayElementAtIndex(0), "Left", 0, 0f, 60f);
            SetLane(lanes.GetArrayElementAtIndex(1), "Right", 1, 60f, RightStop);
            SetLane(lanes.GetArrayElementAtIndex(2), "Left (re-assault)", 0, 160f, LeftPause);

            var miniBoss = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(MiniBossPath);
            SerializedProperty bosses = so.FindProperty("_miniBosses");
            bosses.arraySize = 3;
            // Strength = Level 2's Armored mini boss (health x3.5, speed x0.9, 3 base damage); size x2.
            SetMiniBoss(bosses.GetArrayElementAtIndex(0), 40f, miniBoss, 0, 1);
            SetMiniBoss(bosses.GetArrayElementAtIndex(1), 90f, miniBoss, 1, 7);
            SetMiniBoss(bosses.GetArrayElementAtIndex(2), 160f, miniBoss, 0, 7);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(invasion);
            report.Add("Invasion: " + InvasionPath + " - 287 s; lanes Left 0-60, Right 60-" + RightStop + ", Left 160-" + LeftPause
                + "; packs of 2-3 every 0.9-0.7 s; max " + MaxAlivePerLane + " regular alive per route; mini bosses 40 s L x1, 90 s R x7, 160 s L x7 (" + (miniBoss != null ? miniBoss.DisplayName : "MISSING") + ", 2 s apart).");
            return invasion;
        }

        private static void EnsureEnemy(SerializedProperty enemies, EnemyDefinition definition, float startWeight, float endWeight,
            float unlock, int maxConsecutive, int baseDamage)
        {
            if (definition == null)
            {
                return;
            }

            SerializedProperty entry = null;
            for (int i = 0; i < enemies.arraySize; i++)
            {
                if (enemies.GetArrayElementAtIndex(i).FindPropertyRelative("Enemy").objectReferenceValue == definition)
                {
                    entry = enemies.GetArrayElementAtIndex(i);
                }
            }

            if (entry == null)
            {
                enemies.arraySize++;
                entry = enemies.GetArrayElementAtIndex(enemies.arraySize - 1);
            }

            entry.FindPropertyRelative("Enemy").objectReferenceValue = definition;
            entry.FindPropertyRelative("StartWeight").floatValue = startWeight;
            entry.FindPropertyRelative("EndWeight").floatValue = endWeight;
            entry.FindPropertyRelative("UnlockTime").floatValue = unlock;
            entry.FindPropertyRelative("MaxConsecutive").intValue = maxConsecutive;
            entry.FindPropertyRelative("BaseDamage").intValue = baseDamage;
        }

        private static void SetLane(SerializedProperty lane, string label, int path, float start, float end)
        {
            lane.FindPropertyRelative("Label").stringValue = label;
            lane.FindPropertyRelative("PathIndex").intValue = path;
            lane.FindPropertyRelative("StartTime").floatValue = start;
            lane.FindPropertyRelative("EndTime").floatValue = end;
        }

        private static void SetMiniBoss(SerializedProperty entry, float time, EnemyDefinition enemy, int path, int count)
        {
            entry.FindPropertyRelative("Time").floatValue = time;
            entry.FindPropertyRelative("Enemy").objectReferenceValue = enemy;
            entry.FindPropertyRelative("Scale").floatValue = 2f; // clearly bigger than a regular enemy, well below the boss (user, 2026-10-09)
            // Health = MiniBossLayers regular (Normal) zombies, shown as an "xN" layered bar - easier for the
            // towers than the old x3.5 Armored, still clearly tougher than any regular enemy (user, 2026-10-09).
            var normal = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(NormalPath);
            float normalHealth = normal != null ? normal.MaxHealth : 100f;
            entry.FindPropertyRelative("HealthMultiplier").floatValue = enemy != null ? MiniBossLayers * normalHealth / enemy.MaxHealth : 1f;
            entry.FindPropertyRelative("HealthLayers").intValue = MiniBossLayers;
            entry.FindPropertyRelative("SpeedMultiplier").floatValue = 0.9f;
            entry.FindPropertyRelative("BaseDamage").intValue = 3;
            entry.FindPropertyRelative("PathIndex").intValue = path;
            entry.FindPropertyRelative("Count").intValue = count;
            entry.FindPropertyRelative("Spacing").floatValue = 2f;
        }

        private static void WireLevelDefinition(ContinuousInvasionDefinition invasion, List<string> report)
        {
            Object level = AssetDatabase.LoadMainAssetAtPath(LevelDefinitionPath);
            var so = new SerializedObject(level);
            so.FindProperty("_continuousInvasion").objectReferenceValue = invasion;
            so.FindProperty("_waves").arraySize = 0;
            so.FindProperty("_maxAliveEnemies").intValue = 30;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
            report.Add("Level_03_Definition: Continuous Invasion = Level03_Invasion, waves cleared (as Level 2), Max Alive Enemies 30 (Level 2's value), Boss Encounter kept.");
        }

        private static void WireScene(List<string> report)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Final boss on the LEFT route.
            var waves = Object.FindFirstObjectByType<WaveController>(FindObjectsInactive.Include);
            var wavesSo = new SerializedObject(waves);
            Object left = wavesSo.FindProperty("_path").objectReferenceValue;
            wavesSo.FindProperty("_bossPath").objectReferenceValue = left;
            wavesSo.ApplyModifiedPropertiesWithoutUndo();
            report.Add("WaveController: Path (0) = " + (left != null ? left.name : "none") + ", Additional Path (1) = right route, Boss Path = " + (left != null ? left.name : "none") + ".");

            // HUD warning layer.
            Canvas canvas = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Canvas")
                {
                    canvas = root.GetComponent<Canvas>();
                }
            }

            if (canvas == null)
            {
                report.Add("WARNING: no root 'Canvas' in Level_03 - warning UI not created.");
                EditorSceneManager.SaveScene(scene);
                return;
            }

            Transform layer = canvas.transform.Find("WarningLayer");
            if (layer == null)
            {
                layer = new GameObject("WarningLayer", typeof(RectTransform)).transform;
                layer.SetParent(canvas.transform, false);
            }

            layer.SetSiblingIndex(0); // every HUD control draws on top of it
            Stretch((RectTransform)layer);

            Image vignette = GetOrAdd<Image>(EnsureChild(layer, "BaseWarningVignette").gameObject);
            Stretch(vignette.rectTransform);
            vignette.material = EnsureMaterial();
            vignette.raycastTarget = false;
            vignette.color = new Color(1f, 1f, 1f, 0f);
            vignette.transform.SetSiblingIndex(0);

            RectTransform textRect = EnsureChild(layer, "BaseUnderAttackText");
            TextMeshProUGUI text = GetOrAdd<TextMeshProUGUI>(textRect.gameObject);
            textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.77f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(900f, 140f);
            text.text = "Base under attack!";
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var fontMaterial = AssetDatabase.LoadAssetAtPath<Material>(FontMaterialPath);
            if (fontMaterial != null)
            {
                text.fontSharedMaterial = fontMaterial; // white face, dark outline (the HUD's own preset)
            }

            text.fontSize = 72f;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.raycastTarget = false;

            var view = GetOrAdd<BaseThreatWarningView>(layer.gameObject);
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("_text").objectReferenceValue = text;
            viewSo.FindProperty("_vignette").objectReferenceValue = vignette;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            var presenter = GetOrAdd<BaseThreatWarningPresenter>(layer.gameObject);
            var presenterSo = new SerializedObject(presenter);
            presenterSo.FindProperty("_view").objectReferenceValue = view;
            presenterSo.ApplyModifiedPropertiesWithoutUndo();

            var compositionRoot = Object.FindFirstObjectByType<LevelCompositionRoot>(FindObjectsInactive.Include);
            var rootSo = new SerializedObject(compositionRoot);
            rootSo.FindProperty("_baseThreatWarningPresenter").objectReferenceValue = presenter;
            rootSo.ApplyModifiedPropertiesWithoutUndo();

            text.gameObject.SetActive(false);
            vignette.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.Add("HUD: Canvas/WarningLayer (first child) with BaseWarningVignette + BaseUnderAttackText, view + presenter, wired to LevelCompositionRoot.");
        }

        private static Material EnsureMaterial()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Materials/UI"))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Materials", "UI");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find(ShaderName));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            return material;
        }

        // Not "??": a missing component can come back as Unity's fake null in the editor.
        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static RectTransform EnsureChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name, typeof(RectTransform)).transform;
                child.SetParent(parent, false);
            }

            return (RectTransform)child;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
