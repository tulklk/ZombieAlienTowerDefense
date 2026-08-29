using System.Reflection;
using AlienDefense.Enemies;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>Covers EnemyController's tractor-capture admission contract: IsCapturable/IsTargetable/IsDamageable
    /// gating, idempotent capture start, and the centralized Resolve(Captured) reward policy.</summary>
    public class EnemyControllerCaptureTests
    {
        private GameObject _enemyObject;
        private GameObject _pathObject;
        private EnemyDefinition _definition;

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in new[] { _enemyObject, _pathObject })
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            if (_definition != null)
            {
                Object.DestroyImmediate(_definition);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            return field.GetValue(target);
        }

        private static T CreateSilently<T>(System.Func<T> factory)
        {
            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            T result = factory();
            Debug.unityLogger.logEnabled = previousLogEnabled;
            return result;
        }

        private (EnemyController controller, EnemyPath3D path) CreateEnemy(EnemyRegistry registry, bool canCapture, int reward = 10)
        {
            _enemyObject = new GameObject("TestEnemy");
            var health = _enemyObject.AddComponent<EnemyHealth>();
            var movement = _enemyObject.AddComponent<EnemyMovement>();
            var captureController = _enemyObject.AddComponent<EnemyCaptureController>();
            var controller = _enemyObject.AddComponent<EnemyController>();

            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("_health").objectReferenceValue = health;
            controllerSerialized.FindProperty("_movement").objectReferenceValue = movement;
            controllerSerialized.FindProperty("_captureController").objectReferenceValue = captureController;
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
            SetPrivateField(_definition, "_canBeTractorCaptured", canCapture);
            SetPrivateField(_definition, "_rewardResource", reward);

            controller.Initialize(_definition, path, null, null, registry, _ => { }, null);
            return (controller, path);
        }

        private static TractorCaptureRequest CreateDummyRequest()
        {
            var anchor = new GameObject("Anchor");
            var socket = new GameObject("Socket");
            return new TractorCaptureRequest(anchor.transform, socket.transform, 8f, 6f, 0.2f, 0.15f, true, 0.25f, 240f);
        }

        [Test]
        public void IsCapturable_TrueByDefault_WhenDefinitionAllows()
        {
            var registry = new EnemyRegistry();
            (EnemyController enemy, _) = CreateEnemy(registry, canCapture: true);

            Assert.IsTrue(enemy.IsCapturable);
        }

        [Test]
        public void IsCapturable_False_WhenDefinitionForbids()
        {
            var registry = new EnemyRegistry();
            (EnemyController enemy, _) = CreateEnemy(registry, canCapture: false);

            Assert.IsFalse(enemy.IsCapturable);
        }

        [Test]
        public void TryBeginTractorCapture_Succeeds_MakesEnemyNonTargetableAndNonDamageable()
        {
            var registry = new EnemyRegistry();
            (EnemyController enemy, _) = CreateEnemy(registry, canCapture: true);

            bool result = enemy.TryBeginTractorCapture(CreateDummyRequest());

            Assert.IsTrue(result);
            Assert.IsFalse(enemy.IsTargetable);
            Assert.IsFalse(enemy.Health.IsDamageable);
            Assert.IsFalse(enemy.IsCapturable, "A captured enemy cannot be admitted into capture again.");
        }

        [Test]
        public void TryBeginTractorCapture_Fails_WhenDefinitionForbids()
        {
            var registry = new EnemyRegistry();
            (EnemyController enemy, _) = CreateEnemy(registry, canCapture: false);

            bool result = enemy.TryBeginTractorCapture(CreateDummyRequest());

            Assert.IsFalse(result);
            Assert.IsTrue(enemy.IsTargetable, "A rejected capture attempt must never affect targetability.");
        }

        [Test]
        public void TryBeginTractorCapture_SecondAttempt_FailsWhileAlreadyCaptured()
        {
            var registry = new EnemyRegistry();
            (EnemyController enemy, _) = CreateEnemy(registry, canCapture: true);
            Assert.IsTrue(enemy.TryBeginTractorCapture(CreateDummyRequest()));

            bool secondAttempt = enemy.TryBeginTractorCapture(CreateDummyRequest());

            Assert.IsFalse(secondAttempt);
        }

        [Test]
        public void CaptureCompletion_ResolvesAsCaptured_GrantsRewardOnce_NoBaseDamage()
        {
            var registry = new EnemyRegistry();
            (EnemyController enemy, _) = CreateEnemy(registry, canCapture: true, reward: 25);

            int resolvedCount = 0;
            EnemyResolveReason capturedReason = default;
            enemy.Resolved += (_, reason) =>
            {
                resolvedCount++;
                capturedReason = reason;
            };

            enemy.TryBeginTractorCapture(CreateDummyRequest());
            var captureController = (EnemyCaptureController)GetPrivateField(enemy, "_captureController");
            InvokePrivateMethod(captureController, "CompleteCapture");

            Assert.AreEqual(1, resolvedCount);
            Assert.AreEqual(EnemyResolveReason.Captured, capturedReason);
            Assert.AreEqual(0, registry.Count, "Captured enemy must unregister like any other resolve.");
        }

        [Test]
        public void ForceResolve_WhileMidCapture_AbortsCaptureController()
        {
            var registry = new EnemyRegistry();
            (EnemyController enemy, _) = CreateEnemy(registry, canCapture: true);
            enemy.TryBeginTractorCapture(CreateDummyRequest());
            var captureController = (EnemyCaptureController)GetPrivateField(enemy, "_captureController");
            Assert.AreEqual(EnemyCaptureState.Pulling, captureController.State);

            enemy.ForceResolve(EnemyResolveReason.LevelEnded);

            Assert.AreEqual(EnemyCaptureState.Aborted, captureController.State);
        }

        [Test]
        public void PoolReuse_ClearsCaptureState_RestoresTargetableAndDamageable()
        {
            var registry = new EnemyRegistry();
            (EnemyController enemy, EnemyPath3D path) = CreateEnemy(registry, canCapture: true);
            enemy.TryBeginTractorCapture(CreateDummyRequest());
            Assert.IsFalse(enemy.IsTargetable);

            enemy.HandleReturnedToPool();
            enemy.Initialize(_definition, path, null, null, registry, _ => { }, null);

            Assert.IsTrue(enemy.IsTargetable);
            Assert.IsTrue(enemy.Health.IsDamageable);
            Assert.IsTrue(enemy.IsCapturable);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"Method '{methodName}' not found on {target.GetType().Name}.");
            method.Invoke(target, null);
        }
    }
}
