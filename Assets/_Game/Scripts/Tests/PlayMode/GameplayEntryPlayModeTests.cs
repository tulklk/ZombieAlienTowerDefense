using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Core;
using AlienDefense.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    /// <summary>Covers LevelCompositionRoot.ResolveLevelDefinition() (invoked via reflection, bypassing the heavy
    /// BuildLevel()/Start() path since the GameObject stays inactive): LevelLaunchContext selection resolves via
    /// LevelCatalog; an unresolved id fails loudly rather than falling back silently; with no ApplicationServices
    /// selection, the Editor-only Development Level Definition fallback is used with a warning; with neither, it
    /// returns null.</summary>
    public class GameplayEntryPlayModeTests
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

        private static object InvokePrivateMethodWithResult(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"Method '{methodName}' not found on {target.GetType().Name}.");
            return method.Invoke(target, null);
        }

        private static T CreateSilently<T>(System.Func<T> factory)
        {
            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            T result = factory();
            Debug.unityLogger.logEnabled = previousLogEnabled;
            return result;
        }

        private LevelDefinition CreateLevelDefinition(string levelId)
        {
            var definition = CreateSilently(() => ScriptableObject.CreateInstance<LevelDefinition>());
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_levelId", levelId);
            return definition;
        }

        private LevelCatalog CreateCatalog(string levelId, LevelDefinition definition)
        {
            var entry = new LevelCatalogEntry();
            SetPrivateField(entry, "_levelDefinition", definition);
            SetPrivateField(entry, "_sceneName", "__NonExistentTestScene__");

            var catalog = CreateSilently(() => ScriptableObject.CreateInstance<LevelCatalog>());
            _scriptableObjects.Add(catalog);
            SetPrivateField(catalog, "_entries", new[] { entry });
            return catalog;
        }

        private LevelCompositionRoot CreateInactiveCompositionRoot()
        {
            var go = new GameObject("TestLevelCompositionRoot");
            go.SetActive(false);
            _spawnedObjects.Add(go);
            return go.AddComponent<LevelCompositionRoot>();
        }

        [UnityTest]
        public IEnumerator ResolveLevelDefinition_WithMatchingCatalogEntry_ReturnsIt()
        {
            LevelDefinition definition = CreateLevelDefinition("level_01");
            LevelCatalog catalog = CreateCatalog("level_01", definition);
            var launchContext = new LevelLaunchContext();
            launchContext.SetSelectedLevel("level_01");
            var services = new ApplicationServices(null, launchContext, catalog, null, null);

            LevelCompositionRoot root = CreateInactiveCompositionRoot();
            root.ReceiveApplicationServices(services);

            var resolved = (LevelDefinition)InvokePrivateMethodWithResult(root, "ResolveLevelDefinition");

            Assert.AreSame(definition, resolved);
            Assert.AreEqual("level_01", GetPrivateField(root, "_resolvedLevelId"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ResolveLevelDefinition_WithUnresolvableId_ReturnsNull_AndLogsError_NotSilentFallback()
        {
            LevelDefinition definition = CreateLevelDefinition("level_01");
            LevelCatalog catalog = CreateCatalog("level_01", definition);
            var launchContext = new LevelLaunchContext();
            launchContext.SetSelectedLevel("level_99_does_not_exist");
            var services = new ApplicationServices(null, launchContext, catalog, null, null);

            LevelCompositionRoot root = CreateInactiveCompositionRoot();
            root.ReceiveApplicationServices(services);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("could not resolve"));
            var resolved = InvokePrivateMethodWithResult(root, "ResolveLevelDefinition");

            Assert.IsNull(resolved);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ResolveLevelDefinition_NoSelection_FallsBackToDevelopmentDefinition_WithWarning()
        {
            LevelDefinition devDefinition = CreateLevelDefinition("dev_level");
            LevelCompositionRoot root = CreateInactiveCompositionRoot();
            SetPrivateField(root, "_developmentLevelDefinition", devDefinition);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Development Level Definition fallback"));
            var resolved = InvokePrivateMethodWithResult(root, "ResolveLevelDefinition");

            Assert.AreSame(devDefinition, resolved);
            Assert.AreEqual("dev_level", GetPrivateField(root, "_resolvedLevelId"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ResolveLevelDefinition_NoSelectionAndNoDevelopmentFallback_ReturnsNull()
        {
            LevelCompositionRoot root = CreateInactiveCompositionRoot();

            var resolved = InvokePrivateMethodWithResult(root, "ResolveLevelDefinition");

            Assert.IsNull(resolved);
            yield return null;
        }
    }
}
