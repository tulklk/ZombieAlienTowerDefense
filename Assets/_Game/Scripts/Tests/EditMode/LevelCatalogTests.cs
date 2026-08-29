using System.Reflection;
using AlienDefense.Core;
using AlienDefense.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.EditMode
{
    public class LevelCatalogTests
    {
        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static T CreateSilently<T>(System.Func<T> factory)
        {
            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            T result = factory();
            Debug.unityLogger.logEnabled = previousLogEnabled;
            return result;
        }

        private static LevelDefinition CreateLevelDefinition(string levelId)
        {
            var definition = CreateSilently(() => ScriptableObject.CreateInstance<LevelDefinition>());
            SetPrivateField(definition, "_levelId", levelId);
            return definition;
        }

        private static LevelCatalogEntry CreateEntry(string levelId, string sceneName)
        {
            var entry = new LevelCatalogEntry();
            SetPrivateField(entry, "_levelDefinition", CreateLevelDefinition(levelId));
            SetPrivateField(entry, "_sceneName", sceneName);
            return entry;
        }

        private static LevelCatalog CreateCatalog(params LevelCatalogEntry[] entries)
        {
            var catalog = CreateSilently(() => ScriptableObject.CreateInstance<LevelCatalog>());
            SetPrivateField(catalog, "_entries", entries);
            return catalog;
        }

        [Test]
        public void TryResolve_KnownId_ReturnsMatchingEntry()
        {
            LevelCatalog catalog = CreateCatalog(CreateEntry("level_01", "Level_01"), CreateEntry("level_02", "Level_02"));

            bool found = catalog.TryResolve("level_02", out LevelCatalogEntry entry);

            Assert.IsTrue(found);
            Assert.AreEqual("Level_02", entry.SceneName);
        }

        [Test]
        public void TryResolve_UnknownId_ReturnsFalse()
        {
            LevelCatalog catalog = CreateCatalog(CreateEntry("level_01", "Level_01"));

            bool found = catalog.TryResolve("does_not_exist", out LevelCatalogEntry entry);

            Assert.IsFalse(found);
            Assert.IsNull(entry);
        }

        [Test]
        public void TryResolve_EmptyId_ReturnsFalse()
        {
            LevelCatalog catalog = CreateCatalog(CreateEntry("level_01", "Level_01"));

            bool found = catalog.TryResolve("", out LevelCatalogEntry entry);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryGetNext_MiddleEntry_ReturnsFollowingEntry()
        {
            LevelCatalog catalog = CreateCatalog(
                CreateEntry("level_01", "Level_01"),
                CreateEntry("level_02", "Level_02"),
                CreateEntry("level_03", "Level_03"));

            bool found = catalog.TryGetNext("level_02", out LevelCatalogEntry next);

            Assert.IsTrue(found);
            Assert.AreEqual("level_03", next.LevelId);
        }

        [Test]
        public void TryGetNext_LastEntry_ReturnsFalse()
        {
            LevelCatalog catalog = CreateCatalog(CreateEntry("level_01", "Level_01"), CreateEntry("level_02", "Level_02"));

            bool found = catalog.TryGetNext("level_02", out LevelCatalogEntry next);

            Assert.IsFalse(found);
            Assert.IsNull(next);
        }

        [Test]
        public void TryGetNext_UnknownCurrentId_ReturnsFalse()
        {
            LevelCatalog catalog = CreateCatalog(CreateEntry("level_01", "Level_01"));

            bool found = catalog.TryGetNext("not_in_catalog", out LevelCatalogEntry next);

            Assert.IsFalse(found);
        }

        [Test]
        public void Count_ReflectsEntryArrayLength()
        {
            LevelCatalog catalog = CreateCatalog(CreateEntry("level_01", "Level_01"), CreateEntry("level_02", "Level_02"));

            Assert.AreEqual(2, catalog.Count);
        }

        [Test]
        public void OnValidate_DuplicateId_LogsError()
        {
            LevelCatalog catalog = CreateCatalog(CreateEntry("level_01", "Level_01"), CreateEntry("level_01", "Level_02"));

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("duplicate Level Id"));
            InvokeOnValidate(catalog);
        }

        [Test]
        public void OnValidate_EmptyCatalog_LogsError()
        {
            LevelCatalog catalog = CreateCatalog();

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("has no entries"));
            InvokeOnValidate(catalog);
        }

        private static void InvokeOnValidate(LevelCatalog catalog)
        {
            MethodInfo method = typeof(LevelCatalog).GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "OnValidate method not found on LevelCatalog.");
            method.Invoke(catalog, null);
        }
    }
}
