using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Economy;
using AlienDefense.Enemies;
using AlienDefense.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>The core rule of this refactor: UFO capture (Captured) only removes the Enemy from the
    /// battlefield — it must never touch EconomyService/EnergyWallet/PlayerLevelProgression, and must never
    /// spawn an EnergyPickup. A Tower/Combat kill (Defeated) is the regression check: that path is intentionally
    /// UNCHANGED by this refactor (still grants EconomyService reward directly; see EnemyController.Resolve).
    ///
    /// EditMode tests never run MonoBehaviour.Update automatically, so each captured enemy's
    /// EnemyCaptureController.Tick must be driven by hand alongside the beam's own Tick — see DriveOneFrame.</summary>
    public class EnemyCaptureNoRewardTests
    {
        private const int StartingResource = 100;
        private const int RewardResource = 10;

        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();
        private readonly List<Object> _scriptableObjects = new List<Object>();
        private readonly List<EnemyCaptureController> _captureControllers = new List<EnemyCaptureController>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawnedObjects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _spawnedObjects.Clear();
            _captureControllers.Clear();

            foreach (Object asset in _scriptableObjects)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
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

        private static T CreateSilently<T>(System.Func<T> factory)
        {
            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            T result = factory();
            Debug.unityLogger.logEnabled = previousLogEnabled;
            return result;
        }

        private EnemyPath3D CreatePath()
        {
            var pathObject = new GameObject("TestPath");
            _spawnedObjects.Add(pathObject);
            var waypointA = new GameObject("A");
            waypointA.transform.SetParent(pathObject.transform);
            var waypointB = new GameObject("B");
            waypointB.transform.position = new Vector3(0f, 0f, 50f);
            waypointB.transform.SetParent(pathObject.transform);
            var path = CreateSilently(() => pathObject.AddComponent<EnemyPath3D>());
            var pathSerialized = new SerializedObject(path);
            SerializedProperty waypointsProperty = pathSerialized.FindProperty("_waypoints");
            waypointsProperty.arraySize = 2;
            waypointsProperty.GetArrayElementAtIndex(0).objectReferenceValue = waypointA.transform;
            waypointsProperty.GetArrayElementAtIndex(1).objectReferenceValue = waypointB.transform;
            pathSerialized.ApplyModifiedPropertiesWithoutUndo();
            return path;
        }

        private EnemyController CreateEnemy(EnemyRegistry registry, EnemyPath3D path, EconomyService economy)
        {
            var go = new GameObject("TestEnemy");
            _spawnedObjects.Add(go);
            var health = go.AddComponent<EnemyHealth>();
            var movement = go.AddComponent<EnemyMovement>();
            var captureController = go.AddComponent<EnemyCaptureController>();
            _captureControllers.Add(captureController);
            var controller = go.AddComponent<EnemyController>();
            SetPrivateField(controller, "_health", health);
            SetPrivateField(controller, "_movement", movement);
            SetPrivateField(controller, "_captureController", captureController);

            var definition = CreateSilently(() => ScriptableObject.CreateInstance<EnemyDefinition>());
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_canBeTractorCaptured", true);
            SetPrivateField(definition, "_tractorResistance", 1f);
            SetPrivateField(definition, "_rewardResource", RewardResource);
            SetPrivateField(definition, "_maxHealth", 10f);

            controller.Initialize(definition, path, economy, null, registry, _ => { }, null);
            controller.Movement.StopMovement();
            controller.transform.position = Vector3.zero;
            return controller;
        }

        private (UFOTractorBeamController beam, Transform anchor) CreateBeam(EnemyRegistry registry)
        {
            var beamObject = new GameObject("TestBeam");
            _spawnedObjects.Add(beamObject);
            var anchorObject = new GameObject("BeamGroundAnchor");
            anchorObject.transform.SetParent(beamObject.transform, false);
            var socketObject = new GameObject("CaptureSocket");
            socketObject.transform.SetParent(beamObject.transform, false);

            var definition = CreateSilently(() => ScriptableObject.CreateInstance<UFOTractorBeamDefinition>());
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_attractionRadius", 3.5f);
            SetPrivateField(definition, "_scanInterval", 0.1f);
            SetPrivateField(definition, "_maxConcurrentCaptures", 8);
            SetPrivateField(definition, "_pullSpeed", 8f);
            SetPrivateField(definition, "_liftSpeed", 6f);
            SetPrivateField(definition, "_beamCenterThreshold", 0.2f);
            SetPrivateField(definition, "_captureSocketThreshold", 0.15f);

            var beam = beamObject.AddComponent<UFOTractorBeamController>();
            SetPrivateField(beam, "_definition", definition);
            SetPrivateField(beam, "_beamGroundAnchor", anchorObject.transform);
            SetPrivateField(beam, "_captureSocket", socketObject.transform);
            beam.Initialize(registry);

            return (beam, anchorObject.transform);
        }

        /// <summary>EditMode tests never run MonoBehaviour.Update automatically: the beam's own Tick only scans
        /// for NEW admissions, while each already-capturing enemy's Pull/Lift progress lives on its own
        /// EnemyCaptureController and must be advanced by hand — this mirrors what Update() would do every frame.</summary>
        private void DriveOneFrame(UFOTractorBeamController beam, float deltaTime)
        {
            beam.Tick(deltaTime);
            foreach (EnemyCaptureController capture in _captureControllers)
            {
                if (capture != null)
                {
                    capture.Tick(deltaTime);
                }
            }
        }

        [Test]
        public void Capture_NeverAddsToEconomy_EvenAfterFullPullLiftCompletes()
        {
            var economy = new EconomyService(StartingResource);
            var registry = new EnemyRegistry();
            EnemyPath3D path = CreatePath();
            (UFOTractorBeamController beam, _) = CreateBeam(registry);
            EnemyController enemy = CreateEnemy(registry, path, economy);

            bool resolved = false;
            EnemyResolveReason resolvedReason = default;
            enemy.Resolved += (_, reason) => { resolved = true; resolvedReason = reason; };

            // Anchor and socket sit at the same local position in this test rig, so a handful of large-deltaTime
            // frames is enough to cross both the Pull and Lift thresholds.
            for (int i = 0; i < 20 && !resolved; i++)
            {
                DriveOneFrame(beam, 0.5f);
            }

            Assert.IsTrue(resolved, "Capture never completed within the frame budget — test rig assumption is wrong.");
            Assert.AreEqual(EnemyResolveReason.Captured, resolvedReason);
            Assert.AreEqual(StartingResource, economy.CurrentResource, "UFO capture must never change EconomyService's balance.");
        }

        [Test]
        public void Defeated_StillAddsRewardResourceToEconomy_UnchangedByThisRefactor()
        {
            var economy = new EconomyService(StartingResource);
            var registry = new EnemyRegistry();
            EnemyPath3D path = CreatePath();
            EnemyController enemy = CreateEnemy(registry, path, economy);

            enemy.ApplyDebugDamage(9999f);

            Assert.AreEqual(StartingResource + RewardResource, economy.CurrentResource,
                "A Tower/Combat kill (Defeated) must still grant EconomyService reward exactly as before this refactor.");
        }

        [Test]
        public void TenCapturedEnemies_NeverChangeEconomy()
        {
            var economy = new EconomyService(StartingResource);
            var registry = new EnemyRegistry();
            EnemyPath3D path = CreatePath();
            (UFOTractorBeamController beam, _) = CreateBeam(registry);

            int resolvedCount = 0;
            for (int i = 0; i < 10; i++)
            {
                EnemyController enemy = CreateEnemy(registry, path, economy);
                enemy.Resolved += (_, reason) =>
                {
                    if (reason == EnemyResolveReason.Captured)
                    {
                        resolvedCount++;
                    }
                };
            }

            for (int i = 0; i < 50 && resolvedCount < 10; i++)
            {
                DriveOneFrame(beam, 0.5f);
            }

            Assert.AreEqual(10, resolvedCount, "All 10 enemies should have been captured within the frame budget (8-slot limit refills as each completes).");
            Assert.AreEqual(StartingResource, economy.CurrentResource, "Capturing 10 enemies must leave EconomyService completely untouched.");
        }
    }
}
