using System.Collections.Generic;
using AlienDefense.Combat;
using AlienDefense.Progression;
using AlienDefense.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>The victory panel's own rules: what it prints for a result, that percentages survive a zero-damage
    /// level, and that Next can only fire once.</summary>
    public class VictoryPanelViewTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

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
        }

        [Test]
        public void Show_FillsLeadersAndRewards_AndHidesUnusedSlots()
        {
            (VictoryPanelView view, Parts parts) = CreateView();

            view.Show(Result(
                rewards: new List<VictoryReward> { new VictoryReward(VictoryRewardType.Coins, 1200) },
                sources: new List<CombatStatsService.Contributor>
                {
                    new CombatStatsService.Contributor("Mortar Tower", null, 1600f),
                    new CombatStatsService.Contributor("Blaster Tower", null, 400f),
                },
                totalDamage: 2000f));

            Assert.AreEqual("CAMPAIGN LEVEL 1", parts.LevelText.text);
            Assert.AreEqual("Perfect Clear", parts.ResultText.text);
            Assert.AreEqual("Mortar Tower", parts.Leaders[0].Name.text);
            Assert.AreEqual("1.6K", parts.Leaders[0].Value.text, "Damage uses the shared compact format.");
            Assert.AreEqual("Blaster Tower", parts.Leaders[1].Name.text);
            Assert.IsTrue(parts.Rewards[0].Root.activeSelf);
            Assert.IsFalse(parts.Rewards[1].Root.activeSelf, "Only the granted rewards take a slot.");
            Assert.IsFalse(parts.NoRewardLabel.activeSelf);
        }

        [Test]
        public void Show_WithoutRewardsOrDamage_ShowsTheEmptyLabels()
        {
            (VictoryPanelView view, Parts parts) = CreateView();

            view.Show(Result(new List<VictoryReward>(), new List<CombatStatsService.Contributor>(), 0f));

            Assert.IsTrue(parts.NoRewardLabel.activeSelf);
            Assert.IsTrue(parts.NoDamageLabel.activeSelf);
            Assert.IsFalse(parts.Leaders[0].Root.activeSelf);
            Assert.IsFalse(parts.StatisticsButton.gameObject.activeSelf, "Nothing to show statistics for.");
        }

        [Test]
        public void Statistics_SharePerSource_AndSurviveZeroTotal()
        {
            (VictoryPanelView view, Parts parts) = CreateView();

            view.Show(Result(
                new List<VictoryReward>(),
                new List<CombatStatsService.Contributor>
                {
                    new CombatStatsService.Contributor("Mortar Tower", null, 750f),
                    new CombatStatsService.Contributor("Rocket", null, 250f),
                },
                totalDamage: 1000f));

            Assert.AreEqual("75%", parts.Stats[0].Percent.text);
            Assert.AreEqual(0.75f, parts.Stats[0].Bar.fillAmount, 0.001f);
            Assert.AreEqual("25%", parts.Stats[1].Percent.text);
            Assert.AreEqual("All Damage: 1K", parts.StatsTotal.text);

            // A level where nothing landed must not divide by zero.
            view.Show(Result(
                new List<VictoryReward>(),
                new List<CombatStatsService.Contributor> { new CombatStatsService.Contributor("Rocket", null, 0f) },
                totalDamage: 0f));

            Assert.AreEqual("0%", parts.Stats[0].Percent.text);
            Assert.AreEqual(0f, parts.Stats[0].Bar.fillAmount, 0.001f);
        }

        [Test]
        public void Next_FiresOnce_AndDisablesItself()
        {
            (VictoryPanelView view, Parts parts) = CreateView();
            int clicks = 0;
            view.NextClicked += () => clicks++;

            view.Show(Result(new List<VictoryReward>(), new List<CombatStatsService.Contributor>(), 0f));

            parts.NextButton.onClick.Invoke();
            parts.NextButton.onClick.Invoke(); // a second tap before the scene swaps must not count

            Assert.AreEqual(1, clicks, "Only the first Next is forwarded.");
            Assert.IsFalse(parts.NextButton.interactable);
        }

        private static LevelVictoryResult Result(List<VictoryReward> rewards,
            List<CombatStatsService.Contributor> sources, float totalDamage)
        {
            return new LevelVictoryResult("level_01", "CAMPAIGN LEVEL 1", 3, isPerfectClear: true,
                remainingBaseHealth: 20, maxBaseHealth: 20, rewards, sources, totalDamage, hasNextLevel: true);
        }

        private struct LeaderParts
        {
            public GameObject Root;
            public TMP_Text Name;
            public TMP_Text Value;
            public Image Bar;
        }

        private struct StatParts
        {
            public GameObject Root;
            public TMP_Text Percent;
            public Image Bar;
        }

        private struct RewardParts
        {
            public GameObject Root;
            public TMP_Text Amount;
        }

        private struct Parts
        {
            public TMP_Text LevelText;
            public TMP_Text ResultText;
            public TMP_Text StatsTotal;
            public GameObject NoRewardLabel;
            public GameObject NoDamageLabel;
            public Button NextButton;
            public Button StatisticsButton;
            public LeaderParts[] Leaders;
            public StatParts[] Stats;
            public RewardParts[] Rewards;
        }

        private (VictoryPanelView, Parts) CreateView()
        {
            var root = new GameObject("VictoryPanel");
            _objects.Add(root);
            var view = root.AddComponent<VictoryPanelView>();
            var so = new SerializedObject(view);

            var parts = new Parts
            {
                Leaders = new LeaderParts[2],
                Stats = new StatParts[2],
                Rewards = new RewardParts[2],
                LevelText = NewText(root, "LevelText"),
                ResultText = NewText(root, "ResultText"),
                StatsTotal = NewText(root, "StatsTotal"),
                NoRewardLabel = NewChild(root, "NoReward"),
                NoDamageLabel = NewChild(root, "NoDamage"),
                NextButton = NewButton(root, "Next"),
                StatisticsButton = NewButton(root, "Stats"),
            };

            so.FindProperty("_levelText").objectReferenceValue = parts.LevelText;
            so.FindProperty("_resultText").objectReferenceValue = parts.ResultText;
            so.FindProperty("_statsTotalText").objectReferenceValue = parts.StatsTotal;
            so.FindProperty("_noRewardLabel").objectReferenceValue = parts.NoRewardLabel;
            so.FindProperty("_noDamageLabel").objectReferenceValue = parts.NoDamageLabel;
            so.FindProperty("_nextButton").objectReferenceValue = parts.NextButton;
            so.FindProperty("_statisticsButton").objectReferenceValue = parts.StatisticsButton;

            SerializedProperty leaders = so.FindProperty("_leaderRows");
            leaders.arraySize = parts.Leaders.Length;
            for (int i = 0; i < parts.Leaders.Length; i++)
            {
                GameObject row = NewChild(root, "Leader" + i);
                TMP_Text nameText = NewText(row, "Name");
                TMP_Text valueText = NewText(row, "Value");
                Image bar = NewImage(row, "Bar");
                SerializedProperty element = leaders.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Root").objectReferenceValue = row;
                element.FindPropertyRelative("NameText").objectReferenceValue = nameText;
                element.FindPropertyRelative("ValueText").objectReferenceValue = valueText;
                element.FindPropertyRelative("Bar").objectReferenceValue = bar;
                parts.Leaders[i] = new LeaderParts { Root = row, Name = nameText, Value = valueText, Bar = bar };
            }

            SerializedProperty stats = so.FindProperty("_statRows");
            stats.arraySize = parts.Stats.Length;
            for (int i = 0; i < parts.Stats.Length; i++)
            {
                GameObject row = NewChild(root, "StatRow" + i);
                TMP_Text percent = NewText(row, "Percent");
                Image bar = NewImage(row, "Bar");
                SerializedProperty element = stats.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Root").objectReferenceValue = row;
                element.FindPropertyRelative("NameText").objectReferenceValue = NewText(row, "Name");
                element.FindPropertyRelative("ValueText").objectReferenceValue = NewText(row, "Value");
                element.FindPropertyRelative("PercentText").objectReferenceValue = percent;
                element.FindPropertyRelative("Bar").objectReferenceValue = bar;
                parts.Stats[i] = new StatParts { Root = row, Percent = percent, Bar = bar };
            }

            SerializedProperty rewards = so.FindProperty("_rewardItems");
            rewards.arraySize = parts.Rewards.Length;
            for (int i = 0; i < parts.Rewards.Length; i++)
            {
                GameObject item = NewChild(root, "Reward" + i);
                TMP_Text amount = NewText(item, "Amount");
                SerializedProperty element = rewards.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Root").objectReferenceValue = item;
                element.FindPropertyRelative("Button").objectReferenceValue = NewButton(item, "Button");
                element.FindPropertyRelative("Icon").objectReferenceValue = NewImage(item, "Icon");
                element.FindPropertyRelative("AmountText").objectReferenceValue = amount;
                parts.Rewards[i] = new RewardParts { Root = item, Amount = amount };
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return (view, parts);
        }

        private static GameObject NewChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            return go;
        }

        private static TMP_Text NewText(GameObject parent, string name)
        {
            var text = NewChild(parent, name).AddComponent<TextMeshProUGUI>();
            return text;
        }

        private static Image NewImage(GameObject parent, string name)
        {
            return NewChild(parent, name).AddComponent<Image>();
        }

        private static Button NewButton(GameObject parent, string name)
        {
            GameObject go = NewChild(parent, name);
            go.AddComponent<Image>();
            return go.AddComponent<Button>();
        }
    }
}
