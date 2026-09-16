using System.IO;
using AlienDefense.Combat;
using AlienDefense.Save;
using AlienDefense.Settings;
using AlienDefense.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>The pause panel's own behaviour: the three setting toggles flip the real settings (and flip back),
    /// and the damage leaders fill from CombatStatsService, biggest first.</summary>
    public class PausePanelViewTests
    {
        private string _testDirectory;
        private readonly System.Collections.Generic.List<GameObject> _objects = new System.Collections.Generic.List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Application.temporaryCachePath, "PausePanelViewTests_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();

            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        [Test]
        public void SfxToggle_MutesAndRestoresSfxVolume()
        {
            SettingsService settings = CreateSettings();
            settings.SetSfxVolume(0.8f);
            (PausePanelView view, PanelParts parts) = CreateView();
            view.Bind(settings, null, null);

            parts.SfxButton.onClick.Invoke();
            Assert.AreEqual(0f, settings.Current.SfxVolume, "First tap mutes SFX.");

            parts.SfxButton.onClick.Invoke();
            Assert.Greater(settings.Current.SfxVolume, 0f, "Second tap brings SFX back.");
        }

        [Test]
        public void MusicToggle_MutesMusic_AndVibrationToggle_FlipsHaptics()
        {
            SettingsService settings = CreateSettings();
            settings.SetMusicVolume(0.6f);
            settings.SetHapticsEnabled(true);
            (PausePanelView view, PanelParts parts) = CreateView();
            view.Bind(settings, null, null);

            parts.MusicButton.onClick.Invoke();
            Assert.AreEqual(0f, settings.Current.MusicVolume);

            parts.VibrationButton.onClick.Invoke();
            Assert.IsFalse(settings.Current.HapticsEnabled);

            parts.VibrationButton.onClick.Invoke();
            Assert.IsTrue(settings.Current.HapticsEnabled);
        }

        [Test]
        public void DamageLeaders_ShowHighestFirst_AndHideTheEmptyLabel()
        {
            var stats = new CombatStatsService();
            (PausePanelView view, PanelParts parts) = CreateView();

            try
            {
                var weak = new GameObject("WeakSource");
                var strong = new GameObject("StrongSource");
                _objects.Add(weak);
                _objects.Add(strong);

                Report(weak, 10f);
                Report(strong, 90f);

                view.Bind(null, null, stats);
                view.Refresh();

                Assert.AreEqual("StrongSource", parts.Rows[0].Name.text);
                Assert.AreEqual("90", parts.Rows[0].Value.text);
                Assert.AreEqual(1f, parts.Rows[0].Bar.fillAmount, 0.001f, "The top row fills the bar.");
                Assert.AreEqual("WeakSource", parts.Rows[1].Name.text);
                Assert.IsFalse(parts.EmptyLabel.activeSelf, "The empty label hides once something dealt damage.");
            }
            finally
            {
                stats.Dispose();
            }
        }

        [Test]
        public void DamageLeaders_WithoutDamage_ShowTheEmptyLabel()
        {
            var stats = new CombatStatsService();
            (PausePanelView view, PanelParts parts) = CreateView();

            try
            {
                view.Bind(null, null, stats);
                view.Refresh();

                Assert.IsTrue(parts.EmptyLabel.activeSelf);
                Assert.IsFalse(parts.Rows[0].Root.activeSelf);
            }
            finally
            {
                stats.Dispose();
            }
        }

        /// <summary>CombatStatsService listens to EnemyHealth's static damage event; raising it through a real
        /// EnemyHealth keeps the test honest about the path the game uses.</summary>
        private void Report(GameObject source, float amount)
        {
            var enemyObject = new GameObject("Enemy");
            _objects.Add(enemyObject);
            var health = enemyObject.AddComponent<AlienDefense.Enemies.EnemyHealth>();
            health.Initialize(10000f);
            health.TryApplyDamage(new DamageInfo(amount, source, Vector3.zero));
        }

        private SettingsService CreateSettings()
        {
            var repository = new SaveFileRepository(_testDirectory);
            var saveService = new SaveService(repository);
            PlayerProfileSaveData data = saveService.LoadOrCreateDefault(null, "level_01");
            return new SettingsService(new PlayerProfileService(saveService, data));
        }

        private struct RowParts
        {
            public GameObject Root;
            public TMP_Text Name;
            public TMP_Text Value;
            public Image Bar;
        }

        private struct PanelParts
        {
            public Button SfxButton;
            public Button MusicButton;
            public Button VibrationButton;
            public GameObject EmptyLabel;
            public RowParts[] Rows;
        }

        private (PausePanelView, PanelParts) CreateView()
        {
            var root = new GameObject("PausePanel");
            _objects.Add(root);
            var view = root.AddComponent<PausePanelView>();
            var so = new UnityEditor.SerializedObject(view);

            var parts = new PanelParts { Rows = new RowParts[2] };
            parts.SfxButton = CreateToggle(root, so, "_sfxToggle", "Sfx");
            parts.MusicButton = CreateToggle(root, so, "_musicToggle", "Music");
            parts.VibrationButton = CreateToggle(root, so, "_vibrationToggle", "Vibration");

            var empty = new GameObject("Empty");
            empty.transform.SetParent(root.transform);
            parts.EmptyLabel = empty;
            so.FindProperty("_noStatisticsLabel").objectReferenceValue = empty;

            UnityEditor.SerializedProperty rows = so.FindProperty("_damageRows");
            rows.arraySize = parts.Rows.Length;
            for (int i = 0; i < parts.Rows.Length; i++)
            {
                var rowObject = new GameObject("Row" + i);
                rowObject.transform.SetParent(root.transform);
                var nameText = new GameObject("Name").AddComponent<TextMeshProUGUI>();
                nameText.transform.SetParent(rowObject.transform);
                var valueText = new GameObject("Value").AddComponent<TextMeshProUGUI>();
                valueText.transform.SetParent(rowObject.transform);
                var bar = new GameObject("Bar").AddComponent<Image>();
                bar.transform.SetParent(rowObject.transform);

                UnityEditor.SerializedProperty row = rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("Root").objectReferenceValue = rowObject;
                row.FindPropertyRelative("NameText").objectReferenceValue = nameText;
                row.FindPropertyRelative("ValueText").objectReferenceValue = valueText;
                row.FindPropertyRelative("Bar").objectReferenceValue = bar;
                parts.Rows[i] = new RowParts { Root = rowObject, Name = nameText, Value = valueText, Bar = bar };
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return (view, parts);
        }

        private static Button CreateToggle(GameObject root, UnityEditor.SerializedObject so, string property, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform);
            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            UnityEditor.SerializedProperty toggle = so.FindProperty(property);
            toggle.FindPropertyRelative("Button").objectReferenceValue = button;
            toggle.FindPropertyRelative("Icon").objectReferenceValue = image;
            return button;
        }
    }
}
