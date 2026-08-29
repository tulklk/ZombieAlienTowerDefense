using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Base;
using AlienDefense.Combat;
using AlienDefense.Economy;
using AlienDefense.Enemies;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    public class PoolStatusResetTests
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

        private static object GetPrivateField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            return field.GetValue(target);
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

        private (EnemyController template, EnemyHealth health, EnemyMovement movement, EnemyStatusController statusController, EnemyShield shield, EnemyDefense defense, BossController boss)
            CreateFullTemplate()
        {
            var go = new GameObject("TestEnemyTemplate");
            go.SetActive(false);
            _spawnedObjects.Add(go);

            var health = go.AddComponent<EnemyHealth>();
            var movement = go.AddComponent<EnemyMovement>();
            var shield = go.AddComponent<EnemyShield>();
            var defense = go.AddComponent<EnemyDefense>();
            SetPrivateField(defense, "_physicalDamageReductionPercent", 0.3f);
            var statusController = go.AddComponent<EnemyStatusController>();
            SetPrivateField(statusController, "_health", health);
            SetPrivateField(statusController, "_movement", movement);
            var controller = go.AddComponent<EnemyController>();
            var boss = go.AddComponent<BossController>();

            SetPrivateField(controller, "_health", health);
            SetPrivateField(controller, "_movement", movement);
            SetPrivateField(controller, "_statusController", statusController);
            SetPrivateField(controller, "_shield", shield);
            SetPrivateField(controller, "_defense", defense);
            SetPrivateField(controller, "_bossController", boss);

            SetPrivateField(boss, "_enemyController", controller);
            SetPrivateField(boss, "_health", health);
            SetPrivateField(boss, "_movement", movement);
            SetPrivateField(boss, "_defense", defense);

            return (controller, health, movement, statusController, shield, defense, boss);
        }

        private StatusEffectDefinition CreateSlow(float magnitude)
        {
            var definition = ScriptableObject.CreateInstance<StatusEffectDefinition>();
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_type", StatusEffectType.Slow);
            SetPrivateField(definition, "_duration", 10f);
            SetPrivateField(definition, "_magnitude", magnitude);
            SetPrivateField(definition, "_tickInterval", 1f);
            SetPrivateField(definition, "_stackingRule", StatusStackingRule.RefreshDurationOnly);
            SetPrivateField(definition, "_maxStacks", 1);
            return definition;
        }

        [UnityTest]
        public IEnumerator PoolReuse_ClearsStatusEffects_AndRestoresFullSpeed()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            (EnemyController template, _, EnemyMovement movement, EnemyStatusController statusController, _, _, _) = CreateFullTemplate();

            var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_prefab", template);
            SetPrivateField(definition, "_poolPrewarmCount", 0);
            SetPrivateField(definition, "_poolDefaultCapacity", 1);
            SetPrivateField(definition, "_poolMaximumSize", 1);

            var runtimeParent = new GameObject("RuntimeParent");
            _spawnedObjects.Add(runtimeParent);
            var registry = new EnemyRegistry();
            var factory = new EnemyFactory(new EnemyPoolRegistry(runtimeParent.transform), registry, new EconomyService(0), new BaseHealthService(100), null);

            EnemyController first = factory.Spawn(definition, path, Vector3.zero, Quaternion.identity);
            EnemyStatusController firstStatus = first.GetComponent<EnemyStatusController>();
            firstStatus.ApplyStatus(CreateSlow(0.3f));
            yield return null;
            Assert.AreEqual(0.3f, (float)GetPrivateField(first.GetComponent<EnemyMovement>(), "_statusSpeedMultiplier"), 0.001f);

            first.ForceResolve(EnemyResolveReason.Removed);
            yield return null;

            EnemyController second = factory.Spawn(definition, path, Vector3.zero, Quaternion.identity);
            Assert.AreSame(first, second, "Pool of size 1 must reuse the same instance.");

            var activeEffects = (System.Collections.IDictionary)GetPrivateField(second.GetComponent<EnemyStatusController>(), "_activeEffects");
            Assert.AreEqual(0, activeEffects.Count);
            Assert.AreEqual(1f, (float)GetPrivateField(second.GetComponent<EnemyMovement>(), "_statusSpeedMultiplier"), 0.001f);
        }

        [UnityTest]
        public IEnumerator PoolReuse_RestoresShieldToFull()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            (EnemyController template, _, _, _, EnemyShield shield, _, _) = CreateFullTemplate();
            SetPrivateField(shield, "_maxShield", 40f);

            var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_prefab", template);
            SetPrivateField(definition, "_poolPrewarmCount", 0);
            SetPrivateField(definition, "_poolDefaultCapacity", 1);
            SetPrivateField(definition, "_poolMaximumSize", 1);

            var runtimeParent = new GameObject("RuntimeParent");
            _spawnedObjects.Add(runtimeParent);
            var registry = new EnemyRegistry();
            var factory = new EnemyFactory(new EnemyPoolRegistry(runtimeParent.transform), registry, new EconomyService(0), new BaseHealthService(100), null);

            EnemyController first = factory.Spawn(definition, path, Vector3.zero, Quaternion.identity);
            yield return null;
            first.GetComponent<EnemyShield>().Absorb(25f);
            Assert.Less(first.GetComponent<EnemyShield>().CurrentShield, 40f);

            first.ForceResolve(EnemyResolveReason.Removed);
            yield return null;

            EnemyController second = factory.Spawn(definition, path, Vector3.zero, Quaternion.identity);
            Assert.AreEqual(40f, second.GetComponent<EnemyShield>().CurrentShield, 0.001f);
        }

        [UnityTest]
        public IEnumerator PoolReuse_RestoresDefenseToInspectorBase()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            (EnemyController template, _, _, _, _, EnemyDefense defense, _) = CreateFullTemplate();

            var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_prefab", template);
            SetPrivateField(definition, "_poolPrewarmCount", 0);
            SetPrivateField(definition, "_poolDefaultCapacity", 1);
            SetPrivateField(definition, "_poolMaximumSize", 1);

            var runtimeParent = new GameObject("RuntimeParent");
            _spawnedObjects.Add(runtimeParent);
            var registry = new EnemyRegistry();
            var factory = new EnemyFactory(new EnemyPoolRegistry(runtimeParent.transform), registry, new EconomyService(0), new BaseHealthService(100), null);

            EnemyController first = factory.Spawn(definition, path, Vector3.zero, Quaternion.identity);
            yield return null;
            first.GetComponent<EnemyDefense>().SetPhysicalDamageReductionPercent(0.9f);

            first.ForceResolve(EnemyResolveReason.Removed);
            yield return null;

            EnemyController second = factory.Spawn(definition, path, Vector3.zero, Quaternion.identity);
            float damageAfterReuse = second.GetComponent<EnemyDefense>().ModifyIncomingDamage(100f, DamageType.Physical);
            Assert.AreEqual(70f, damageAfterReuse, 0.001f, "Base reduction (0.3) from the inspector must be restored, not the runtime override (0.9).");
        }

        [UnityTest]
        public IEnumerator PoolReuse_ResetsBossStateToPhaseOne()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            (EnemyController template, EnemyHealth health, _, _, _, _, BossController boss) = CreateFullTemplate();

            var behavior = ScriptableObject.CreateInstance<BossBehaviorDefinition>();
            _scriptableObjects.Add(behavior);
            SetPrivateField(behavior, "_phaseTwoHealthThreshold", 0.5f);
            SetPrivateField(behavior, "_minionSpawnInterval", 1000f);
            SetPrivateField(behavior, "_phaseTwoMinionIntervalMultiplier", 0.6f);
            SetPrivateField(behavior, "_phaseTwoSpeedMultiplier", 1.4f);
            SetPrivateField(behavior, "_phaseOneDamageReductionPercent", 0.1f);
            SetPrivateField(behavior, "_phaseTwoDamageReductionPercent", 0.3f);
            SetPrivateField(boss, "_behavior", behavior);

            var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_prefab", template);
            SetPrivateField(definition, "_maxHealth", 100f);
            SetPrivateField(definition, "_poolPrewarmCount", 0);
            SetPrivateField(definition, "_poolDefaultCapacity", 1);
            SetPrivateField(definition, "_poolMaximumSize", 1);

            var runtimeParent = new GameObject("RuntimeParent");
            _spawnedObjects.Add(runtimeParent);
            var registry = new EnemyRegistry();
            var factory = new EnemyFactory(new EnemyPoolRegistry(runtimeParent.transform), registry, new EconomyService(0), new BaseHealthService(100), null);

            EnemyController first = factory.Spawn(definition, path, Vector3.zero, Quaternion.identity);
            yield return null;
            BossController firstBoss = first.GetComponent<BossController>();
            firstBoss.Initialize(new NullSpawnCoordinator());
            first.ApplyDebugDamage(60f);
            yield return null;

            Assert.AreEqual(BossState.PhaseTwo, firstBoss.State, "Precondition: boss must actually reach Phase Two before pooling can be proven to reset it.");

            first.ForceResolve(EnemyResolveReason.Removed);
            yield return null;

            EnemyController second = factory.Spawn(definition, path, Vector3.zero, Quaternion.identity);
            Assert.AreEqual(BossState.PhaseOne, second.GetComponent<BossController>().State);
        }

        private sealed class NullSpawnCoordinator : IEnemySpawnCoordinator
        {
            public EnemyController SpawnTrackedEnemy(EnemyDefinition definition) => null;
        }
    }
}
