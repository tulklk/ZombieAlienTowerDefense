using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Base;
using AlienDefense.Economy;
using AlienDefense.Enemies;
using AlienDefense.Player;
using AlienDefense.Waves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    /// <summary>End-to-end coverage of the continuous multi-enemy Tractor Beam using real pooled EnemyController
    /// instances and real Update() loops: full Pull->Lift->Captured resolution, following a moving UFO, Wave
    /// tracking, Defeat cleanup, Pause/x2, and repeated pool reuse.</summary>
    public class TractorBeamLifecyclePlayModeTests
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

        private static IEnumerator WaitForGameSeconds(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
        }

        private EnemyPath3D CreatePath()
        {
            var pathObject = new GameObject("TestPath");
            _spawnedObjects.Add(pathObject);
            LogAssert.Expect(LogType.Error, $"[EnemyPath3D] '{pathObject.name}' needs at least two waypoints.");
            var path = pathObject.AddComponent<EnemyPath3D>();

            var waypointA = new GameObject("A");
            waypointA.transform.position = Vector3.zero;
            _spawnedObjects.Add(waypointA);
            var waypointB = new GameObject("B");
            waypointB.transform.position = new Vector3(0f, 0f, 50f);
            _spawnedObjects.Add(waypointB);

            SetPrivateField(path, "_waypoints", new[] { waypointA.transform, waypointB.transform });
            return path;
        }

        private EnemyController CreateEnemyTemplate(bool canCapture = true)
        {
            var go = new GameObject("TestEnemyTemplate");
            go.SetActive(false);
            _spawnedObjects.Add(go);

            var health = go.AddComponent<EnemyHealth>();
            var movement = go.AddComponent<EnemyMovement>();
            var captureController = go.AddComponent<EnemyCaptureController>();
            var controller = go.AddComponent<EnemyController>();
            SetPrivateField(controller, "_health", health);
            SetPrivateField(controller, "_movement", movement);
            SetPrivateField(controller, "_captureController", captureController);

            return controller;
        }

        private EnemyDefinition CreateEnemyDefinition(EnemyController template, bool canCapture = true, float resistance = 1f, int reward = 10)
        {
            var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_prefab", template);
            SetPrivateField(definition, "_canBeTractorCaptured", canCapture);
            SetPrivateField(definition, "_tractorResistance", resistance);
            SetPrivateField(definition, "_rewardResource", reward);
            SetPrivateField(definition, "_poolPrewarmCount", 0);
            SetPrivateField(definition, "_poolDefaultCapacity", 20);
            SetPrivateField(definition, "_poolMaximumSize", 20);
            return definition;
        }

        private EnemyFactory CreateEnemyFactory(out EnemyRegistry registry, out EconomyService economy)
        {
            var runtimeParent = new GameObject("EnemyRuntimeParent");
            _spawnedObjects.Add(runtimeParent);
            registry = new EnemyRegistry();
            economy = new EconomyService(0);
            var poolRegistry = new EnemyPoolRegistry(runtimeParent.transform);
            return new EnemyFactory(poolRegistry, registry, economy, new BaseHealthService(100), null);
        }

        private (UFOTractorBeamController beam, Transform anchor, Transform socket) CreateBeam(
            EnemyRegistry registry, float attractionRadius = 3.5f, int maxConcurrent = 8, float scanInterval = 0.1f)
        {
            var beamObject = new GameObject("TestBeam");
            _spawnedObjects.Add(beamObject);
            var anchorObject = new GameObject("BeamGroundAnchor");
            anchorObject.transform.SetParent(beamObject.transform, false);
            var socketObject = new GameObject("CaptureSocket");
            socketObject.transform.SetParent(beamObject.transform, false);
            socketObject.transform.localPosition = new Vector3(0f, 1.5f, 0f);

            var definition = ScriptableObject.CreateInstance<UFOTractorBeamDefinition>();
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_attractionRadius", attractionRadius);
            SetPrivateField(definition, "_scanInterval", scanInterval);
            SetPrivateField(definition, "_maxConcurrentCaptures", maxConcurrent);
            SetPrivateField(definition, "_pullSpeed", 12f);
            SetPrivateField(definition, "_liftSpeed", 10f);
            SetPrivateField(definition, "_beamCenterThreshold", 0.2f);
            SetPrivateField(definition, "_captureSocketThreshold", 0.15f);

            var beam = beamObject.AddComponent<UFOTractorBeamController>();
            SetPrivateField(beam, "_definition", definition);
            SetPrivateField(beam, "_beamGroundAnchor", anchorObject.transform);
            SetPrivateField(beam, "_captureSocket", socketObject.transform);
            beam.Initialize(registry);

            return (beam, anchorObject.transform, socketObject.transform);
        }

        [UnityTest]
        public IEnumerator MultiEnemyCapture_EndToEnd_AllResolveCapturedWithReward()
        {
            EnemyPath3D path = CreatePath();
            EnemyFactory factory = CreateEnemyFactory(out EnemyRegistry registry, out EconomyService economy);
            EnemyController template = CreateEnemyTemplate();
            EnemyDefinition definition = CreateEnemyDefinition(template, reward: 5);

            (UFOTractorBeamController beam, _, _) = CreateBeam(registry, maxConcurrent: 8);

            var enemies = new EnemyController[5];
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyController enemy = factory.Spawn(definition, path, new Vector3(0.2f * i, 0f, 0f), Quaternion.identity);
                enemy.Movement.StopMovement();
                enemies[i] = enemy;
            }

            int capturedCount = 0;
            foreach (EnemyController enemy in enemies)
            {
                enemy.Resolved += (_, reason) =>
                {
                    if (reason == EnemyResolveReason.Captured)
                    {
                        capturedCount++;
                    }
                };
            }

            // Generous window for scan admission + full Pull + Lift for all 5 enemies.
            yield return WaitForGameSeconds(3f);

            Assert.AreEqual(5, capturedCount, "Every enemy inside the beam must eventually resolve as Captured.");
            Assert.AreEqual(0, registry.Count);
            Assert.AreEqual(0, beam.ActiveCaptureCount);
            Assert.AreEqual(25, economy.CurrentResource, "Reward must be granted exactly once per captured enemy.");
        }

        [UnityTest]
        public IEnumerator MovingUFO_EnemyKeepsFollowingAnchor_AndStillGetsCaptured()
        {
            EnemyPath3D path = CreatePath();
            EnemyFactory factory = CreateEnemyFactory(out EnemyRegistry registry, out _);
            EnemyController template = CreateEnemyTemplate();
            EnemyDefinition definition = CreateEnemyDefinition(template);

            (UFOTractorBeamController beam, Transform anchor, _) = CreateBeam(registry, maxConcurrent: 8);

            EnemyController enemy = factory.Spawn(definition, path, new Vector3(1f, 0f, 0f), Quaternion.identity);
            enemy.Movement.StopMovement();

            bool captured = false;
            enemy.Resolved += (_, reason) => captured = reason == EnemyResolveReason.Captured;

            yield return WaitForGameSeconds(0.3f);
            Assert.IsFalse(enemy.IsTargetable, "Enemy should have been admitted into capture already.");

            // Move the "UFO" (the anchor) away repeatedly while the enemy is mid-capture.
            for (int i = 0; i < 10; i++)
            {
                anchor.position += new Vector3(0.3f, 0f, 0.2f);
                yield return WaitForGameSeconds(0.1f);
            }

            yield return WaitForGameSeconds(2f);

            Assert.IsTrue(captured, "A committed capture must complete even while the UFO keeps moving.");
        }

        [UnityTest]
        public IEnumerator WaveTracking_DoesNotCompleteUntilCapturedEnemiesResolve()
        {
            EnemyPath3D path = CreatePath();
            EnemyFactory factory = CreateEnemyFactory(out EnemyRegistry registry, out _);
            EnemyController template = CreateEnemyTemplate();
            EnemyDefinition definition = CreateEnemyDefinition(template);

            var waveGo = new GameObject("TestWaveController", typeof(WaveController));
            _spawnedObjects.Add(waveGo);
            var waveController = waveGo.GetComponent<WaveController>();
            SetPrivateField(waveController, "_path", path);
            SetPrivateField(waveController, "_autoStartFirstWave", true);
            SetPrivateField(waveController, "_autoStartNextWaves", true);

            var wave = ScriptableObject.CreateInstance<WaveDefinition>();
            _scriptableObjects.Add(wave);
            SetPrivateField(wave, "_id", "wave_capture_test");
            SetPrivateField(wave, "_displayName", "Capture Test Wave");
            SetPrivateField(wave, "_preparationDurationOverride", 0.02f);
            var group = new EnemySpawnGroup();
            SetPrivateField(group, "_enemyDefinition", definition);
            SetPrivateField(group, "_count", 3);
            SetPrivateField(group, "_delayBeforeGroup", 0f);
            SetPrivateField(group, "_spawnInterval", 0.02f);
            SetPrivateField(wave, "_spawnGroups", new[] { group });

            int completedCount = 0;
            waveController.WaveCompleted += _ => completedCount++;

            (UFOTractorBeamController beam, _, _) = CreateBeam(registry, maxConcurrent: 8);

            waveController.Initialize(factory, new[] { wave }, 0.02f);

            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            Assert.AreEqual(3, registry.Count, "All 3 enemies must have spawned and still be active (mid-capture).");
            Assert.AreEqual(0, completedCount, "Wave must not complete while captured enemies are still Pulling/Lifting.");

            yield return WaitForGameSeconds(3f);

            Assert.AreEqual(0, registry.Count);
            Assert.AreEqual(1, completedCount, "Wave must complete once every enemy — captured or otherwise — has resolved.");
        }

        [UnityTest]
        public IEnumerator Defeat_MidCapture_ForceResolveCleansUp_NoRewardNoDoubleRelease()
        {
            EnemyPath3D path = CreatePath();
            EnemyFactory factory = CreateEnemyFactory(out EnemyRegistry registry, out EconomyService economy);
            EnemyController template = CreateEnemyTemplate();
            EnemyDefinition definition = CreateEnemyDefinition(template, reward: 50);

            (UFOTractorBeamController beam, _, _) = CreateBeam(registry, maxConcurrent: 8);

            var enemies = new EnemyController[4];
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyController enemy = factory.Spawn(definition, path, new Vector3(0.2f * i, 0f, 0f), Quaternion.identity);
                enemy.Movement.StopMovement();
                enemies[i] = enemy;
            }

            yield return WaitForGameSeconds(0.3f);
            foreach (EnemyController enemy in enemies)
            {
                Assert.IsFalse(enemy.IsTargetable, "All 4 enemies should have started capture (well under Max=8).");
            }

            // Mirror LevelCompositionRoot.DespawnAllEnemies on Victory/Defeat.
            Assert.DoesNotThrow(() =>
            {
                while (registry.Count > 0)
                {
                    registry.GetAt(0).ForceResolve(EnemyResolveReason.LevelEnded);
                }
            });

            Assert.AreEqual(0, registry.Count);
            Assert.AreEqual(0, economy.CurrentResource, "LevelEnded must never grant a reward, even for enemies that were mid-capture.");
            Assert.AreEqual(0, beam.ActiveCaptureCount, "Beam's tracking must self-clean via the Resolved event.");

            // A further frame must not throw (capture controllers should be Aborted, not still ticking a released object).
            yield return WaitForGameSeconds(0.5f);
        }

        [UnityTest]
        public IEnumerator Pause_FreezesCapture_ResumeContinuesWithoutDuplicateCapture()
        {
            EnemyPath3D path = CreatePath();
            EnemyFactory factory = CreateEnemyFactory(out EnemyRegistry registry, out _);
            EnemyController template = CreateEnemyTemplate();
            EnemyDefinition definition = CreateEnemyDefinition(template);

            (UFOTractorBeamController beam, _, _) = CreateBeam(registry, maxConcurrent: 8);

            EnemyController enemy = factory.Spawn(definition, path, new Vector3(1f, 0f, 0f), Quaternion.identity);
            enemy.Movement.StopMovement();

            yield return WaitForGameSeconds(0.2f);
            Assert.IsFalse(enemy.IsTargetable);
            Assert.AreEqual(1, beam.ActiveCaptureCount);

            yield return null;
            Time.timeScale = 0f;
            Vector3 positionAtPause = enemy.transform.position;

            for (int i = 0; i < 15; i++)
            {
                yield return null;
            }

            Assert.AreEqual(positionAtPause, enemy.transform.position, "Capture movement must fully freeze while paused.");
            Assert.AreEqual(1, beam.ActiveCaptureCount, "Pausing must not lose or duplicate capture tracking.");

            Time.timeScale = 1f;
            yield return WaitForGameSeconds(2f);

            Assert.AreEqual(0, registry.Count, "Resuming must let the capture continue to completion normally.");
            Assert.AreEqual(0, beam.ActiveCaptureCount);
        }

        [UnityTest]
        public IEnumerator PoolReuse_RepeatedCaptureCycles_NeverLeaksOrDoubleRewards()
        {
            EnemyPath3D path = CreatePath();
            EnemyFactory factory = CreateEnemyFactory(out EnemyRegistry registry, out EconomyService economy);
            EnemyController template = CreateEnemyTemplate();
            EnemyDefinition definition = CreateEnemyDefinition(template, reward: 3);

            (UFOTractorBeamController beam, _, _) = CreateBeam(registry, maxConcurrent: 8, scanInterval: 0.05f);

            const int cycles = 10;
            for (int cycle = 0; cycle < cycles; cycle++)
            {
                EnemyController enemy = factory.Spawn(definition, path, Vector3.zero, Quaternion.identity);
                enemy.Movement.StopMovement();

                bool resolvedAsCaptured = false;
                enemy.Resolved += (_, reason) => resolvedAsCaptured = reason == EnemyResolveReason.Captured;

                yield return WaitForGameSeconds(1.5f);

                Assert.IsTrue(resolvedAsCaptured, $"Cycle {cycle}: enemy must resolve as Captured.");
                Assert.IsTrue(enemy.IsTargetable, $"Cycle {cycle}: capture flags must be reset (this is the same reused instance).");
                Assert.IsTrue(enemy.Health.IsDamageable, $"Cycle {cycle}: damage-immunity must be reset.");
                Assert.AreEqual(0, registry.Count, $"Cycle {cycle}: registry must be empty between cycles.");
                Assert.AreEqual(0, beam.ActiveCaptureCount, $"Cycle {cycle}: beam tracking must be empty between cycles.");
            }

            Assert.AreEqual(3 * cycles, economy.CurrentResource, "Exactly one reward per cycle, no more.");
        }
    }
}
