using System.Collections.Generic;
using AlienDefense.Combat;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class AreaDamageTests
    {
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();

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
        }

        private sealed class FakeDamageable : IDamageable
        {
            public int HitCount;
            public float TotalDamage;
            public bool IsDamageable => true;

            public bool TryApplyDamage(in DamageInfo damageInfo)
            {
                HitCount++;
                TotalDamage += damageInfo.Amount;
                return true;
            }
        }

        private sealed class FakeCombatTarget : ICombatTarget
        {
            public bool IsTargetable { get; set; } = true;
            public int Generation => 0;
            public Transform AimPoint { get; set; }
            public IDamageable Damageable { get; set; }
            public IStatusApplicable StatusController => null;
        }

        private sealed class FakeSplashTargetProvider : ISplashTargetProvider
        {
            private readonly List<ICombatTarget> _targets = new List<ICombatTarget>();

            public void Add(ICombatTarget target)
            {
                _targets.Add(target);
            }

            public int Count => _targets.Count;
            public ICombatTarget GetAt(int index) => _targets[index];
        }

        private FakeCombatTarget CreateTarget(Vector3 position, bool isTargetable = true)
        {
            var go = new GameObject("FakeTarget");
            go.transform.position = position;
            _spawnedObjects.Add(go);

            return new FakeCombatTarget
            {
                AimPoint = go.transform,
                Damageable = new FakeDamageable(),
                IsTargetable = isTargetable
            };
        }

        [Test]
        public void ResolveSplash_DamagesEveryTargetInsideRadius()
        {
            var provider = new FakeSplashTargetProvider();
            FakeCombatTarget inside = CreateTarget(new Vector3(1f, 0f, 0f));
            provider.Add(inside);
            var resolver = new AreaDamageResolver(provider);

            int hitCount = resolver.ResolveSplash(Vector3.zero, 2f, new DamageInfo(10f, null, Vector3.zero));

            Assert.AreEqual(1, hitCount);
            Assert.AreEqual(1, ((FakeDamageable)inside.Damageable).HitCount);
            Assert.AreEqual(10f, ((FakeDamageable)inside.Damageable).TotalDamage, 0.001f);
        }

        [Test]
        public void ResolveSplash_SkipsTargetsOutsideRadius()
        {
            var provider = new FakeSplashTargetProvider();
            FakeCombatTarget outside = CreateTarget(new Vector3(10f, 0f, 0f));
            provider.Add(outside);
            var resolver = new AreaDamageResolver(provider);

            int hitCount = resolver.ResolveSplash(Vector3.zero, 2f, new DamageInfo(10f, null, Vector3.zero));

            Assert.AreEqual(0, hitCount);
            Assert.AreEqual(0, ((FakeDamageable)outside.Damageable).HitCount);
        }

        [Test]
        public void ResolveSplash_SkipsNonTargetableEnemies()
        {
            var provider = new FakeSplashTargetProvider();
            FakeCombatTarget resolved = CreateTarget(new Vector3(0.5f, 0f, 0f), isTargetable: false);
            provider.Add(resolved);
            var resolver = new AreaDamageResolver(provider);

            int hitCount = resolver.ResolveSplash(Vector3.zero, 2f, new DamageInfo(10f, null, Vector3.zero));

            Assert.AreEqual(0, hitCount);
        }

        [Test]
        public void ResolveSplash_DamagesEachTargetExactlyOnce_NoDuplicates()
        {
            var provider = new FakeSplashTargetProvider();
            FakeCombatTarget target = CreateTarget(Vector3.zero);
            provider.Add(target);
            var resolver = new AreaDamageResolver(provider);

            resolver.ResolveSplash(Vector3.zero, 3f, new DamageInfo(10f, null, Vector3.zero));

            Assert.AreEqual(1, ((FakeDamageable)target.Damageable).HitCount);
        }

        [Test]
        public void ResolveSplash_MultipleTargets_OnlyDamagesThoseInsideRadius()
        {
            var provider = new FakeSplashTargetProvider();
            FakeCombatTarget near = CreateTarget(new Vector3(1f, 0f, 0f));
            FakeCombatTarget mid = CreateTarget(new Vector3(2.4f, 0f, 0f));
            FakeCombatTarget far = CreateTarget(new Vector3(6f, 0f, 0f));
            provider.Add(near);
            provider.Add(mid);
            provider.Add(far);
            var resolver = new AreaDamageResolver(provider);

            int hitCount = resolver.ResolveSplash(Vector3.zero, 2.5f, new DamageInfo(15f, null, Vector3.zero));

            Assert.AreEqual(2, hitCount);
            Assert.AreEqual(1, ((FakeDamageable)near.Damageable).HitCount);
            Assert.AreEqual(1, ((FakeDamageable)mid.Damageable).HitCount);
            Assert.AreEqual(0, ((FakeDamageable)far.Damageable).HitCount);
        }

        [Test]
        public void ResolveSplash_ZeroRadius_HitsNothing()
        {
            var provider = new FakeSplashTargetProvider();
            provider.Add(CreateTarget(Vector3.zero));
            var resolver = new AreaDamageResolver(provider);

            int hitCount = resolver.ResolveSplash(Vector3.zero, 0f, new DamageInfo(10f, null, Vector3.zero));

            Assert.AreEqual(0, hitCount);
        }
    }
}
