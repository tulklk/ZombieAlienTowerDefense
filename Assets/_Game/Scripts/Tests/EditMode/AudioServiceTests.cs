using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Audio;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class AudioServiceTests
    {
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();
        private readonly List<Object> _createdAssets = new List<Object>();

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

            foreach (Object asset in _createdAssets)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }

            _createdAssets.Clear();
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

        private AudioService CreateService(int sfxSourceCount)
        {
            var go = new GameObject("TestAudioService");
            _spawnedObjects.Add(go);
            var service = go.AddComponent<AudioService>();

            var musicSource = go.AddComponent<AudioSource>();

            var sfxSources = new AudioSource[sfxSourceCount];
            for (int i = 0; i < sfxSourceCount; i++)
            {
                var sfxGo = new GameObject($"Sfx_{i}");
                sfxGo.transform.SetParent(go.transform);
                _spawnedObjects.Add(sfxGo);
                sfxSources[i] = sfxGo.AddComponent<AudioSource>();
            }

            SetPrivateField(service, "_musicSource", musicSource);
            SetPrivateField(service, "_sfxSources", sfxSources);

            return service;
        }

        private AudioClip CreateTestClip()
        {
            AudioClip clip = AudioClip.Create("TestClip", 1, 1, 44100, false);
            _createdAssets.Add(clip);
            return clip;
        }

        [Test]
        public void PlaySfx_CyclesThroughSourcesRoundRobin()
        {
            AudioService service = CreateService(3);
            AudioClip clip = CreateTestClip();

            service.PlaySfx(clip);
            Assert.AreEqual(1, (int)GetPrivateField(service, "_nextSfxIndex"));

            service.PlaySfx(clip);
            Assert.AreEqual(2, (int)GetPrivateField(service, "_nextSfxIndex"));

            service.PlaySfx(clip);
            Assert.AreEqual(0, (int)GetPrivateField(service, "_nextSfxIndex"), "Index should wrap back to 0 after reaching the pool size.");
        }

        [Test]
        public void PlaySfx_WithNullClip_DoesNotAdvanceIndex()
        {
            AudioService service = CreateService(3);

            service.PlaySfx(null);

            Assert.AreEqual(0, (int)GetPrivateField(service, "_nextSfxIndex"));
        }

        [Test]
        public void SetSfxVolume_ClampsToZeroOneRange()
        {
            AudioService service = CreateService(1);

            service.SetSfxVolume(5f);
            Assert.AreEqual(1f, service.SfxVolume);

            service.SetSfxVolume(-2f);
            Assert.AreEqual(0f, service.SfxVolume);
        }

        [Test]
        public void SetMusicVolume_ClampsAndAppliesToMusicSource()
        {
            AudioService service = CreateService(1);

            service.SetMusicVolume(2f);

            Assert.AreEqual(1f, service.MusicVolume);
            var musicSource = (AudioSource)GetPrivateField(service, "_musicSource");
            Assert.AreEqual(1f, musicSource.volume);
        }
    }
}
