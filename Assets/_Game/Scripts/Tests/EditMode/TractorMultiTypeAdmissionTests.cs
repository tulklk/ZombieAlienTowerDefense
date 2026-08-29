using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Enemies;
using AlienDefense.Environment;
using AlienDefense.Pickups;
using AlienDefense.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>Covers UFOTractorBeamController admitting Enemy + Energy Pickup + Environment Prop concurrently,
    /// each against its own independent MaxConcurrent* slot pool — a full Enemy beam must never block Energy or
    /// Prop admission and vice versa.</summary>
    public class TractorMultiTypeAdmissionTests
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

        private EnemyController CreateEnemy(EnemyRegistry registry, EnemyPath3D path)
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
            SetPrivateField(definition, "_canBeTractorCaptured", true);
            SetPrivateField(definition, "_tractorResistance", 1f);

            controller.Initialize(definition, path, null, null, registry, _ => { }, null);
            controller.Movement.StopMovement();
            controller.transform.position = Vector3.zero;
            return controller;
        }

        private EnergyPickupController CreatePickup(EnergyPickupRegistry registry)
        {
            var go = new GameObject("TestPickup");
            _spawnedObjects.Add(go);
            var pickup = go.AddComponent<EnergyPickupController>();
            pickup.Initialize(1);
            registry.Register(pickup);
            return pickup;
        }

        private TractorAbsorbableProp CreateProp(TractorAbsorbablePropRegistry registry)
        {
            var go = new GameObject("TestProp");
            _spawnedObjects.Add(go);
            var prop = go.AddComponent<TractorAbsorbableProp>();
            prop.Register(registry);
            return prop;
        }

        private UFOTractorBeamController CreateBeam(EnemyRegistry enemyRegistry, EnergyPickupRegistry energyRegistry,
            TractorAbsorbablePropRegistry propRegistry, int maxEnemy, int maxEnergy, int maxProp)
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
            SetPrivateField(definition, "_maxConcurrentCaptures", maxEnemy);
            SetPrivateField(definition, "_maxConcurrentEnergyAbsorptions", maxEnergy);
            SetPrivateField(definition, "_maxConcurrentPropAbsorptions", maxProp);
            SetPrivateField(definition, "_pullSpeed", 8f);
            SetPrivateField(definition, "_liftSpeed", 6f);
            SetPrivateField(definition, "_beamCenterThreshold", 0.2f);
            SetPrivateField(definition, "_captureSocketThreshold", 0.15f);

            var beam = beamObject.AddComponent<UFOTractorBeamController>();
            SetPrivateField(beam, "_definition", definition);
            SetPrivateField(beam, "_beamGroundAnchor", anchorObject.transform);
            SetPrivateField(beam, "_captureSocket", socketObject.transform);
            beam.Initialize(enemyRegistry, energyRegistry, propRegistry);

            return beam;
        }

        [Test]
        public void EachCategory_AdmitsUpToItsOwnLimit_IndependentlyOfTheOthers()
        {
            var enemyRegistry = new EnemyRegistry();
            var energyRegistry = new EnergyPickupRegistry();
            var propRegistry = new TractorAbsorbablePropRegistry();
            EnemyPath3D path = CreatePath();

            UFOTractorBeamController beam = CreateBeam(enemyRegistry, energyRegistry, propRegistry, maxEnemy: 2, maxEnergy: 3, maxProp: 1);

            for (int i = 0; i < 5; i++)
            {
                CreateEnemy(enemyRegistry, path);
                CreatePickup(energyRegistry);
                CreateProp(propRegistry);
            }

            beam.Tick(0.2f);

            Assert.AreEqual(2, beam.ActiveCaptureCount, "Enemy admission must stop exactly at its own MaxConcurrentCaptures.");
            Assert.AreEqual(3, beam.ActiveEnergyAbsorptionCount, "Energy admission must stop exactly at its own MaxConcurrentEnergyAbsorptions.");
            Assert.AreEqual(1, beam.ActivePropAbsorptionCount, "Prop admission must stop exactly at its own MaxConcurrentPropAbsorptions.");
            Assert.AreEqual(6, beam.TotalActiveAbsorptionCount, "Total must be the sum across all three categories.");
        }

        [Test]
        public void EnemySlotsFull_NeverBlocksEnergyOrPropAdmission()
        {
            var enemyRegistry = new EnemyRegistry();
            var energyRegistry = new EnergyPickupRegistry();
            var propRegistry = new TractorAbsorbablePropRegistry();
            EnemyPath3D path = CreatePath();

            UFOTractorBeamController beam = CreateBeam(enemyRegistry, energyRegistry, propRegistry, maxEnemy: 1, maxEnergy: 5, maxProp: 5);

            CreateEnemy(enemyRegistry, path);
            CreateEnemy(enemyRegistry, path); // second enemy: no free slot, must simply wait
            CreatePickup(energyRegistry);
            CreateProp(propRegistry);

            beam.Tick(0.2f);

            Assert.AreEqual(1, beam.ActiveCaptureCount);
            Assert.AreEqual(1, beam.ActiveEnergyAbsorptionCount, "A full Enemy beam must not block Energy admission.");
            Assert.AreEqual(1, beam.ActivePropAbsorptionCount, "A full Enemy beam must not block Prop admission.");
        }
    }
}
