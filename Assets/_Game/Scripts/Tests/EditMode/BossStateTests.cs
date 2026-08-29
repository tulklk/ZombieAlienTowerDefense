using System.Reflection;
using AlienDefense.Combat;
using AlienDefense.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class BossStateTests
    {
        private GameObject _bossObject;
        private EnemyDefinition _minionDefinition;
        private BossBehaviorDefinition _behavior;

        [TearDown]
        public void TearDown()
        {
            if (_bossObject != null)
            {
                Object.DestroyImmediate(_bossObject);
            }

            if (_minionDefinition != null)
            {
                Object.DestroyImmediate(_minionDefinition);
            }

            if (_behavior != null)
            {
                Object.DestroyImmediate(_behavior);
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

        private static void InvokePrivateMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"Method '{methodName}' not found on {target.GetType().Name}.");
            method.Invoke(target, null);
        }

        private static T CreateSilently<T>(System.Func<T> factory)
        {
            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            T result = factory();
            Debug.unityLogger.logEnabled = previousLogEnabled;
            return result;
        }

        private sealed class FakeSpawnCoordinator : IEnemySpawnCoordinator
        {
            public int SpawnCount;

            public EnemyController SpawnTrackedEnemy(EnemyDefinition definition)
            {
                SpawnCount++;
                return null;
            }
        }

        private (BossController boss, EnemyHealth health, FakeSpawnCoordinator coordinator) CreateBoss(
            float phaseTwoThreshold = 0.5f, int minionCountPerBurst = 2, float phaseTwoSpeedMultiplier = 1.4f, float phaseTwoReduction = 0.3f)
        {
            _bossObject = new GameObject("TestBoss");
            var health = _bossObject.AddComponent<EnemyHealth>();
            health.Initialize(100f);
            var movement = _bossObject.AddComponent<EnemyMovement>();
            var defense = _bossObject.AddComponent<EnemyDefense>();
            var boss = _bossObject.AddComponent<BossController>();

            _minionDefinition = CreateSilently(() => ScriptableObject.CreateInstance<EnemyDefinition>());

            _behavior = CreateSilently(() => ScriptableObject.CreateInstance<BossBehaviorDefinition>());
            SetPrivateField(_behavior, "_phaseTwoHealthThreshold", phaseTwoThreshold);
            SetPrivateField(_behavior, "_minionDefinition", _minionDefinition);
            SetPrivateField(_behavior, "_minionCountPerBurst", minionCountPerBurst);
            SetPrivateField(_behavior, "_minionSpawnInterval", 8f);
            SetPrivateField(_behavior, "_phaseTwoMinionIntervalMultiplier", 0.6f);
            SetPrivateField(_behavior, "_phaseTwoSpeedMultiplier", phaseTwoSpeedMultiplier);
            SetPrivateField(_behavior, "_phaseOneDamageReductionPercent", 0.1f);
            SetPrivateField(_behavior, "_phaseTwoDamageReductionPercent", phaseTwoReduction);

            SetPrivateField(boss, "_health", health);
            SetPrivateField(boss, "_movement", movement);
            SetPrivateField(boss, "_defense", defense);
            SetPrivateField(boss, "_behavior", _behavior);

            var coordinator = new FakeSpawnCoordinator();
            boss.Initialize(coordinator);

            return (boss, health, coordinator);
        }

        [Test]
        public void Initialize_StartsInPhaseOne()
        {
            (BossController boss, _, _) = CreateBoss();

            Assert.AreEqual(BossState.PhaseOne, boss.State);
        }

        [Test]
        public void Update_HealthBelowThreshold_TransitionsToPhaseTwo()
        {
            (BossController boss, EnemyHealth health, _) = CreateBoss(phaseTwoThreshold: 0.5f);
            health.TryApplyDamage(51f);

            BossState? phaseFromEvent = null;
            boss.PhaseChanged += state => phaseFromEvent = state;
            InvokePrivateMethod(boss, "Update");

            Assert.AreEqual(BossState.PhaseTwo, boss.State);
            Assert.AreEqual(BossState.PhaseTwo, phaseFromEvent);
        }

        [Test]
        public void Update_HealthAboveThreshold_StaysInPhaseOne()
        {
            (BossController boss, EnemyHealth health, _) = CreateBoss(phaseTwoThreshold: 0.5f);
            health.TryApplyDamage(10f);

            InvokePrivateMethod(boss, "Update");

            Assert.AreEqual(BossState.PhaseOne, boss.State);
        }

        [Test]
        public void EnterPhaseTwo_AppliesSpeedMultiplier_AndTemporaryResistance()
        {
            (BossController boss, EnemyHealth health, _) = CreateBoss(phaseTwoThreshold: 0.5f, phaseTwoSpeedMultiplier: 1.6f, phaseTwoReduction: 0.4f);
            health.TryApplyDamage(60f);

            InvokePrivateMethod(boss, "Update");

            var movement = (EnemyMovement)GetPrivateField(boss, "_movement");
            Assert.AreEqual(1.6f, (float)GetPrivateField(movement, "_behaviorSpeedMultiplier"), 0.001f);

            var defense = (EnemyDefense)GetPrivateField(boss, "_defense");
            Assert.AreEqual(60f, defense.ModifyIncomingDamage(100f, DamageType.Physical), 0.001f);
        }

        [Test]
        public void Update_MinionTimerElapsed_SpawnsExactlyMinionCountPerBurst()
        {
            (BossController boss, _, FakeSpawnCoordinator coordinator) = CreateBoss(minionCountPerBurst: 3);
            SetPrivateField(boss, "_minionTimer", 0f);

            InvokePrivateMethod(boss, "Update");

            Assert.AreEqual(3, coordinator.SpawnCount);
        }

        [Test]
        public void Update_MinionTimerNotElapsed_DoesNotSpawn()
        {
            (BossController boss, _, FakeSpawnCoordinator coordinator) = CreateBoss();
            SetPrivateField(boss, "_minionTimer", 100f);

            InvokePrivateMethod(boss, "Update");

            Assert.AreEqual(0, coordinator.SpawnCount);
        }

        [Test]
        public void ResetState_RestoresPhaseOneAndDeactivates()
        {
            (BossController boss, EnemyHealth health, FakeSpawnCoordinator coordinator) = CreateBoss(phaseTwoThreshold: 0.5f);
            health.TryApplyDamage(60f);
            InvokePrivateMethod(boss, "Update");
            Assert.AreEqual(BossState.PhaseTwo, boss.State);

            boss.ResetState();

            Assert.AreEqual(BossState.PhaseOne, boss.State);

            SetPrivateField(boss, "_minionTimer", 0f);
            InvokePrivateMethod(boss, "Update");
            Assert.AreEqual(0, coordinator.SpawnCount, "ResetState must deactivate minion spawning until Initialize is called again.");
        }
    }
}
