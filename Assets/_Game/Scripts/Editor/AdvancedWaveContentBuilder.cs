using AlienDefense.Data;
using AlienDefense.Enemies;
using AlienDefense.Waves;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds one extra WaveDefinition (Armored + Shield + Boss) and appends it to Level_01's wave list.</summary>
    internal static class AdvancedWaveContentBuilder
    {
        private const string DataFolder = "Assets/_Game/Data/Waves";
        private const string WavePath = DataFolder + "/Wave_11_Advanced.asset";
        private const string ArmoredDefinitionPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Armored.asset";
        private const string ShieldDefinitionPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Shield.asset";
        private const string BossDefinitionPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Boss.asset";
        private const string Level01Path = "Assets/_Game/Data/Levels/Level_01_Definition.asset";

        [MenuItem("AlienDefense/Setup/22. Add Advanced Wave (Armored/Shield/Boss) To Level_01")]
        public static void AddAdvancedWaveToLevel01()
        {
            AdvancedEnemyPrefabBuilder.CreateAll();
            BossContentBuilder.CreateAll();
            EditorFolderUtility.EnsureFolder(DataFolder);

            var armored = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(ArmoredDefinitionPath);
            var shield = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(ShieldDefinitionPath);
            var boss = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(BossDefinitionPath);

            if (armored == null || shield == null || boss == null)
            {
                Debug.LogError("[AlienDefense Setup] Could not resolve one of the advanced EnemyDefinitions; aborting.");
                return;
            }

            WaveDefinition wave = CreateOrLoadWave(armored, shield, boss);

            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level01Path);
            if (level == null)
            {
                Debug.LogError($"[AlienDefense Setup] Level_01_Definition not found at {Level01Path}.");
                return;
            }

            var serializedLevel = new SerializedObject(level);
            SerializedProperty wavesProperty = serializedLevel.FindProperty("_waves");

            bool alreadyPresent = false;
            for (int i = 0; i < wavesProperty.arraySize; i++)
            {
                if (wavesProperty.GetArrayElementAtIndex(i).objectReferenceValue == wave)
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if (!alreadyPresent)
            {
                int index = wavesProperty.arraySize;
                wavesProperty.arraySize++;
                wavesProperty.GetArrayElementAtIndex(index).objectReferenceValue = wave;
                serializedLevel.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log($"[AlienDefense Setup] Appended '{wave.name}' as wave #{index + 1} on Level_01.");
            }
            else
            {
                Debug.Log("[AlienDefense Setup] Wave_11_Advanced is already present on Level_01; nothing to do.");
            }
        }

        private static WaveDefinition CreateOrLoadWave(EnemyDefinition armored, EnemyDefinition shield, EnemyDefinition boss)
        {
            var wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>(WavePath);
            if (wave == null)
            {
                wave = ScriptableObject.CreateInstance<WaveDefinition>();
                AssetDatabase.CreateAsset(wave, WavePath);
            }

            var serializedWave = new SerializedObject(wave);
            serializedWave.FindProperty("_id").stringValue = "wave_11_advanced";
            serializedWave.FindProperty("_displayName").stringValue = "Wave 11 - Advanced";
            serializedWave.FindProperty("_preparationDurationOverride").floatValue = 4f;

            SerializedProperty groupsProperty = serializedWave.FindProperty("_spawnGroups");
            groupsProperty.arraySize = 3;

            SetGroup(groupsProperty.GetArrayElementAtIndex(0), armored, count: 4, delay: 0f, interval: 1.2f);
            SetGroup(groupsProperty.GetArrayElementAtIndex(1), shield, count: 4, delay: 1.5f, interval: 1.0f);
            SetGroup(groupsProperty.GetArrayElementAtIndex(2), boss, count: 1, delay: 3f, interval: 0f);

            serializedWave.ApplyModifiedPropertiesWithoutUndo();
            return wave;
        }

        private static void SetGroup(SerializedProperty groupProperty, EnemyDefinition definition, int count, float delay, float interval)
        {
            groupProperty.FindPropertyRelative("_delayBeforeGroup").floatValue = delay;
            groupProperty.FindPropertyRelative("_spawnInterval").floatValue = interval;
            groupProperty.FindPropertyRelative("_interleaveEntries").boolValue = false;
            groupProperty.FindPropertyRelative("_enemyDefinition").objectReferenceValue = null;
            groupProperty.FindPropertyRelative("_count").intValue = 1;

            SerializedProperty entriesProperty = groupProperty.FindPropertyRelative("_entries");
            entriesProperty.arraySize = 1;
            SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(0);
            entryProperty.FindPropertyRelative("_enemyDefinition").objectReferenceValue = definition;
            entryProperty.FindPropertyRelative("_count").intValue = count;
        }
    }
}
