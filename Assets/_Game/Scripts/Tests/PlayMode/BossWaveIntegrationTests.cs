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
    public class BossWaveIntegrationTests
    {
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();
        private readonly List<Object> _scriptableObjects = new List<Object>();

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

            foreach (Object asset in _scriptableObjects)
            {
                if (asset != null)
                {
                    Object.Destroy(asset);
                }
            }

            _scriptableObjects.Clear();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        /// <summary>Waits real elapsed game seconds (accumulating Time.deltaTime) rather than a fixed frame count,
        /// so the test stays correct regardless of the Editor's actual frame rate.</summary>
        private static IEnumerator WaitForGameSeconds(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
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

        private EnemyController CreateMinionTemplate()
        {
            var go = new GameObject("TestMinionTemplate");
            go.SetActive(false);
            _spawnedObjects.Add(go);

            var health = go.AddComponent<EnemyHealth>();
            var movement = go.AddComponent<EnemyMovement>();
            var controller = go.AddComponent<EnemyController>();
            SetPrivateField(controller, "_health", health);
            SetPrivateField(controller, "_movement", movement);

            return controller;
        }

        private EnemyController CreateBossTemplate()
        {
            var go = new GameObject("TestBossTemplate");
            go.SetActive(false);
            _spawnedObjects.Add(go);

            var health = go.AddComponent<EnemyHealth>();
            var movement = go.AddComponent<EnemyMovement>();
            var defense = go.AddComponent<EnemyDefense>();
            var controller = go.AddComponent<EnemyController>();
            var bossController = go.AddComponent<BossController>();

            SetPrivateField(controller, "_health", health);
            SetPrivateField(controller, "_movement", movement);
            SetPrivateField(controller, "_defense", defense);
            SetPrivateField(controller, "_bossController", bossController);

            SetPrivateField(bossController, "_enemyController", controller);
            SetPrivateField(bossController, "_health", health);
            SetPrivateField(bossController, "_movement", movement);
            SetPrivateField(bossController, "_defense", defense);

            return controller;
        }

        private EnemyDefinition CreateEnemyDefinition(EnemyController template, int prewarm, int defaultCapacity, int maxSize)
        {
            var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_prefab", template);
            SetPrivateField(definition, "_poolPrewarmCount", prewarm);
            SetPrivateField(definition, "_poolDefaultCapacity", defaultCapacity);
            SetPrivateField(definition, "_poolMaximumSize", maxSize);
            return definition;
        }

        private BossBehaviorDefinition CreateBehavior(EnemyDefinition minionDefinition, float minionInterval, int minionCount)
        {
            var behavior = ScriptableObject.CreateInstance<BossBehaviorDefinition>();
            _scriptableObjects.Add(behavior);
            SetPrivateField(behavior, "_phaseTwoHealthThreshold", 0.5f);
            SetPrivateField(behavior, "_minionDefinition", minionDefinition);
            SetPrivateField(behavior, "_minionCountPerBurst", minionCount);
            SetPrivateField(behavior, "_minionSpawnInterval", minionInterval);
            SetPrivateField(behavior, "_phaseTwoMinionIntervalMultiplier", 0.6f);
            SetPrivateField(behavior, "_phaseTwoSpeedMultiplier", 1.4f);
            SetPrivateField(behavior, "_phaseOneDamageReductionPercent", 0.1f);
            SetPrivateField(behavior, "_phaseTwoDamageReductionPercent", 0.3f);
            return behavior;
        }

        private WaveDefinition CreateWaveDefinition(string id, float preparationOverride, params (EnemyDefinition definition, int count, float delay, float interval)[] groups)
        {
            var wave = ScriptableObject.CreateInstance<WaveDefinition>();
            _scriptableObjects.Add(wave);
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
        public IEnumerator BossSpawn_FiresBossSpawnedEvent_AndWiresSpawnCoordinator()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            var runtimeParent = new GameObject("RuntimeParent");
            _spawnedObjects.Add(runtimeParent);
            var registry = new EnemyRegistry();
            var factory = new EnemyFactory(new EnemyPoolRegistry(runtimeParent.transform), registry, new EconomyService(0), new BaseHealthService(100), null);

            EnemyController minionTemplate = CreateMinionTemplate();
            EnemyDefinition minionDefinition = CreateEnemyDefinition(minionTemplate, 0, 5, 5);

            EnemyController bossTemplate = CreateBossTemplate();
            BossBehaviorDefinition behavior = CreateBehavior(minionDefinition, minionInterval: 1000f, minionCount: 1);
            SetPrivateField(bossTemplate.GetComponent<BossController>(), "_behavior", behavior);
            EnemyDefinition bossDefinition = CreateEnemyDefinition(bossTemplate, 0, 2, 2);

            WaveDefinition wave = CreateWaveDefinition("wave_boss", 0.02f, (bossDefinition, 1, 0f, 0f));
            WaveController waveController = CreateWaveController(path);

            int bossSpawnedCount = 0;
            waveController.BossSpawned += (enemy, bossController) =>
            {
                bossSpawnedCount++;
                bossController.Initialize(waveController);
            };

            waveController.Initialize(factory, new[] { wave }, 0.02f);

            yield return WaitForGameSeconds(0.3f);

            Assert.AreEqual(1, bossSpawnedCount);
            Assert.AreEqual(1, registry.Count);
            Assert.AreEqual(BossState.PhaseOne, registry.GetAt(0).BossController.State);
        }

        [UnityTest]
        public IEnumerator BossMinions_CountTowardActiveWave_WaveDoesNotCompleteWhileMinionsAlive()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            var runtimeParent = new GameObject("RuntimeParent");
            _spawnedObjects.Add(runtimeParent);
            var registry = new EnemyRegistry();
            var factory = new EnemyFactory(new EnemyPoolRegistry(runtimeParent.transform), registry, new EconomyService(0), new BaseHealthService(100), null);

            EnemyController minionTemplate = CreateMinionTemplate();
            EnemyDefinition minionDefinition = CreateEnemyDefinition(minionTemplate, 0, 5, 5);

            EnemyController bossTemplate = CreateBossTemplate();
            // Very short minion interval so a burst fires almost immediately once the boss is initialized.
            BossBehaviorDefinition behavior = CreateBehavior(minionDefinition, minionInterval: 0.05f, minionCount: 1);
            SetPrivateField(bossTemplate.GetComponent<BossController>(), "_behavior", behavior);
            EnemyDefinition bossDefinition = CreateEnemyDefinition(bossTemplate, 0, 2, 2);

            WaveDefinition wave = CreateWaveDefinition("wave_boss", 0.02f, (bossDefinition, 1, 0f, 0f));
            WaveController waveController = CreateWaveController(path);

            waveController.BossSpawned += (enemy, bossController) => bossController.Initialize(waveController);

            int completedCount = 0;
            waveController.WaveCompleted += _ => completedCount++;

            waveController.Initialize(factory, new[] { wave }, 0.02f);

            // Boss spawns, then its first minion burst fires; wait long enough for both.
            yield return WaitForGameSeconds(0.5f);

            Assert.AreEqual(2, registry.Count, "Boss + at least one minion should be active.");
            Assert.AreEqual(WaveState.WaitingForRemainingEnemies, waveController.CurrentState);
            Assert.AreEqual(0, completedCount);

            while (registry.Count > 0)
            {
                registry.GetAt(0).ApplyDebugDamage(999999f);
                yield return null;
            }

            yield return null;

            Assert.AreEqual(WaveState.Completed, waveController.CurrentState);
            Assert.AreEqual(1, completedCount, "Wave must only complete once the boss and every minion it spawned have resolved.");
        }

        [UnityTest]
        public IEnumerator Boss_ResolvesExactlyOnce_AndTransitionsToDefeatedState()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            var runtimeParent = new GameObject("RuntimeParent");
            _spawnedObjects.Add(runtimeParent);
            var registry = new EnemyRegistry();
            var economy = new EconomyService(0);
            var factory = new EnemyFactory(new EnemyPoolRegistry(runtimeParent.transform), registry, economy, new BaseHealthService(100), null);

            EnemyController minionTemplate = CreateMinionTemplate();
            EnemyDefinition minionDefinition = CreateEnemyDefinition(minionTemplate, 0, 5, 5);

            EnemyController bossTemplate = CreateBossTemplate();
            BossBehaviorDefinition behavior = CreateBehavior(minionDefinition, minionInterval: 1000f, minionCount: 1);
            SetPrivateField(bossTemplate.GetComponent<BossController>(), "_behavior", behavior);
            EnemyDefinition bossDefinition = CreateEnemyDefinition(bossTemplate, 0, 2, 2);

            WaveDefinition wave = CreateWaveDefinition("wave_boss", 0.02f, (bossDefinition, 1, 0f, 0f));
            WaveController waveController = CreateWaveController(path);
            waveController.BossSpawned += (enemy, bossController) => bossController.Initialize(waveController);

            waveController.Initialize(factory, new[] { wave }, 0.02f);
            yield return WaitForGameSeconds(0.2f);

            Assert.AreEqual(1, registry.Count);
            EnemyController boss = registry.GetAt(0);
            BossController bossController = boss.BossController;
            int resolvedCount = 0;
            boss.Resolved += (_, __) => resolvedCount++;

            boss.ApplyDebugDamage(999999f);
            yield return null;
            boss.ApplyDebugDamage(999999f);
            yield return null;

            Assert.AreEqual(1, resolvedCount, "Resolve must be idempotent even under repeated overkill damage.");
            Assert.AreEqual(bossDefinition.RewardResource, economy.CurrentResource);
            Assert.AreEqual(BossState.Defeated, bossController.State);
        }
    }
}
