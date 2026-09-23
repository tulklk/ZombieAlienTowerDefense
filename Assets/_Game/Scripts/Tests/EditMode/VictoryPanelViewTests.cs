using System.Collections.Generic;
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
                sources: new List<DamageResultEntry>
                {
                    new DamageResultEntry("Mortar Tower", null, 1600f),
                    new DamageResultEntry("Blaster Tower", null, 400f),
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

            view.Show(Result(new List<VictoryReward>(), new List<DamageResultEntry>(), 0f));

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
                new List<DamageResultEntry>
                {
                    new DamageResultEntry("Mortar Tower", null, 750f),
                    new DamageResultEntry("Rocket", null, 250f),
                },
                totalDamage: 1000f));

            Assert.AreEqual("75%", parts.Stats[0].Percent.text);
            Assert.AreEqual(0.75f, parts.Stats[0].Bar.fillAmount, 0.001f);
            Assert.AreEqual("25%", parts.Stats[1].Percent.text);
            Assert.AreEqual("All Damage: 1K", parts.StatsTotal.text);

            // A level where nothing landed must not divide by zero.
            view.Show(Result(
                new List<VictoryReward>(),
                new List<DamageResultEntry> { new DamageResultEntry("Rocket", null, 0f) },
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

            view.Show(Result(new List<VictoryReward>(), new List<DamageResultEntry>(), 0f));

            parts.NextButton.onClick.Invoke();
            parts.NextButton.onClick.Invoke(); // a second tap before the scene swaps must not count

            Assert.AreEqual(1, clicks, "Only the first Next is forwarded.");
            Assert.IsFalse(parts.NextButton.interactable);
        }

        [TestCase(20, 20, "Perfect Clear")]
        [TestCase(13, 20, "Remaining HP: ")]
        [TestCase(10, 20, "Remaining HP: ")]
        [TestCase(9, 20, "Clear")]
        [TestCase(1, 20, "Clear")]
        public void ResultLine_FollowsTheBaseHpRule(int remaining, int max, string expectedStart)
        {
            (VictoryPanelView view, Parts parts) = CreateView();

            view.Show(new LevelVictoryResult("level_01", "CAMPAIGN LEVEL 1", 1, isPerfectClear: remaining >= max,
                remaining, max, new List<VictoryReward>(), new List<DamageResultEntry>(), 0f, hasNextLevel: true));

            StringAssert.StartsWith(expectedStart, parts.ResultText.text);
            if (expectedStart == "Clear")
            {
                Assert.AreEqual("Clear", parts.ResultText.text, "Below 50% the line is just 'Clear'.");
            }
        }

        [Test]
        public void ResultLine_ShowsTheRemainingPercent()
        {
            (VictoryPanelView view, Parts parts) = CreateView();

            // 62% of the base left (Acceptance test 9).
            view.Show(new LevelVictoryResult("level_01", "CAMPAIGN LEVEL 1", 2, isPerfectClear: false,
                62, 100, new List<VictoryReward>(), new List<DamageResultEntry>(), 0f, hasNextLevel: true));

            StringAssert.Contains("62%", parts.ResultText.text);
            StringAssert.StartsWith("Remaining HP: ", parts.ResultText.text);
        }

        [Test]
        public void Rewards_MoreThanTheBuiltTiles_GrowThePoolOnce()
        {
            (VictoryPanelView view, Parts parts) = CreateView();
            var rewards = new List<VictoryReward>
            {
                new VictoryReward(VictoryRewardType.Coins, 5800),
                new VictoryReward(VictoryRewardType.Experience, 3000),
                new VictoryReward(VictoryRewardType.UfoBaseCard, 5),
                new VictoryReward(VictoryRewardType.BlasterCard, 10),
                new VictoryReward(VictoryRewardType.FrostCard, 20),
            };

            view.Show(Result(rewards, new List<DamageResultEntry>(), 0f));
            int tilesAfterFirstShow = parts.Rewards[0].Root.transform.parent.childCount;
            view.Show(Result(rewards, new List<DamageResultEntry>(), 0f));

            Assert.AreEqual(tilesAfterFirstShow, parts.Rewards[0].Root.transform.parent.childCount,
                "Showing the panel again reuses the clones instead of instantiating more.");

            var shownAmounts = new List<string>();
            foreach (Transform child in parts.Rewards[0].Root.transform.parent)
            {
                var amount = child.Find("Amount")?.GetComponent<TMP_Text>();
                if (child.name.StartsWith("Reward") && child.gameObject.activeSelf && amount != null)
                {
                    shownAmounts.Add(amount.text);
                }
            }

            CollectionAssert.AreEquivalent(new[] { "5.8K", "3K", "5", "10", "20" }, shownAmounts,
                "Every granted reward gets a tile with its compact amount.");
        }

        [Test]
        public void Statistics_ThreeTowers_SplitFiftyThirtyTwenty()
        {
            (VictoryPanelView view, Parts parts) = CreateView(statRows: 3);

            view.Show(Result(new List<VictoryReward>(), new List<DamageResultEntry>
            {
                new DamageResultEntry("Blaster", null, 500f),
                new DamageResultEntry("Mortar", null, 300f),
                new DamageResultEntry("Frost", null, 200f),
            }, 1000f));

            Assert.AreEqual("All Damage: 1K", parts.StatsTotal.text);
            Assert.AreEqual("50%", parts.Stats[0].Percent.text);
            Assert.AreEqual("30%", parts.Stats[1].Percent.text);
            Assert.AreEqual("20%", parts.Stats[2].Percent.text);
            Assert.AreEqual(0.5f, parts.Stats[0].Bar.fillAmount, 0.001f);
        }

        [Test]
        public void OpeningAndClosingPopups_ChangesNoNumbers()
        {
            (VictoryPanelView view, Parts parts) = CreateView();
            view.Show(Result(
                new List<VictoryReward> { new VictoryReward(VictoryRewardType.Coins, 1200) },
                new List<DamageResultEntry> { new DamageResultEntry("Blaster", null, 1000f) },
                1000f));

            for (int i = 0; i < 3; i++)
            {
                parts.StatisticsButton.onClick.Invoke();
                parts.RewardButtons[0].onClick.Invoke(); // a reward tap only opens its detail popup
            }

            Assert.AreEqual("All Damage: 1K", parts.StatsTotal.text);
            Assert.AreEqual("100%", parts.Stats[0].Percent.text);
            Assert.AreEqual("1.2K", parts.Rewards[0].Amount.text);
        }

        private static LevelVictoryResult Result(List<VictoryReward> rewards,
            List<DamageResultEntry> sources, float totalDamage)
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
            public Button[] RewardButtons;
        }

        private (VictoryPanelView, Parts) CreateView(int statRows = 2)
        {
            var root = new GameObject("VictoryPanel");
            _objects.Add(root);
            var view = root.AddComponent<VictoryPanelView>();
            var so = new SerializedObject(view);

            var parts = new Parts
            {
                Leaders = new LeaderParts[2],
                Stats = new StatParts[statRows],
                Rewards = new RewardParts[2],
                RewardButtons = new Button[2],
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
                parts.RewardButtons[i] = NewButton(item, "Button");
                element.FindPropertyRelative("Button").objectReferenceValue = parts.RewardButtons[i];
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
