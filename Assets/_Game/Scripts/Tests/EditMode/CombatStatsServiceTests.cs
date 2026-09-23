using System.Collections.Generic;
using AlienDefense.Combat;
using AlienDefense.Enemies;
using AlienDefense.Progression;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>The match's damage tracking behind the victory statistics: only damage that really came off enemy
    /// health counts, shares never divide by zero, and a new run starts from nothing.</summary>
    public class CombatStatsServiceTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private CombatStatsService _stats;

        [SetUp]
        public void SetUp()
        {
            _stats = new CombatStatsService();
            _stats.BeginRun();
        }

        [TearDown]
        public void TearDown()
        {
            _stats.Dispose();
            foreach (GameObject go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void OnlyOneSource_GetsTheWholeShare()
        {
            _stats.RegisterDamage(Source("Blaster"), 1000f);

            IReadOnlyList<CombatStatsService.Contributor> leaders = _stats.GetLeaders(0);

            Assert.AreEqual(1, leaders.Count, "Towers that dealt nothing get no row.");
            Assert.AreEqual(1000f, _stats.TotalDamage);
            Assert.AreEqual(1f, DamageResultEntry.ShareOf(leaders[0].Damage, _stats.TotalDamage));
        }

        [Test]
        public void Shares_AreProportional_AndSortedBiggestFirst()
        {
            _stats.RegisterDamage(Source("Frost"), 200f);
            _stats.RegisterDamage(Source("Blaster"), 500f);
            _stats.RegisterDamage(Source("Mortar"), 300f);

            IReadOnlyList<CombatStatsService.Contributor> leaders = _stats.GetLeaders(0);

            Assert.AreEqual("Blaster", leaders[0].Name);
            Assert.AreEqual("Mortar", leaders[1].Name);
            Assert.AreEqual("Frost", leaders[2].Name);
            Assert.AreEqual(0.5f, DamageResultEntry.ShareOf(leaders[0].Damage, _stats.TotalDamage), 0.0001f);
            Assert.AreEqual(0.3f, DamageResultEntry.ShareOf(leaders[1].Damage, _stats.TotalDamage), 0.0001f);
            Assert.AreEqual(0.2f, DamageResultEntry.ShareOf(leaders[2].Damage, _stats.TotalDamage), 0.0001f);
        }

        [Test]
        public void Overkill_CountsOnlyTheHealthThatWasLeft()
        {
            GameObject blaster = Source("Blaster");
            GameObject enemy = Source("Enemy");
            var health = enemy.AddComponent<EnemyHealth>();
            health.Initialize(10f);

            health.TryApplyDamage(new DamageInfo(1000f, blaster, Vector3.zero, DamageType.True));

            Assert.AreEqual(10f, _stats.TotalDamage, 0.001f, "A 1000 hit on a 10 HP enemy counts as 10.");
        }

        [Test]
        public void ZeroTotal_ShareIsZero_NotNaN()
        {
            Assert.AreEqual(0f, DamageResultEntry.ShareOf(0f, 0f));
            Assert.AreEqual(0f, DamageResultEntry.ShareOf(50f, 0f));
        }

        [Test]
        public void BeginRun_ClearsEverything()
        {
            _stats.RegisterDamage(Source("Blaster"), 750f);

            _stats.BeginRun();

            Assert.AreEqual(0f, _stats.TotalDamage);
            Assert.AreEqual(0, _stats.GetLeaders(0).Count);
        }

        private GameObject Source(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }
    }
}
