using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Vfx;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    public class VfxLifecyclePlayModeTests
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

        private static T CreateSilently<T>(System.Func<T> factory)
        {
            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            T result = factory();
            Debug.unityLogger.logEnabled = previousLogEnabled;
            return result;
        }

        private VfxDefinition CreateDefinition(float lifetime, int maxSize)
        {
            var templateObject = new GameObject("TestPooledVfxTemplate");
            templateObject.SetActive(false);
            _spawnedObjects.Add(templateObject);
            var template = templateObject.AddComponent<PooledVfx>();

            var definition = CreateSilently(() => ScriptableObject.CreateInstance<VfxDefinition>());
            _scriptableObjects.Add(definition);

            SetPrivateField(definition, "_prefab", template);
            SetPrivateField(definition, "_lifetime", lifetime);
            SetPrivateField(definition, "_poolPrewarmCount", 0);
            SetPrivateField(definition, "_poolDefaultCapacity", 1);
            SetPrivateField(definition, "_poolMaximumSize", maxSize);
            return definition;
        }

        private VfxService CreateService()
        {
            var runtimeParent = new GameObject("VfxRuntimeParent");
            _spawnedObjects.Add(runtimeParent);
            var poolRegistry = new VfxPoolRegistry(runtimeParent.transform);
            return new VfxService(poolRegistry);
        }

        [UnityTest]
        public IEnumerator Play_ActivatesInstance_ThenAutoReturnsAfterLifetime()
        {
            VfxDefinition definition = CreateDefinition(0.05f, 2);
            VfxService service = CreateService();

            service.Play(definition, Vector3.zero, Quaternion.identity);

            yield return null;

            GameObject spawned = GameObject.Find("TestPooledVfxTemplate(Clone)");
            Assert.IsNotNull(spawned, "Expected a spawned PooledVfx instance.");
            Assert.IsTrue(spawned.activeSelf);

            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            Assert.IsFalse(spawned.activeSelf, "PooledVfx should have auto-returned to the pool after its lifetime.");
        }

        [UnityTest]
        public IEnumerator Play_ReusesReturnedInstance_ForSubsequentCalls()
        {
            VfxDefinition definition = CreateDefinition(0.05f, 1);
            VfxService service = CreateService();

            service.Play(definition, Vector3.zero, Quaternion.identity);
            yield return null;

            GameObject firstSpawn = GameObject.Find("TestPooledVfxTemplate(Clone)");
            Assert.IsNotNull(firstSpawn, "Expected a spawned PooledVfx instance while still active.");

            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            Assert.IsFalse(firstSpawn.activeSelf, "Instance should have auto-returned to the pool by now.");

            service.Play(definition, new Vector3(1f, 2f, 3f), Quaternion.identity);
            yield return null;

            Assert.IsTrue(firstSpawn.activeSelf, "Expected the same pooled instance (max size 1) to be reused.");
            Assert.AreEqual(new Vector3(1f, 2f, 3f), firstSpawn.transform.position);
        }

        [Test]
        public void Play_WithNullDefinition_DoesNotThrow()
        {
            VfxService service = CreateService();

            Assert.DoesNotThrow(() => service.Play(null, Vector3.zero, Quaternion.identity));
        }
    }
}
