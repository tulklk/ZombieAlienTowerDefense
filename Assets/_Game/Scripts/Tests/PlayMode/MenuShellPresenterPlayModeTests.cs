using System.Collections;
using System.Reflection;
using AlienDefense.UI.MainMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    /// <summary>Verifies MenuShellPresenter panel swapping without scene loads.</summary>
    public class MenuShellPresenterPlayModeTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawnedObjects = new System.Collections.Generic.List<GameObject>();

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
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        [UnityTest]
        public IEnumerator SwitchTab_ActivatesTargetPanel_AndHidesOthers()
        {
            var root = new GameObject("MenuShellTestRoot");
            _spawnedObjects.Add(root);

            var playPanel = new GameObject("PlayPanel");
            playPanel.transform.SetParent(root.transform, false);
            playPanel.SetActive(true);
            _spawnedObjects.Add(playPanel);

            var shopPanel = new GameObject("ShopPanel");
            shopPanel.transform.SetParent(root.transform, false);
            shopPanel.SetActive(false);
            _spawnedObjects.Add(shopPanel);

            var upgradePanel = new GameObject("UpgradePanel");
            upgradePanel.transform.SetParent(root.transform, false);
            upgradePanel.SetActive(false);
            _spawnedObjects.Add(upgradePanel);

            var basePanel = new GameObject("BasePanel");
            basePanel.transform.SetParent(root.transform, false);
            basePanel.SetActive(false);
            _spawnedObjects.Add(basePanel);

            var defensePanel = new GameObject("DefensePanel");
            defensePanel.transform.SetParent(root.transform, false);
            defensePanel.SetActive(false);
            _spawnedObjects.Add(defensePanel);

            var previewRoot = new GameObject("LevelPreviewRoot");
            previewRoot.transform.SetParent(root.transform, false);
            previewRoot.SetActive(true);
            _spawnedObjects.Add(previewRoot);

            var shellView = root.AddComponent<MenuShellView>();
            SetPrivateField(shellView, "_playPanel", playPanel);
            SetPrivateField(shellView, "_shopPanel", shopPanel);
            SetPrivateField(shellView, "_upgradePanel", upgradePanel);
            SetPrivateField(shellView, "_basePanel", basePanel);
            SetPrivateField(shellView, "_defensePanel", defensePanel);
            SetPrivateField(shellView, "_levelPreviewRoot", previewRoot);

            var presenter = root.AddComponent<MenuShellPresenter>();
            SetPrivateField(presenter, "_view", shellView);

            presenter.SwitchTab(MenuTab.Shop);
            yield return null;

            Assert.IsFalse(playPanel.activeSelf, "Play panel should be hidden after switching to Shop.");
            Assert.IsTrue(shopPanel.activeSelf, "Shop panel should be active.");
            Assert.IsFalse(previewRoot.activeSelf, "Level preview should be hidden outside Play tab.");

            presenter.SwitchTab(MenuTab.Play);
            yield return null;

            Assert.IsTrue(playPanel.activeSelf, "Play panel should be active again.");
            Assert.IsFalse(shopPanel.activeSelf, "Shop panel should be hidden.");
            Assert.IsTrue(previewRoot.activeSelf, "Level preview should be visible on Play tab.");
        }
    }
}
