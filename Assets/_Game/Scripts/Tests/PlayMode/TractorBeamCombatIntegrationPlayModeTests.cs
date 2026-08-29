using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Combat;
using AlienDefense.Core;
using AlienDefense.Enemies;
using AlienDefense.Towers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    /// <summary>Covers the boundary between the Tractor Beam and the existing Tower/Projectile combat pipeline:
    /// a captured enemy must stop being a valid Tower target and never take projectile damage, and pooled-instance
    /// generation must still protect an in-flight projectile from hitting a reused enemy.</summary>
    public class TractorBeamCombatIntegrationPlayModeTests
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

        private static T CreateSilently<T>(System.Func<T> factory)
        {
            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            T result = factory();
            Debug.unityLogger.logEnabled = previousLogEnabled;
            return result;
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
            var path = CreateSilently(() => pathObject.AddComponent<EnemyPath3D>());

            var waypointA = new GameObject("A");
            waypointA.transform.position = Vector3.zero;
            _spawnedObjects.Add(waypointA);
            var waypointB = new GameObject("B");
            waypointB.transform.position = new Vector3(0f, 0f, 50f);
            _spawnedObjects.Add(waypointB);

            SetPrivateField(path, "_waypoints", new[] { waypointA.transform, waypointB.transform });
            return path;
        }

        private EnemyController CreateStationaryEnemy(EnemyPath3D path, EnemyRegistry registry, Vector3 position, out EnemyDefinition definition)
        {
            var go = new GameObject("TestEnemy");
            go.SetActive(false);
            _spawnedObjects.Add(go);
            var health = go.AddComponent<EnemyHealth>();
            var movement = go.AddComponent<EnemyMovement>();
            var captureController = go.AddComponent<EnemyCaptureController>();
            var controller = go.AddComponent<EnemyController>();
            SetPrivateField(controller, "_health", health);
            SetPrivateField(controller, "_movement", movement);
            SetPrivateField(controller, "_captureController", captureController);

            definition = CreateSilently(() => ScriptableObject.CreateInstance<EnemyDefinition>());
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_canBeTractorCaptured", true);
            SetPrivateField(definition, "_tractorResistance", 1f);

            go.SetActive(true);
            controller.Initialize(definition, path, null, null, registry, _ => { }, null);
            controller.Movement.StopMovement();
            controller.transform.position = position;
            return controller;
        }

        private ProjectileFactory CreateProjectileFactory(out ProjectileDefinition projectileDefinition, float speed = 5f)
        {
            var template = new GameObject("TestProjectileTemplate");
            template.SetActive(false);
            _spawnedObjects.Add(template);
            template.AddComponent<ProjectileController>();

            var definition = CreateSilently(() => ScriptableObject.CreateInstance<ProjectileDefinition>());
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_prefab", template.GetComponent<ProjectileController>());
            SetPrivateField(definition, "_speed", speed);
            SetPrivateField(definition, "_maximumLifetime", 5f);
            SetPrivateField(definition, "_hitDistance", 0.3f);
            SetPrivateField(definition, "_poolPrewarmCount", 0);
            SetPrivateField(definition, "_poolDefaultCapacity", 10);
            SetPrivateField(definition, "_poolMaximumSize", 10);
            projectileDefinition = definition;

            var runtimeParent = new GameObject("ProjectileRuntimeParent");
            _spawnedObjects.Add(runtimeParent);
            return new ProjectileFactory(new ProjectilePoolRegistry(runtimeParent.transform));
        }

        private TowerDefinition CreateTowerDefinition(ProjectileDefinition projectileDefinition, float range, float damage, float attacksPerSecond)
        {
            var definition = CreateSilently(() => ScriptableObject.CreateInstance<TowerDefinition>());
            _scriptableObjects.Add(definition);

            var level = new TowerLevelData();
            SetPrivateField(level, "_upgradeCost", 0);
            SetPrivateField(level, "_damage", damage);
            SetPrivateField(level, "_range", range);
            SetPrivateField(level, "_attacksPerSecond", attacksPerSecond);
            SetPrivateField(level, "_turretRotationSpeed", 720f);

            SetPrivateField(definition, "_levels", new[] { level });
            SetPrivateField(definition, "_projectileDefinition", projectileDefinition);
            SetPrivateField(definition, "_defaultTargetingMode", TargetingMode.Closest);
            return definition;
        }

        private TowerController CreateTower(Vector3 position)
        {
            var go = new GameObject("TestTower");
            go.SetActive(false);
            _spawnedObjects.Add(go);
            go.transform.position = position;

            var targeting = go.AddComponent<TowerTargeting>();
            var attack = go.AddComponent<TowerAttackController>();
            var visual = go.AddComponent<TowerVisual>();
            var controller = go.AddComponent<TowerController>();

            var firePointObject = new GameObject("FirePoint");
            firePointObject.transform.SetParent(go.transform, false);
            _spawnedObjects.Add(firePointObject);

            SetPrivateField(attack, "_firePoint", firePointObject.transform);
            SetPrivateField(controller, "_targeting", targeting);
            SetPrivateField(controller, "_attack", attack);
            SetPrivateField(controller, "_visual", visual);

            go.SetActive(true);
            return controller;
        }

        private static GameFlowController CreatePlayingWaveGameFlow()
        {
            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();
            gameFlow.BeginPlayingWave();
            return gameFlow;
        }

        [UnityTest]
        public IEnumerator CapturedEnemy_BecomesInvalidTowerTarget_AndTowerRetargets()
        {
            EnemyPath3D path = CreatePath();
            var registry = new EnemyRegistry();
            EnemyController captureTarget = CreateStationaryEnemy(path, registry, new Vector3(2f, 0f, 0f), out _);
            EnemyController fallbackTarget = CreateStationaryEnemy(path, registry, new Vector3(3f, 0f, 0f), out _);

            ProjectileFactory projectileFactory = CreateProjectileFactory(out ProjectileDefinition projectileDefinition);
            TowerDefinition towerDefinition = CreateTowerDefinition(projectileDefinition, 10f, 5f, 4f);
            TowerController tower = CreateTower(Vector3.zero);
            tower.Initialize(towerDefinition, registry, projectileFactory, null, CreatePlayingWaveGameFlow());

            // Let the tower pick its first target (Closest -> captureTarget).
            yield return WaitForGameSeconds(0.3f);
            var targeting = tower.GetComponent<TowerTargeting>();
            Assert.AreEqual(captureTarget, targeting.CurrentTarget);

            var anchor = new GameObject("Anchor");
            _spawnedObjects.Add(anchor);
            anchor.transform.position = captureTarget.transform.position;
            var socket = new GameObject("Socket");
            _spawnedObjects.Add(socket);
            socket.transform.position = captureTarget.transform.position + new Vector3(0f, 5f, 0f);
            var request = new TractorCaptureRequest(anchor.transform, socket.transform, 8f, 6f, 0.2f, 0.15f, true, 0.25f, 240f);
            Assert.IsTrue(captureTarget.TryBeginTractorCapture(request));

            // Target refresh runs on its own interval; give it time to notice captureTarget is no longer targetable.
            yield return WaitForGameSeconds(0.3f);

            Assert.AreNotEqual(captureTarget, targeting.CurrentTarget, "Tower must drop a target that started being captured.");
            Assert.AreEqual(fallbackTarget, targeting.CurrentTarget, "Tower must retarget to the next valid enemy automatically.");
        }

        [UnityTest]
        public IEnumerator CapturedEnemy_TakesNoProjectileDamage()
        {
            EnemyPath3D path = CreatePath();
            var registry = new EnemyRegistry();
            EnemyController target = CreateStationaryEnemy(path, registry, new Vector3(2f, 0f, 0f), out _);

            ProjectileFactory factory = CreateProjectileFactory(out ProjectileDefinition projectileDefinition, speed: 3f);

            var anchor = new GameObject("Anchor");
            _spawnedObjects.Add(anchor);
            anchor.transform.position = target.transform.position;
            var socket = new GameObject("Socket");
            _spawnedObjects.Add(socket);
            socket.transform.position = target.transform.position + new Vector3(0f, 5f, 0f);
            var request = new TractorCaptureRequest(anchor.transform, socket.transform, 8f, 6f, 0.2f, 0.15f, true, 0.25f, 240f);
            target.TryBeginTractorCapture(request);

            float healthAtCaptureStart = target.Health.CurrentHealth;

            var handle = new CombatTargetHandle(target);
            Assert.IsFalse(handle.IsValid, "A CombatTargetHandle taken after capture starts must already be invalid.");

            yield return WaitForGameSeconds(1f);

            Assert.AreEqual(healthAtCaptureStart, target.Health.CurrentHealth, 0.01f);
        }

        [UnityTest]
        public IEnumerator InFlightProjectile_DespawnsSafely_WhenTargetIsCapturedMidFlight()
        {
            EnemyPath3D path = CreatePath();
            var registry = new EnemyRegistry();
            EnemyController target = CreateStationaryEnemy(path, registry, new Vector3(0f, 0f, 8f), out _);

            ProjectileFactory factory = CreateProjectileFactory(out ProjectileDefinition projectileDefinition, speed: 3f);
            var handle = new CombatTargetHandle(target);
            var damage = new DamageInfo(999f, null, target.AimPoint.position);
            var request = new ProjectileSpawnRequest(Vector3.zero, Quaternion.identity, handle, damage);
            ProjectileController projectile = factory.Spawn(projectileDefinition, request);
            Assert.IsNotNull(projectile);

            yield return null;

            var anchor = new GameObject("Anchor");
            _spawnedObjects.Add(anchor);
            anchor.transform.position = target.transform.position;
            var socket = new GameObject("Socket");
            _spawnedObjects.Add(socket);
            socket.transform.position = target.transform.position + new Vector3(0f, 5f, 0f);
            var captureRequest = new TractorCaptureRequest(anchor.transform, socket.transform, 8f, 6f, 0.2f, 0.15f, true, 0.25f, 240f);
            Assert.IsTrue(target.TryBeginTractorCapture(captureRequest));

            float healthAfterCapture = target.Health.CurrentHealth;

            // 8 units at 3 units/sec would take ~2.7s to arrive; wait past that so the projectile has definitely
            // finished (either despawned on invalid target or, if the bug regressed, hit and dealt damage).
            yield return WaitForGameSeconds(3.5f);

            Assert.IsFalse(projectile.gameObject.activeSelf, "Projectile must despawn once its target becomes invalid.");
            Assert.AreEqual(healthAfterCapture, target.Health.CurrentHealth, 0.01f, "A captured enemy must never take the in-flight projectile's damage.");
        }

        [UnityTest]
        public IEnumerator ReusedPooledEnemy_IsNeverDamaged_ByAnOlderGenerationProjectile_AfterTractorCapture()
        {
            EnemyPath3D path = CreatePath();
            var registry = new EnemyRegistry();
            EnemyController original = CreateStationaryEnemy(path, registry, new Vector3(0f, 0f, 20f), out EnemyDefinition definition);
            int captureGeneration = original.Generation;

            ProjectileFactory factory = CreateProjectileFactory(out ProjectileDefinition projectileDefinition, speed: 2f);
            var handle = new CombatTargetHandle(original);
            var damage = new DamageInfo(999f, null, original.AimPoint.position);
            var request = new ProjectileSpawnRequest(Vector3.zero, Quaternion.identity, handle, damage);
            ProjectileController projectile = factory.Spawn(projectileDefinition, request);
            Assert.IsNotNull(projectile);

            yield return null;

            // The projectile's target (generation N) gets captured by the Tractor Beam mid-flight, completes
            // capture (Resolved -> pool release), and the exact same GameObject/EnemyController is immediately
            // reused for a new spawn instance at generation N+1 — before the projectile's next Update.
            var anchor = new GameObject("Anchor");
            _spawnedObjects.Add(anchor);
            anchor.transform.position = original.transform.position;
            var socket = new GameObject("Socket");
            _spawnedObjects.Add(socket);
            socket.transform.position = original.transform.position + new Vector3(0f, 5f, 0f);
            var captureRequest = new TractorCaptureRequest(anchor.transform, socket.transform, 8f, 6f, 0.2f, 0.15f, true, 0.25f, 240f);
            Assert.IsTrue(original.TryBeginTractorCapture(captureRequest));

            var captureController = original.GetComponent<EnemyCaptureController>();
            typeof(EnemyCaptureController).GetMethod("CompleteCapture", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(captureController, null);

            Assert.AreEqual(0, registry.Count, "Captured enemy must have resolved and unregistered.");

            original.transform.position = new Vector3(0f, 0f, 0f);
            original.Initialize(definition, path, null, null, registry, _ => { }, null);
            original.Movement.StopMovement();
            Assert.Greater(original.Generation, captureGeneration);
            float reusedHealth = original.Health.CurrentHealth;

            yield return WaitForGameSeconds(2f);

            Assert.AreEqual(reusedHealth, original.Health.CurrentHealth, 0.01f,
                "The reused (higher-generation) instance must never take damage from a projectile that was targeting the pre-capture generation.");
        }
    }
}
