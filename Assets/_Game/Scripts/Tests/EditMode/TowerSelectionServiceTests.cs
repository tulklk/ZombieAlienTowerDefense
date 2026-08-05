using System.Collections.Generic;
using AlienDefense.Towers;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class TowerSelectionServiceTests
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

        private TowerController CreateTower()
        {
            var go = new GameObject("TestTower");
            _spawnedObjects.Add(go);
            return go.AddComponent<TowerController>();
        }

        [Test]
        public void Select_SetsSelectedTower_AndFiresEvent()
        {
            var service = new TowerSelectionService();
            TowerController tower = CreateTower();
            TowerController received = null;
            service.SelectionChanged += t => received = t;

            bool result = service.Select(tower);

            Assert.IsTrue(result);
            Assert.AreEqual(tower, service.SelectedTower);
            Assert.AreEqual(tower, received);
        }

        [Test]
        public void Select_NullTower_Rejected()
        {
            var service = new TowerSelectionService();

            bool result = service.Select(null);

            Assert.IsFalse(result);
            Assert.IsNull(service.SelectedTower);
        }

        [Test]
        public void Select_SoldTower_Rejected()
        {
            var service = new TowerSelectionService();
            TowerController tower = CreateTower();
            tower.MarkSold();

            bool result = service.Select(tower);

            Assert.IsFalse(result);
            Assert.IsNull(service.SelectedTower);
        }

        [Test]
        public void Select_SameTowerTwice_DoesNotFireEventAgain()
        {
            var service = new TowerSelectionService();
            TowerController tower = CreateTower();
            service.Select(tower);

            int fireCount = 0;
            service.SelectionChanged += _ => fireCount++;

            service.Select(tower);

            Assert.AreEqual(0, fireCount);
        }

        [Test]
        public void Clear_ClearsSelectedTower_AndFiresEvent()
        {
            var service = new TowerSelectionService();
            TowerController tower = CreateTower();
            service.Select(tower);

            TowerController received = tower;
            service.SelectionChanged += t => received = t;

            service.Clear();

            Assert.IsNull(service.SelectedTower);
            Assert.IsNull(received);
        }

        [Test]
        public void Clear_WhenAlreadyEmpty_DoesNotFireEvent()
        {
            var service = new TowerSelectionService();

            int fireCount = 0;
            service.SelectionChanged += _ => fireCount++;

            service.Clear();

            Assert.AreEqual(0, fireCount);
        }

        [Test]
        public void ClearIfSelected_MatchingTower_Clears()
        {
            var service = new TowerSelectionService();
            TowerController tower = CreateTower();
            service.Select(tower);

            service.ClearIfSelected(tower);

            Assert.IsNull(service.SelectedTower);
        }

        [Test]
        public void ClearIfSelected_DifferentTower_DoesNotClear()
        {
            var service = new TowerSelectionService();
            TowerController selected = CreateTower();
            TowerController other = CreateTower();
            service.Select(selected);

            service.ClearIfSelected(other);

            Assert.AreEqual(selected, service.SelectedTower);
        }
    }
}
