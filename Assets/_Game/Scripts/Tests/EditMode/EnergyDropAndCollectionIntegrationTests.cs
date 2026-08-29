using System.Reflection;
using AlienDefense.Economy;
using AlienDefense.Enemies;
using AlienDefense.Pickups;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>End-to-end regression for the exact scenario in the refactor spec: a Tower kill (Defeated) must
    /// NOT grant Energy/XP directly — it only drops an EnergyPickup. Energy/XP appear ONLY once that pickup is
    /// separately collected (its own full Pull->Lift->Collected cycle completes).</summary>
    public class EnergyDropAndCollectionIntegrationTests
    {
        private GameObject _enemyPrefabObject;
        private GameObject _enemyPoolParent;
        private GameObject _pickupPrefabObject;
        private GameObject _pickupPoolParent;
        private GameObject _pathObject;
        private ScriptableObject _definition;

        [TearDown]
        public void TearDown()
        {
            if (_enemyPrefabObject != null) Object.DestroyImmediate(_enemyPrefabObject);
            if (_enemyPoolParent != null) Object.DestroyImmediate(_enemyPoolParent);
            if (_pickupPrefabObject != null) Object.DestroyImmediate(_pickupPrefabObject);
            if (_pickupPoolParent != null) Object.DestroyImmediate(_pickupPoolParent);
            if (_pathObject != null) Object.DestroyImmediate(_pathObject);
            if (_definition != null) Object.DestroyImmediate(_definition);
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

        [Test]
        public void Defeated_DropsPickup_NotDirectReward_ThenCollectingIt_GrantsWalletAndXp()
        {
            const int rewardValue = 4;

            // --- Energy/XP services (the ONLY funnel to reward) ---
            var wallet = new EnergyWalletService();
            var progression = new PlayerLevelProgressionService();
            var collection = new EnergyCollectionService(wallet, progression);

            // --- EnergyPickup pool/registry/factory/drop service ---
            _pickupPrefabObject = new GameObject("PickupPrefab");
            var pickupTemplate = _pickupPrefabObject.AddComponent<EnergyPickupController>();
            _pickupPoolParent = new GameObject("PickupPoolParent");
            var pickupPool = new EnergyPickupPool(pickupTemplate, _pickupPoolParent.transform, 4, 16, collectionChecks: false);
            var pickupRegistry = new EnergyPickupRegistry();
            var pickupFactory = new EnergyPickupFactory(pickupPool, pickupRegistry, collection);
            var dropService = new EnergyDropService(pickupFactory);

            // --- Enemy pool/registry/factory (with EnergyDropService wired, EconomyService intentionally null) ---
            var enemyPath = CreatePath();
            _enemyPrefabObject = new GameObject("EnemyPrefab");
            var health = _enemyPrefabObject.AddComponent<EnemyHealth>();
            var movement = _enemyPrefabObject.AddComponent<EnemyMovement>();
            var captureController = _enemyPrefabObject.AddComponent<EnemyCaptureController>();
            var enemyPrefabController = _enemyPrefabObject.AddComponent<EnemyController>();
            SetPrivateField(enemyPrefabController, "_health", health);
            SetPrivateField(enemyPrefabController, "_movement", movement);
            SetPrivateField(enemyPrefabController, "_captureController", captureController);

            var definition = CreateSilently(() => ScriptableObject.CreateInstance<EnemyDefinition>());
            _definition = definition;
            SetPrivateField(definition, "_maxHealth", 10f);
            SetPrivateField(definition, "_rewardResource", rewardValue);
            SetPrivateField(definition, "_prefab", enemyPrefabController);

            var enemyRegistry = new EnemyRegistry();
            _enemyPoolParent = new GameObject("EnemyPoolParent");
            var enemyPoolRegistry = new EnemyPoolRegistry(_enemyPoolParent.transform);
            var enemyFactory = new EnemyFactory(enemyPoolRegistry, enemyRegistry, economy: null, baseHealth: null,
                cameraTransform: null, vfxService: null, energyDrop: dropService);

            // --- Act: spawn and kill the enemy (Defeated) ---
            EnemyController enemy = enemyFactory.Spawn(definition, enemyPath, Vector3.zero, Quaternion.identity);
            Assert.IsNotNull(enemy);

            enemy.ApplyDebugDamage(9999f);

            Assert.AreEqual(0, wallet.CurrentEnergy, "A Defeated kill must not grant Energy directly.");
            Assert.AreEqual(0, progression.CurrentExperience, "A Defeated kill must not grant XP directly.");
            Assert.AreEqual(1, pickupRegistry.Count, "Defeated must drop exactly one EnergyPickup.");

            EnergyPickupController pickup = pickupRegistry.GetAt(0);
            Assert.AreEqual(rewardValue, pickup.Value, "The dropped pickup's value must match the enemy's RewardResource.");

            // --- Act: the UFO tractor-beams the dropped pickup all the way to Collected ---
            var anchorObject = new GameObject("Anchor");
            var socketObject = new GameObject("Socket");
            var request = new TractorEnergyAbsorptionRequest(
                anchorObject.transform, socketObject.transform,
                pullSpeed: 8f, liftSpeed: 6f, centerThreshold: 0.2f, socketThreshold: 0.15f,
                shrinkDuringLift: true, minimumVisualScale: 0.25f, spinSpeedDegreesPerSecond: 0f);

            bool started = pickup.TryBeginAbsorption(request);
            Assert.IsTrue(started);

            for (int i = 0; i < 10; i++)
            {
                pickup.Tick(0.5f);
            }

            Object.DestroyImmediate(anchorObject);
            Object.DestroyImmediate(socketObject);

            Assert.AreEqual(0, pickupRegistry.Count, "A Collected pickup must unregister itself.");
            Assert.AreEqual(rewardValue, wallet.CurrentEnergy, "Only actually collecting the pickup grants Energy.");
            Assert.AreEqual(rewardValue, progression.CurrentExperience, "Only actually collecting the pickup grants XP.");
        }

        private EnemyPath3D CreatePath()
        {
            var pathObject = new GameObject("TestPath");
            _pathObject = pathObject;
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
    }
}
