using System.Collections.Generic;
using AlienDefense.Combat;
using AlienDefense.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>EnemyHealth.DamageLanded drives the floating damage numbers: it must report the damage that actually
    /// landed (clamped to remaining health), keep the hit's popup style, and stay silent for hits that do nothing.</summary>
    public class DamageLandedEventTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<(EnemyHealth health, DamagePopupStyle style, float amount)> _events =
            new List<(EnemyHealth, DamagePopupStyle, float)>();

        [SetUp]
        public void SetUp()
        {
            EnemyHealth.DamageLanded += Record;
        }

        [TearDown]
        public void TearDown()
        {
            EnemyHealth.DamageLanded -= Record;
            foreach (GameObject go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
            _events.Clear();
        }

        [Test]
        public void ReportsFinalDamage_ClampedToRemainingHealth_WithPopupStyle()
        {
            EnemyHealth health = CreateHealth(30f);

            health.TryApplyDamage(new DamageInfo(20f, null, Vector3.zero, popupStyle: DamagePopupStyle.Fire));
            health.TryApplyDamage(new DamageInfo(20f, null, Vector3.zero, popupStyle: DamagePopupStyle.Fire));

            Assert.AreEqual(2, _events.Count);
            Assert.AreEqual(20f, _events[0].amount, 0.001f);
            Assert.AreEqual(10f, _events[1].amount, 0.001f, "Only the 10 health that was left is reported.");
            Assert.AreEqual(DamagePopupStyle.Fire, _events[1].style);
        }

        [Test]
        public void SilentForImmuneOrDeadTargets()
        {
            EnemyHealth health = CreateHealth(10f);
            health.SetEncounterImmune(true);
            health.TryApplyDamage(new DamageInfo(5f, null, Vector3.zero, popupStyle: DamagePopupStyle.Fire));
            Assert.AreEqual(0, _events.Count, "Immune: no damage, no number.");

            health.SetEncounterImmune(false);
            health.TryApplyDamage(new DamageInfo(50f, null, Vector3.zero, popupStyle: DamagePopupStyle.Fire));
            health.TryApplyDamage(new DamageInfo(50f, null, Vector3.zero, popupStyle: DamagePopupStyle.Fire));
            Assert.AreEqual(1, _events.Count, "A dead enemy takes no further numbers.");
        }

        [Test]
        public void SplashKeepsPopupStyle_ForEveryEnemyHit()
        {
            var provider = new Provider();
            var healths = new List<EnemyHealth>();
            for (int i = 0; i < 3; i++)
            {
                EnemyHealth health = CreateHealth(100f);
                health.transform.position = new Vector3(i * 0.5f, 0f, 0f);
                healths.Add(health);
                provider.Targets.Add(new Target { AimPoint = health.transform, Damageable = health });
            }

            new AreaDamageResolver(provider).ResolveSplash(Vector3.zero, 3f,
                new DamageInfo(25f, null, Vector3.zero, popupStyle: DamagePopupStyle.Fire));

            Assert.AreEqual(3, _events.Count, "One number per enemy the blast hurt.");
            for (int i = 0; i < 3; i++)
            {
                Assert.AreSame(healths[i], _events[i].health);
                Assert.AreEqual(DamagePopupStyle.Fire, _events[i].style);
                Assert.AreEqual(25f, _events[i].amount, 0.001f);
            }
        }

        private void Record(EnemyHealth health, DamageInfo info, float amount)
        {
            _events.Add((health, info.PopupStyle, amount));
        }

        private EnemyHealth CreateHealth(float max)
        {
            var go = new GameObject("Enemy");
            _objects.Add(go);
            var health = go.AddComponent<EnemyHealth>();
            health.Initialize(max);
            return health;
        }

        private sealed class Target : ICombatTarget
        {
            public bool IsTargetable => true;
            public int Generation => 0;
            public Transform AimPoint { get; set; }
            public IDamageable Damageable { get; set; }
            public IStatusApplicable StatusController => null;
        }

        private sealed class Provider : ISplashTargetProvider
        {
            public readonly List<ICombatTarget> Targets = new List<ICombatTarget>();
            public int Count => Targets.Count;
            public ICombatTarget GetAt(int index) => Targets[index];
        }
    }
}
