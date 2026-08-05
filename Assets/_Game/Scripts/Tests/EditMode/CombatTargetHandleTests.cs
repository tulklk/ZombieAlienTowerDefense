using AlienDefense.Combat;
using AlienDefense.Enemies;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.EditMode
{
    public class CombatTargetHandleTests
    {
        private GameObject _enemyObject;
        private GameObject _pathObject;
        private EnemyDefinition _definition;

        [TearDown]
        public void TearDown()
        {
            if (_enemyObject != null)
            {
                Object.DestroyImmediate(_enemyObject);
            }

            if (_pathObject != null)
            {
                Object.DestroyImmediate(_pathObject);
            }

            if (_definition != null)
            {
                Object.DestroyImmediate(_definition);
            }
        }

        private static T CreateSilently<T>(System.Func<T> factory)
        {
            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            T result = factory();
            Debug.unityLogger.logEnabled = previousLogEnabled;
            return result;
        }

        private (EnemyController controller, EnemyPath3D path) CreateInitializedEnemy(EnemyRegistry registry)
        {
            _enemyObject = new GameObject("TestEnemy");
            var health = _enemyObject.AddComponent<EnemyHealth>();
            var movement = _enemyObject.AddComponent<EnemyMovement>();
            var controller = _enemyObject.AddComponent<EnemyController>();

            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("_health").objectReferenceValue = health;
            controllerSerialized.FindProperty("_movement").objectReferenceValue = movement;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            _pathObject = new GameObject("TestPath");
            var waypointA = new GameObject("A");
            waypointA.transform.SetParent(_pathObject.transform);
            var waypointB = new GameObject("B");
            waypointB.transform.position = new Vector3(0f, 0f, 10f);
            waypointB.transform.SetParent(_pathObject.transform);
            var path = CreateSilently(() => _pathObject.AddComponent<EnemyPath3D>());
            var pathSerialized = new SerializedObject(path);
            SerializedProperty waypointsProperty = pathSerialized.FindProperty("_waypoints");
            waypointsProperty.arraySize = 2;
            waypointsProperty.GetArrayElementAtIndex(0).objectReferenceValue = waypointA.transform;
            waypointsProperty.GetArrayElementAtIndex(1).objectReferenceValue = waypointB.transform;
            pathSerialized.ApplyModifiedPropertiesWithoutUndo();

            _definition = CreateSilently(() => ScriptableObject.CreateInstance<EnemyDefinition>());

            controller.Initialize(_definition, path, null, null, registry, null, null);
            return (controller, path);
        }

        [Test]
        public void Generation_IncrementsOnEachInitialize()
        {
            var registry = new EnemyRegistry();
            (EnemyController enemy, EnemyPath3D path) = CreateInitializedEnemy(registry);
            int firstGeneration = enemy.Generation;

            registry.Unregister(enemy);
            enemy.Initialize(_definition, path, null, null, registry, null, null);

            Assert.Greater(enemy.Generation, firstGeneration);
        }

        [Test]
        public void IsTargetable_IsTrueAfterInitialize_FalseAfterResolve()
        {
            var registry = new EnemyRegistry();
            (EnemyController enemy, _) = CreateInitializedEnemy(registry);

            Assert.IsTrue(enemy.IsTargetable);

            enemy.ForceResolve(EnemyResolveReason.Removed);

            Assert.IsFalse(enemy.IsTargetable);
        }

        [Test]
        public void CombatTargetHandle_IsValid_WhileTargetIsTargetable()
        {
            var registry = new EnemyRegistry();
            (EnemyController enemy, _) = CreateInitializedEnemy(registry);

            var handle = new CombatTargetHandle(enemy);

            Assert.IsTrue(handle.IsValid);
        }

        [Test]
        public void CombatTargetHandle_BecomesInvalid_AfterTargetResolves()
        {
            var registry = new EnemyRegistry();
            (EnemyController enemy, _) = CreateInitializedEnemy(registry);
            var handle = new CombatTargetHandle(enemy);

            enemy.ForceResolve(EnemyResolveReason.Removed);

            Assert.IsFalse(handle.IsValid);
        }

        [Test]
        public void CombatTargetHandle_BecomesInvalid_AfterTargetIsReusedWithNewGeneration()
        {
            var registry = new EnemyRegistry();
            (EnemyController enemy, EnemyPath3D path) = CreateInitializedEnemy(registry);
            var handle = new CombatTargetHandle(enemy);

            enemy.ForceResolve(EnemyResolveReason.Removed);
            enemy.Initialize(_definition, path, null, null, registry, null, null);

            Assert.IsFalse(handle.IsValid);
            Assert.IsTrue(enemy.IsTargetable);
        }

        [Test]
        public void CombatTargetHandle_ForDefaultStruct_IsInvalid()
        {
            var handle = default(CombatTargetHandle);

            Assert.IsFalse(handle.IsValid);
        }
    }
}
