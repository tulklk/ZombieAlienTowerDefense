using System.IO;
using AlienDefense.Data;
using AlienDefense.Enemies;
using AlienDefense.Waves;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds (or rewrites) the ten Level_01 WaveDefinition assets with multi-archetype groups.</summary>
    internal static class WaveDefinitionBuilder
    {
        private const string DataFolder = "Assets/_Game/Data/Waves";
        private const string NormalDefinitionPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Normal.asset";
        private const string RunnerDefinitionPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Runner.asset";
        private const string TankDefinitionPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Tank.asset";
        private const string ArmoredDefinitionPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Armored.asset";
        private const string Level01Path = "Assets/_Game/Data/Levels/Level_01_Definition.asset";
        private const string AutoRunRequestPath = "Assets/_Game/EditorReports/RebuildWaves.request";

        [InitializeOnLoadMethod]
        private static void AutoRunIfRequested()
        {
            if (!File.Exists(AutoRunRequestPath))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(AutoRunRequestPath))
                {
                    return;
                }

                try
                {
                    File.Delete(AutoRunRequestPath);
                    string metaPath = AutoRunRequestPath + ".meta";
                    if (File.Exists(metaPath))
                    {
                        File.Delete(metaPath);
                    }
                }
                catch
                {
                    // Ignore delete races.
                }

                CreateAll();
                ConfigureLevel01();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            };
        }

        [MenuItem("AlienDefense/Setup/Configure Level_01 Wave Difficulty")]
        public static void ConfigureLevel01()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level01Path);
            if (level == null)
            {
                Debug.LogError($"[AlienDefense Setup] Missing Level_01 at {Level01Path}");
                return;
            }

            var so = new SerializedObject(level);
            so.FindProperty("_preparationDuration").floatValue = 4f;
            so.FindProperty("_maxAliveEnemies").intValue = 15;
            so.FindProperty("_healthPerWaveStep").floatValue = 0.10f;
            so.FindProperty("_speedPerWaveStep").floatValue = 0.015f;
            so.FindProperty("_damagePerWaveStep").floatValue = 0.07f;
            so.FindProperty("_maxSpeedMultiplier").floatValue = 1.22f;
            so.FindProperty("_debugWaveLogs").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
            Debug.Log("[AlienDefense Setup] Level_01 wave difficulty configured (maxAlive=15, prep=4s).");
        }

        private struct EntrySpec
        {
            public EnemyKind Enemy;
            public int Count;

            public EntrySpec(EnemyKind enemy, int count)
            {
                Enemy = enemy;
                Count = count;
            }
        }

        private struct GroupSpec
        {
            public float DelayBeforeGroup;
            public float SpawnInterval;
            public bool Interleave;
            public EntrySpec[] Entries;

            public GroupSpec(float delayBeforeGroup, float spawnInterval, bool interleave, params EntrySpec[] entries)
            {
                DelayBeforeGroup = delayBeforeGroup;
                SpawnInterval = spawnInterval;
                Interleave = interleave;
                Entries = entries;
            }
        }

        private enum EnemyKind { Normal, Runner, Tank, Armored }

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
            // Wave 1 — Intro
            new WaveSpec(3f,
                new GroupSpec(0f, 1.35f, false,
                    new EntrySpec(EnemyKind.Normal, 6))),

            // Wave 2
            new WaveSpec(4f,
                new GroupSpec(0f, 1.25f, false,
                    new EntrySpec(EnemyKind.Normal, 8))),

            // Wave 3 — Intro Fast
            new WaveSpec(-1f,
                new GroupSpec(0f, 1.15f, false,
                    new EntrySpec(EnemyKind.Normal, 6)),
                new GroupSpec(1.2f, 1.15f, false,
                    new EntrySpec(EnemyKind.Runner, 2))),

            // Wave 4
            new WaveSpec(-1f,
                new GroupSpec(0f, 1.10f, false,
                    new EntrySpec(EnemyKind.Normal, 7)),
                new GroupSpec(1.0f, 1.10f, true,
                    new EntrySpec(EnemyKind.Runner, 4),
                    new EntrySpec(EnemyKind.Normal, 2))),

            // Wave 5 — Intro Tank (tank mid-wave)
            new WaveSpec(-1f,
                new GroupSpec(0f, 1.00f, true,
                    new EntrySpec(EnemyKind.Normal, 4),
                    new EntrySpec(EnemyKind.Runner, 2)),
                new GroupSpec(1.0f, 1.00f, false,
                    new EntrySpec(EnemyKind.Tank, 1)),
                new GroupSpec(1.0f, 1.00f, true,
                    new EntrySpec(EnemyKind.Normal, 2),
                    new EntrySpec(EnemyKind.Runner, 1))),

            // Wave 6
            new WaveSpec(-1f,
                new GroupSpec(0f, 0.95f, true,
                    new EntrySpec(EnemyKind.Normal, 7),
                    new EntrySpec(EnemyKind.Runner, 4)),
                new GroupSpec(1.2f, 1.10f, false,
                    new EntrySpec(EnemyKind.Tank, 2))),

            // Wave 7
            new WaveSpec(-1f,
                new GroupSpec(0f, 0.90f, true,
                    new EntrySpec(EnemyKind.Normal, 5),
                    new EntrySpec(EnemyKind.Runner, 6)),
                new GroupSpec(1.0f, 1.05f, false,
                    new EntrySpec(EnemyKind.Tank, 1)),
                new GroupSpec(0.8f, 1.05f, false,
                    new EntrySpec(EnemyKind.Tank, 1)),
                new GroupSpec(0.8f, 1.05f, false,
                    new EntrySpec(EnemyKind.Tank, 1))),

            // Wave 8 — Heavy + Elite (Armored)
            new WaveSpec(-1f,
                new GroupSpec(0f, 0.85f, true,
                    new EntrySpec(EnemyKind.Normal, 7),
                    new EntrySpec(EnemyKind.Runner, 4)),
                new GroupSpec(1.0f, 1.00f, false,
                    new EntrySpec(EnemyKind.Tank, 1)),
                new GroupSpec(0.9f, 1.00f, false,
                    new EntrySpec(EnemyKind.Tank, 1)),
                new GroupSpec(0.9f, 1.00f, false,
                    new EntrySpec(EnemyKind.Tank, 1)),
                new GroupSpec(1.2f, 0f, false,
                    new EntrySpec(EnemyKind.Armored, 1))),

            // Wave 9
            new WaveSpec(-1f,
                new GroupSpec(0f, 0.80f, true,
                    new EntrySpec(EnemyKind.Normal, 6),
                    new EntrySpec(EnemyKind.Runner, 6)),
                new GroupSpec(1.0f, 1.00f, false,
                    new EntrySpec(EnemyKind.Tank, 1)),
                new GroupSpec(0.85f, 1.00f, false,
                    new EntrySpec(EnemyKind.Tank, 1)),
                new GroupSpec(0.85f, 1.00f, false,
                    new EntrySpec(EnemyKind.Tank, 1)),
                new GroupSpec(0.85f, 1.00f, false,
                    new EntrySpec(EnemyKind.Tank, 1)),
                new GroupSpec(1.2f, 0f, false,
                    new EntrySpec(EnemyKind.Armored, 1))),

            // Wave 10 — Final (3 phases)
            new WaveSpec(-1f,
                // Phase A
                new GroupSpec(0f, 0.75f, true,
                    new EntrySpec(EnemyKind.Runner, 4),
                    new EntrySpec(EnemyKind.Normal, 4)),
                // Phase B
                new GroupSpec(1.5f, 0.85f, true,
                    new EntrySpec(EnemyKind.Tank, 1),
                    new EntrySpec(EnemyKind.Normal, 5)),
                new GroupSpec(0.9f, 1.00f, false,
                    new EntrySpec(EnemyKind.Tank, 1)),
                new GroupSpec(0.9f, 1.00f, false,
                    new EntrySpec(EnemyKind.Tank, 1)),
                // Phase C
                new GroupSpec(1.5f, 0.75f, true,
                    new EntrySpec(EnemyKind.Runner, 4),
                    new EntrySpec(EnemyKind.Tank, 1)),
                new GroupSpec(0.9f, 1.00f, false,
                    new EntrySpec(EnemyKind.Tank, 1)),
                new GroupSpec(1.0f, 0f, false,
                    new EntrySpec(EnemyKind.Armored, 1))),
        };

        [MenuItem("AlienDefense/Setup/6. Create 10 Wave Definitions")]
        public static WaveDefinition[] CreateAll()
        {
            EnemyPrefabBuilder.CreateAll();
            EditorFolderUtility.EnsureFolder(DataFolder);

            var normal = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(NormalDefinitionPath);
            var runner = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(RunnerDefinitionPath);
            var tank = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(TankDefinitionPath);
            var armored = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(ArmoredDefinitionPath);

            if (normal == null || runner == null || tank == null)
            {
                Debug.LogError("[AlienDefense Setup] Missing core EnemyDefinitions; aborting wave build.");
                return System.Array.Empty<WaveDefinition>();
            }

            ApplyArchetypeDefaults(normal, EnemyArchetype.Normal, 1f, 1f, 1f);
            ApplyArchetypeDefaults(runner, EnemyArchetype.Fast, 0.8f, 1f, 1f);
            ApplyArchetypeDefaults(tank, EnemyArchetype.Tank, 1.25f, 0.25f, 1f);
            if (armored != null)
            {
                ApplyArchetypeDefaults(armored, EnemyArchetype.Elite, 1.15f, 0.5f, 1.1f);
            }

            var shield = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Shield.asset");
            if (shield != null)
            {
                ApplyArchetypeDefaults(shield, EnemyArchetype.Elite, 1.15f, 0.75f, 1.1f);
            }

            var boss = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Boss.asset");
            if (boss != null)
            {
                ApplyArchetypeDefaults(boss, EnemyArchetype.Boss, 1.2f, 0.2f, 1.15f);
            }

            var result = new WaveDefinition[Waves.Length];
            for (int i = 0; i < Waves.Length; i++)
            {
                result[i] = CreateOrLoadWave(i + 1, Waves[i], normal, runner, tank, armored);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[AlienDefense Setup] 10 wave definitions ready (multi-archetype progression).");
            return result;
        }

        private static void ApplyArchetypeDefaults(
            EnemyDefinition definition,
            EnemyArchetype archetype,
            float healthFactor,
            float speedFactor,
            float damageFactor)
        {
            var so = new SerializedObject(definition);
            so.FindProperty("_archetype").enumValueIndex = (int)archetype;
            so.FindProperty("_healthScaleFactor").floatValue = healthFactor;
            so.FindProperty("_speedScaleFactor").floatValue = speedFactor;
            so.FindProperty("_damageScaleFactor").floatValue = damageFactor;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static WaveDefinition CreateOrLoadWave(
            int waveNumber,
            WaveSpec spec,
            EnemyDefinition normal,
            EnemyDefinition runner,
            EnemyDefinition tank,
            EnemyDefinition armored)
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
                SerializedProperty groupProperty = groupsProperty.GetArrayElementAtIndex(i);
                WriteGroup(groupProperty, groupSpec, normal, runner, tank, armored);
            }

            serializedWave.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(wave);
            return wave;
        }

        private static void WriteGroup(
            SerializedProperty groupProperty,
            GroupSpec groupSpec,
            EnemyDefinition normal,
            EnemyDefinition runner,
            EnemyDefinition tank,
            EnemyDefinition armored)
        {
            groupProperty.FindPropertyRelative("_delayBeforeGroup").floatValue = groupSpec.DelayBeforeGroup;
            groupProperty.FindPropertyRelative("_spawnInterval").floatValue = Mathf.Max(0.70f, groupSpec.SpawnInterval) > 0f
                ? Mathf.Max(0.70f, groupSpec.SpawnInterval)
                : 0f;
            // Allow 0 interval for single-enemy elite drops; otherwise clamp to >= 0.70
            if (groupSpec.SpawnInterval <= 0f)
            {
                groupProperty.FindPropertyRelative("_spawnInterval").floatValue = 0f;
            }
            else
            {
                groupProperty.FindPropertyRelative("_spawnInterval").floatValue = Mathf.Max(0.70f, groupSpec.SpawnInterval);
            }

            groupProperty.FindPropertyRelative("_interleaveEntries").boolValue = groupSpec.Interleave;

            // Clear legacy fields
            groupProperty.FindPropertyRelative("_enemyDefinition").objectReferenceValue = null;
            groupProperty.FindPropertyRelative("_count").intValue = 1;

            SerializedProperty entriesProperty = groupProperty.FindPropertyRelative("_entries");
            entriesProperty.arraySize = groupSpec.Entries.Length;
            for (int e = 0; e < groupSpec.Entries.Length; e++)
            {
                EntrySpec entrySpec = groupSpec.Entries[e];
                EnemyDefinition definition = Resolve(entrySpec.Enemy, normal, runner, tank, armored);
                SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(e);
                entryProperty.FindPropertyRelative("_enemyDefinition").objectReferenceValue = definition;
                entryProperty.FindPropertyRelative("_count").intValue = entrySpec.Count;
            }
        }

        private static EnemyDefinition Resolve(
            EnemyKind kind,
            EnemyDefinition normal,
            EnemyDefinition runner,
            EnemyDefinition tank,
            EnemyDefinition armored)
        {
            switch (kind)
            {
                case EnemyKind.Normal:
                    return normal;
                case EnemyKind.Runner:
                    return runner;
                case EnemyKind.Tank:
                    return tank;
                case EnemyKind.Armored:
                    return armored != null ? armored : tank;
                default:
                    return normal;
            }
        }
    }
}
