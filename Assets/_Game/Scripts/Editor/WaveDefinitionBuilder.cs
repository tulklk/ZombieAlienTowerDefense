using AlienDefense.Enemies;
using AlienDefense.Waves;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds (or reuses) the ten Phase 4 WaveDefinition assets from the Phase 3 enemy definitions.</summary>
    internal static class WaveDefinitionBuilder
    {
        private const string DataFolder = "Assets/_Game/Data/Waves";
        private const string NormalDefinitionPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Normal.asset";
        private const string RunnerDefinitionPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Runner.asset";
        private const string TankDefinitionPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Tank.asset";

        private struct GroupSpec
        {
            public EnemyKind Enemy;
            public int Count;
            public float DelayBeforeGroup;
            public float SpawnInterval;

            public GroupSpec(EnemyKind enemy, int count, float delayBeforeGroup, float spawnInterval)
            {
                Enemy = enemy;
                Count = count;
                DelayBeforeGroup = delayBeforeGroup;
                SpawnInterval = spawnInterval;
            }
        }

        private enum EnemyKind { Normal, Runner, Tank }

        private struct WaveSpec
        {
            public float PreparationOverride;
            public GroupSpec[] Groups;

            public WaveSpec(float preparationOverride, params GroupSpec[] groups)
            {
                PreparationOverride = preparationOverride;
                Groups = groups;
            }
        }

        private static readonly WaveSpec[] Waves =
        {
            new WaveSpec(3f,
                new GroupSpec(EnemyKind.Normal, 5, 0f, 1.0f)),

            new WaveSpec(4f,
                new GroupSpec(EnemyKind.Normal, 8, 0f, 0.85f)),

            new WaveSpec(-1f,
                new GroupSpec(EnemyKind.Normal, 6, 0f, 0.8f),
                new GroupSpec(EnemyKind.Runner, 3, 1.5f, 0.9f)),

            new WaveSpec(-1f,
                new GroupSpec(EnemyKind.Normal, 8, 0f, 0.7f),
                new GroupSpec(EnemyKind.Runner, 5, 1.0f, 0.65f)),

            new WaveSpec(-1f,
                new GroupSpec(EnemyKind.Tank, 2, 0f, 1.4f),
                new GroupSpec(EnemyKind.Normal, 10, 1.5f, 0.65f)),

            new WaveSpec(-1f,
                new GroupSpec(EnemyKind.Runner, 10, 0f, 0.5f),
                new GroupSpec(EnemyKind.Normal, 8, 1.0f, 0.6f)),

            new WaveSpec(-1f,
                new GroupSpec(EnemyKind.Tank, 4, 0f, 1.2f),
                new GroupSpec(EnemyKind.Runner, 8, 1.0f, 0.55f)),

            new WaveSpec(-1f,
                new GroupSpec(EnemyKind.Normal, 12, 0f, 0.45f),
                new GroupSpec(EnemyKind.Runner, 10, 1.0f, 0.45f),
                new GroupSpec(EnemyKind.Tank, 3, 1.5f, 1.0f)),

            new WaveSpec(-1f,
                new GroupSpec(EnemyKind.Tank, 5, 0f, 0.9f),
                new GroupSpec(EnemyKind.Runner, 12, 1.0f, 0.4f),
                new GroupSpec(EnemyKind.Normal, 12, 1.0f, 0.4f)),

            new WaveSpec(-1f,
                new GroupSpec(EnemyKind.Tank, 8, 0f, 0.75f),
                new GroupSpec(EnemyKind.Runner, 15, 1.0f, 0.35f),
                new GroupSpec(EnemyKind.Normal, 15, 1.0f, 0.35f)),
        };

        [MenuItem("AlienDefense/Setup/6. Create 10 Wave Definitions")]
        public static WaveDefinition[] CreateAll()
        {
            EnemyPrefabBuilder.CreateAll();
            EditorFolderUtility.EnsureFolder(DataFolder);

            var normal = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(NormalDefinitionPath);
            var runner = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(RunnerDefinitionPath);
            var tank = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(TankDefinitionPath);

            var result = new WaveDefinition[Waves.Length];
            for (int i = 0; i < Waves.Length; i++)
            {
                result[i] = CreateOrLoadWave(i + 1, Waves[i], normal, runner, tank);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[AlienDefense Setup] 10 wave definitions ready.");
            return result;
        }

        private static WaveDefinition CreateOrLoadWave(int waveNumber, WaveSpec spec, EnemyDefinition normal, EnemyDefinition runner, EnemyDefinition tank)
        {
            string id = $"wave_{waveNumber:00}";
            string path = $"{DataFolder}/Wave_{waveNumber:00}.asset";

            var wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>(path);
            if (wave == null)
            {
                wave = ScriptableObject.CreateInstance<WaveDefinition>();
                AssetDatabase.CreateAsset(wave, path);
            }

            var serializedWave = new SerializedObject(wave);
            serializedWave.FindProperty("_id").stringValue = id;
            serializedWave.FindProperty("_displayName").stringValue = $"Wave {waveNumber}";
            serializedWave.FindProperty("_preparationDurationOverride").floatValue = spec.PreparationOverride;

            SerializedProperty groupsProperty = serializedWave.FindProperty("_spawnGroups");
            groupsProperty.arraySize = spec.Groups.Length;
            for (int i = 0; i < spec.Groups.Length; i++)
            {
                GroupSpec groupSpec = spec.Groups[i];
                EnemyDefinition definition = groupSpec.Enemy switch
                {
                    EnemyKind.Normal => normal,
                    EnemyKind.Runner => runner,
                    EnemyKind.Tank => tank,
                    _ => null
                };

                SerializedProperty groupProperty = groupsProperty.GetArrayElementAtIndex(i);
                groupProperty.FindPropertyRelative("_enemyDefinition").objectReferenceValue = definition;
                groupProperty.FindPropertyRelative("_count").intValue = groupSpec.Count;
                groupProperty.FindPropertyRelative("_delayBeforeGroup").floatValue = groupSpec.DelayBeforeGroup;
                groupProperty.FindPropertyRelative("_spawnInterval").floatValue = groupSpec.SpawnInterval;
            }

            serializedWave.ApplyModifiedPropertiesWithoutUndo();
            return wave;
        }
    }
}
