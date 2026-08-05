using System.Collections.Generic;
using AlienDefense.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class EnemyRegistryTests
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

        private EnemyController CreateEnemy(string name)
        {
            var go = new GameObject(name);
            _spawnedObjects.Add(go);
            return go.AddComponent<EnemyController>();
        }

        [Test]
        public void Register_AddsEnemyAndIncrementsCount()
        {
            var registry = new EnemyRegistry();
            EnemyController enemy = CreateEnemy("EnemyA");

            bool result = registry.Register(enemy);

            Assert.IsTrue(result);
            Assert.AreEqual(1, registry.Count);
            Assert.AreEqual(enemy, registry.GetAt(0));
        }

        [Test]
        public void Register_RejectsDuplicate()
        {
            var registry = new EnemyRegistry();
            EnemyController enemy = CreateEnemy("EnemyA");
            registry.Register(enemy);

            bool result = registry.Register(enemy);

            Assert.IsFalse(result);
            Assert.AreEqual(1, registry.Count);
        }

        [Test]
        public void Unregister_RemovesEnemyAndDecrementsCount()
        {
            var registry = new EnemyRegistry();
            EnemyController enemy = CreateEnemy("EnemyA");
            registry.Register(enemy);

            bool result = registry.Unregister(enemy);

            Assert.IsTrue(result);
            Assert.AreEqual(0, registry.Count);
        }

        [Test]
        public void Unregister_UnknownEnemy_ReturnsFalseSafely()
        {
            var registry = new EnemyRegistry();
            EnemyController enemy = CreateEnemy("EnemyA");

            bool result = registry.Unregister(enemy);

            Assert.IsFalse(result);
        }

        [Test]
        public void Clear_RemovesAllEnemies()
        {
            var registry = new EnemyRegistry();
            registry.Register(CreateEnemy("EnemyA"));
            registry.Register(CreateEnemy("EnemyB"));

            registry.Clear();

            Assert.AreEqual(0, registry.Count);
        }
    }
}
