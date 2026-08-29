using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Enemies;
using AlienDefense.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>Covers UFOTractorBeamController's continuous area-admission scan: multiple concurrent captures,
    /// MaxConcurrentCaptures slot limiting/refill, and Boss immunity — never a single locked CurrentTarget.</summary>
    public class TractorCaptureAdmissionTests
    {
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();
        private readonly List<Object> _scriptableObjects = new List<Object>();

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

        private EnemyController CreateEnemy(EnemyRegistry registry, EnemyPath3D path, Vector3 position, bool canCapture, float resistance = 1f)
        {
            var go = new GameObject("TestEnemy");
            _spawnedObjects.Add(go);
            var health = go.AddComponent<EnemyHealth>();
            var movement = go.AddComponent<EnemyMovement>();
            var captureController = go.AddComponent<EnemyCaptureController>();
            var controller = go.AddComponent<EnemyController>();
            SetPrivateField(controller, "_health", health);
            SetPrivateField(controller, "_movement", movement);
            SetPrivateField(controller, "_captureController", captureController);

            var definition = CreateSilently(() => ScriptableObject.CreateInstance<EnemyDefinition>());
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_canBeTractorCaptured", canCapture);
            SetPrivateField(definition, "_tractorResistance", resistance);

            controller.Initialize(definition, path, null, null, registry, _ => { }, null);
            controller.Movement.StopMovement();
            controller.transform.position = position;
            return controller;
        }

        private (UFOTractorBeamController beam, Transform anchor) CreateBeam(EnemyRegistry registry, float attractionRadius, int maxConcurrent, float scanInterval = 0.1f)
        {
            var beamObject = new GameObject("TestBeam");
            _spawnedObjects.Add(beamObject);
            var anchorObject = new GameObject("BeamGroundAnchor");
            anchorObject.transform.SetParent(beamObject.transform, false);
            var socketObject = new GameObject("CaptureSocket");
            socketObject.transform.SetParent(beamObject.transform, false);

            var definition = CreateSilently(() => ScriptableObject.CreateInstance<UFOTractorBeamDefinition>());
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_attractionRadius", attractionRadius);
            SetPrivateField(definition, "_scanInterval", scanInterval);
            SetPrivateField(definition, "_maxConcurrentCaptures", maxConcurrent);
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

        [Test]
        public void Scan_AdmitsUpToMaxConcurrentCaptures_LeavesRestUncaptured()
        {
            var registry = new EnemyRegistry();
            EnemyPath3D path = CreatePath();
            (UFOTractorBeamController beam, _) = CreateBeam(registry, attractionRadius: 3.5f, maxConcurrent: 8);

            var enemies = new EnemyController[10];
            for (int i = 0; i < 10; i++)
            {
                enemies[i] = CreateEnemy(registry, path, new Vector3(0.1f * i, 0f, 0f), canCapture: true);
            }

            beam.Tick(0.2f);

            Assert.AreEqual(8, beam.ActiveCaptureCount);

            int capturedCount = 0;
            foreach (EnemyController enemy in enemies)
            {
                if (!enemy.IsTargetable)
                {
                    capturedCount++;
                }
            }

            Assert.AreEqual(8, capturedCount, "Exactly MaxConcurrentCaptures enemies should have started capture simultaneously.");
        }

        [Test]
        public void Scan_SlotFreedByResolve_IsRefilledOnNextScan()
        {
            var registry = new EnemyRegistry();
            EnemyPath3D path = CreatePath();
            (UFOTractorBeamController beam, _) = CreateBeam(registry, attractionRadius: 3.5f, maxConcurrent: 2);

            EnemyController a = CreateEnemy(registry, path, new Vector3(0f, 0f, 0f), canCapture: true);
            EnemyController b = CreateEnemy(registry, path, new Vector3(0.1f, 0f, 0f), canCapture: true);
            EnemyController c = CreateEnemy(registry, path, new Vector3(0.2f, 0f, 0f), canCapture: true);

            beam.Tick(0.2f);
            Assert.AreEqual(2, beam.ActiveCaptureCount);
            Assert.IsTrue(c.IsCapturable, "Third enemy must remain admissible once a slot frees up.");

            a.ForceResolve(EnemyResolveReason.Removed);
            Assert.AreEqual(1, beam.ActiveCaptureCount);

            beam.Tick(0.2f);
            Assert.AreEqual(2, beam.ActiveCaptureCount);
            Assert.IsFalse(c.IsTargetable, "Beam must keep admitting new enemies without any per-enemy cooldown.");
        }

        [Test]
        public void Scan_SkipsEnemyOutsideAttractionRadius()
        {
            var registry = new EnemyRegistry();
            EnemyPath3D path = CreatePath();
            (UFOTractorBeamController beam, _) = CreateBeam(registry, attractionRadius: 3.5f, maxConcurrent: 8);

            EnemyController farEnemy = CreateEnemy(registry, path, new Vector3(50f, 0f, 0f), canCapture: true);

            beam.Tick(0.2f);

            Assert.AreEqual(0, beam.ActiveCaptureCount);
            Assert.IsTrue(farEnemy.IsTargetable);
        }

        [Test]
        public void Scan_SkipsBoss_NeverConsumesASlot()
        {
            var registry = new EnemyRegistry();
            EnemyPath3D path = CreatePath();
            (UFOTractorBeamController beam, _) = CreateBeam(registry, attractionRadius: 3.5f, maxConcurrent: 8);

            EnemyController boss = CreateEnemy(registry, path, Vector3.zero, canCapture: false);

            beam.Tick(0.2f);

            Assert.AreEqual(0, beam.ActiveCaptureCount);
            Assert.IsTrue(boss.IsTargetable, "Boss must remain targetable by Towers; the beam must never touch it.");
        }

        [Test]
        public void MaxConcurrentCaptures_Zero_MeansUnlimited()
        {
            var registry = new EnemyRegistry();
            EnemyPath3D path = CreatePath();
            (UFOTractorBeamController beam, _) = CreateBeam(registry, attractionRadius: 3.5f, maxConcurrent: 0);

            for (int i = 0; i < 12; i++)
            {
                CreateEnemy(registry, path, new Vector3(0.05f * i, 0f, 0f), canCapture: true);
            }

            beam.Tick(0.2f);

            Assert.AreEqual(12, beam.ActiveCaptureCount);
        }

        [Test]
        public void SetEnabled_False_StopsAdmittingNewCaptures_ButKeepsExistingCount()
        {
            var registry = new EnemyRegistry();
            EnemyPath3D path = CreatePath();
            (UFOTractorBeamController beam, _) = CreateBeam(registry, attractionRadius: 3.5f, maxConcurrent: 8);
            CreateEnemy(registry, path, Vector3.zero, canCapture: true);

            beam.Tick(0.2f);
            Assert.AreEqual(1, beam.ActiveCaptureCount);

            beam.SetEnabled(false);
            CreateEnemy(registry, path, new Vector3(0.1f, 0f, 0f), canCapture: true);
            beam.Tick(0.2f);

            Assert.AreEqual(1, beam.ActiveCaptureCount, "Disabling the beam must only gate admission of new captures.");
        }
    }
}
