using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Base;
using AlienDefense.Economy;
using AlienDefense.Enemies;
using AlienDefense.Waves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    public class WaveControllerPlayModeTests
    {
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;

            foreach (GameObject go in _spawnedObjects)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }

            _spawnedObjects.Clear();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private EnemyPath3D CreatePath(params Vector3[] points)
        {
            var pathObject = new GameObject("TestEnemyPath");
            _spawnedObjects.Add(pathObject);
            LogAssert.Expect(LogType.Error, $"[EnemyPath3D] '{pathObject.name}' needs at least two waypoints.");
            var path = pathObject.AddComponent<EnemyPath3D>();

            var waypointTransforms = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var waypoint = new GameObject($"Waypoint_{i}");
                waypoint.transform.position = points[i];
                _spawnedObjects.Add(waypoint);
                waypointTransforms[i] = waypoint.transform;
            }

            SetPrivateField(path, "_waypoints", waypointTransforms);
            return path;
        }

        private GameObject CreateEnemyTemplate()
        {
            var go = new GameObject("TestEnemyTemplate");
            go.SetActive(false);
            _spawnedObjects.Add(go);

            var health = go.AddComponent<EnemyHealth>();
            var movement = go.AddComponent<EnemyMovement>();
            var controller = go.AddComponent<EnemyController>();
            SetPrivateField(controller, "_health", health);
            SetPrivateField(controller, "_movement", movement);

            return go;
        }

        private EnemyDefinition CreateEnemyDefinition(EnemyController template, int prewarm, int defaultCapacity, int maxSize)
        {
            var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            SetPrivateField(definition, "_prefab", template);
            SetPrivateField(definition, "_poolPrewarmCount", prewarm);
            SetPrivateField(definition, "_poolDefaultCapacity", defaultCapacity);
            SetPrivateField(definition, "_poolMaximumSize", maxSize);
            return definition;
        }

        private EnemyFactory CreateFactory(out EnemyRegistry registry)
        {
            var runtimeParent = new GameObject("RuntimeParent");
            _spawnedObjects.Add(runtimeParent);

            registry = new EnemyRegistry();
            var economy = new EconomyService(0);
            var baseHealth = new BaseHealthService(100);
            var poolRegistry = new EnemyPoolRegistry(runtimeParent.transform);
            return new EnemyFactory(poolRegistry, registry, economy, baseHealth, null);
        }

        private WaveDefinition CreateWaveDefinition(string id, float preparationOverride, params (EnemyDefinition definition, int count, float delay, float interval)[] groups)
        {
            var wave = ScriptableObject.CreateInstance<WaveDefinition>();
            SetPrivateField(wave, "_id", id);
            SetPrivateField(wave, "_displayName", id);
            SetPrivateField(wave, "_preparationDurationOverride", preparationOverride);

            var groupArray = new EnemySpawnGroup[groups.Length];
            for (int i = 0; i < groups.Length; i++)
            {
                var group = new EnemySpawnGroup();
                SetPrivateField(group, "_enemyDefinition", groups[i].definition);
                SetPrivateField(group, "_count", groups[i].count);
                SetPrivateField(group, "_delayBeforeGroup", groups[i].delay);
                SetPrivateField(group, "_spawnInterval", groups[i].interval);
                groupArray[i] = group;
            }

            SetPrivateField(wave, "_spawnGroups", groupArray);
            return wave;
        }

        private WaveController CreateWaveController(EnemyPath3D path)
        {
            var go = new GameObject("TestWaveController", typeof(WaveController));
            _spawnedObjects.Add(go);
            var controller = go.GetComponent<WaveController>();
            SetPrivateField(controller, "_path", path);
            SetPrivateField(controller, "_autoStartFirstWave", true);
            SetPrivateField(controller, "_autoStartNextWaves", true);
            return controller;
        }

        [UnityTest]
        public IEnumerator WaveController_SpawnsPlannedCount_AndCompletesOnlyAfterAllResolve()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            EnemyFactory factory = CreateFactory(out EnemyRegistry registry);
            GameObject template = CreateEnemyTemplate();
            EnemyDefinition definition = CreateEnemyDefinition(template.GetComponent<EnemyController>(), 0, 5, 5);
            WaveDefinition wave = CreateWaveDefinition("wave_test", 0.05f, (definition, 3, 0f, 0.02f));
            WaveController waveController = CreateWaveController(path);

            int completedCount = 0;
            int allCompletedCount = 0;
            waveController.WaveCompleted += _ => completedCount++;
            waveController.AllWavesCompleted += () => allCompletedCount++;

            waveController.Initialize(factory, new[] { wave }, 0.05f);

            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }

            Assert.AreEqual(3, registry.Count);
            Assert.AreEqual(WaveState.WaitingForRemainingEnemies, waveController.CurrentState);
            Assert.AreEqual(0, completedCount);

            while (registry.Count > 0)
            {
                registry.GetAt(0).ApplyDebugDamage(999999f);
                yield return null;
            }

            yield return null;

            Assert.AreEqual(WaveState.Completed, waveController.CurrentState);
            Assert.AreEqual(1, completedCount);
            Assert.AreEqual(1, allCompletedCount);
        }

        [UnityTest]
        public IEnumerator WaveController_StopWaves_PreventsFurtherCompletionEvents()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            EnemyFactory factory = CreateFactory(out EnemyRegistry registry);
            GameObject template = CreateEnemyTemplate();
            EnemyDefinition definition = CreateEnemyDefinition(template.GetComponent<EnemyController>(), 0, 5, 5);
            WaveDefinition wave = CreateWaveDefinition("wave_test", 0.05f, (definition, 2, 0f, 0.02f));
            WaveController waveController = CreateWaveController(path);

            int completedCount = 0;
            waveController.WaveCompleted += _ => completedCount++;

            waveController.Initialize(factory, new[] { wave }, 0.05f);

            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }

            Assert.AreEqual(2, registry.Count);

            waveController.StopWaves();

            while (registry.Count > 0)
            {
                registry.GetAt(0).ForceResolve(EnemyResolveReason.Removed);
                yield return null;
            }

            yield return null;

            Assert.AreEqual(WaveState.Stopped, waveController.CurrentState);
            Assert.AreEqual(0, completedCount);
        }

        [UnityTest]
        public IEnumerator WaveController_SpawnFailure_RecordsFailure_AndDoesNotHangWave()
        {
            var brokenDefinition = ScriptableObject.CreateInstance<EnemyDefinition>();
            brokenDefinition.name = "BrokenDefinition";

            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 10f));
            EnemyFactory factory = CreateFactory(out EnemyRegistry registry);
            WaveDefinition wave = CreateWaveDefinition("wave_fail", 0.02f, (brokenDefinition, 2, 0f, 0.02f));
            WaveController waveController = CreateWaveController(path);

            int completedCount = 0;
            waveController.WaveCompleted += _ => completedCount++;

            LogAssert.Expect(LogType.Error, "[EnemyPoolRegistry] EnemyDefinition 'BrokenDefinition' has no Prefab.");
            LogAssert.Expect(LogType.Error, "[EnemyPoolRegistry] EnemyDefinition 'BrokenDefinition' has no Prefab.");

            waveController.Initialize(factory, new[] { wave }, 0.02f);

            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }

            Assert.AreEqual(WaveState.Completed, waveController.CurrentState);
            Assert.AreEqual(1, completedCount);
            Assert.AreEqual(0, registry.Count);
        }

        [UnityTest]
        public IEnumerator WaveController_DoesNotSpawn_WhileTimeScaleIsZero()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 10f));
            EnemyFactory factory = CreateFactory(out EnemyRegistry registry);
            GameObject template = CreateEnemyTemplate();
            EnemyDefinition definition = CreateEnemyDefinition(template.GetComponent<EnemyController>(), 0, 5, 5);
            WaveDefinition wave = CreateWaveDefinition("wave_pause", 0.05f, (definition, 3, 0f, 0.02f));
            WaveController waveController = CreateWaveController(path);

            waveController.Initialize(factory, new[] { wave }, 0.05f);

            yield return null;
            Time.timeScale = 0f;

            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }

            Assert.AreEqual(0, registry.Count);

            Time.timeScale = 1f;
        }
    }
}
