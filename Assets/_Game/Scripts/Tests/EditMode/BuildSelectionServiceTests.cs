using AlienDefense.Building;
using AlienDefense.Towers;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class BuildSelectionServiceTests
    {
        private TowerDefinition _definitionA;
        private TowerDefinition _definitionB;

        [SetUp]
        public void SetUp()
        {
            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            _definitionA = ScriptableObject.CreateInstance<TowerDefinition>();
            _definitionB = ScriptableObject.CreateInstance<TowerDefinition>();
            Debug.unityLogger.logEnabled = previousLogEnabled;
        }

        [TearDown]
        public void TearDown()
        {
            if (_definitionA != null)
            {
                Object.DestroyImmediate(_definitionA);
            }

            if (_definitionB != null)
            {
                Object.DestroyImmediate(_definitionB);
            }
        }

        [Test]
        public void SelectTower_SetsSelectedDefinition_AndFiresEvent()
        {
            var service = new BuildSelectionService();
            TowerDefinition received = null;
            service.SelectedTowerChanged += d => received = d;

            bool result = service.SelectTower(_definitionA);

            Assert.IsTrue(result);
            Assert.AreEqual(_definitionA, service.SelectedTowerDefinition);
            Assert.AreEqual(_definitionA, received);
        }

        [Test]
        public void SelectTower_NullDefinition_Rejected()
        {
            var service = new BuildSelectionService();

            bool result = service.SelectTower(null);

            Assert.IsFalse(result);
            Assert.IsNull(service.SelectedTowerDefinition);
        }

        [Test]
        public void SelectTower_SameDefinitionTwice_DoesNotFireEventAgain()
        {
            var service = new BuildSelectionService();
            service.SelectTower(_definitionA);

            int fireCount = 0;
            service.SelectedTowerChanged += _ => fireCount++;

            service.SelectTower(_definitionA);

            Assert.AreEqual(0, fireCount);
        }

        [Test]
        public void SelectTower_DifferentDefinition_ReplacesSelection()
        {
            var service = new BuildSelectionService();
            service.SelectTower(_definitionA);

            service.SelectTower(_definitionB);

            Assert.AreEqual(_definitionB, service.SelectedTowerDefinition);
        }

        [Test]
        public void ClearSelection_ClearsSelectedDefinition_AndFiresEvent()
        {
            var service = new BuildSelectionService();
            service.SelectTower(_definitionA);

            TowerDefinition received = _definitionA;
            service.SelectedTowerChanged += d => received = d;

            service.ClearSelection();

            Assert.IsNull(service.SelectedTowerDefinition);
            Assert.IsNull(received);
        }

        [Test]
        public void ClearSelection_WhenAlreadyEmpty_DoesNotFireEvent()
        {
            var service = new BuildSelectionService();

            int fireCount = 0;
            service.SelectedTowerChanged += _ => fireCount++;

            service.ClearSelection();

            Assert.AreEqual(0, fireCount);
        }
    }
}
