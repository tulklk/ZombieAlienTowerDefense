using System;
using System.Collections.Generic;
using AlienDefense.Combat;
using AlienDefense.Progression;
using AlienDefense.UI.MainMenu;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>The victory screen: the level result, this match's top damage sources, the rewards that were granted,
    /// and Next. Two panels hang off it - a reward detail popup and the full damage statistics - both pre-built and
    /// reused rather than instantiated per click.
    ///
    /// Strictly a presenter of <see cref="LevelVictoryResult"/>: rewards are already granted and saved by the time
    /// Show is called, so nothing here can hand them out twice, and Next only raises an event for
    /// GameStateUIController to navigate with.</summary>
    public sealed class VictoryPanelView : MonoBehaviour
    {
        /// <summary>One "damage leaders" line on the main panel.</summary>
        [Serializable]
        public struct LeaderRow
        {
            public GameObject Root;
            public Image Icon;
            public TMP_Text NameText;
            public TMP_Text ValueText;
            public Image Bar;
        }

        /// <summary>One reward tile in the grid.</summary>
        [Serializable]
        public struct RewardItem
        {
            public GameObject Root;
            public Button Button;
            public Image Frame;
            public Image Icon;
            public TMP_Text AmountText;
        }

        /// <summary>One row of the damage statistics popup.</summary>
        [Serializable]
        public struct StatRow
        {
            public GameObject Root;
            public Image Icon;
            public TMP_Text NameText;
            public TMP_Text PercentText;
            public TMP_Text ValueText;
            public Image Bar;
        }

        private const float CountUpDuration = 0.7f;

        [Header("Root")]
        [SerializeField]
        private CanvasGroup _overlayGroup;

        [SerializeField]
        private RectTransform _mainPanel;

        [SerializeField]
        [Tooltip("Optional. Golden glow / sparkles behind the panel.")]
        private CanvasGroup _victoryVfx;

        [Header("Header")]
        [SerializeField]
        private TMP_Text _levelText;

        [SerializeField]
        private TMP_Text _resultText;

        [Header("Damage leaders")]
        [SerializeField]
        private LeaderRow[] _leaderRows = new LeaderRow[0];

        [SerializeField]
        [Tooltip("Optional. Shown when nothing dealt damage this level.")]
        private GameObject _noDamageLabel;

        [SerializeField]
        private Button _statisticsButton;

        [Header("Rewards")]
        [SerializeField]
        private RewardItem[] _rewardItems = new RewardItem[0];

        [SerializeField]
        [Tooltip("Optional. Shown when the level granted nothing.")]
        private GameObject _noRewardLabel;

        [SerializeField]
        private Button _nextButton;

        [Header("Reward detail popup")]
        [SerializeField]
        private CanvasGroup _rewardPopup;

        [SerializeField]
        private RectTransform _rewardPopupPanel;

        [SerializeField]
        private Image _rewardPopupHeader;

        [SerializeField]
        private TMP_Text _rewardPopupTitle;

        [SerializeField]
        private Image _rewardPopupIcon;

        [SerializeField]
        private TMP_Text _rewardPopupAmount;

        [SerializeField]
        private TMP_Text _rewardPopupDescription;

        [SerializeField]
        private Button _rewardPopupClose;

        [Header("Damage statistics popup")]
        [SerializeField]
        private CanvasGroup _statsPopup;

        [SerializeField]
        private RectTransform _statsPopupPanel;

        [SerializeField]
        private TMP_Text _statsTotalText;

        [SerializeField]
        private StatRow[] _statRows = new StatRow[0];

        [SerializeField]
        private Button _statsPopupClose;

        [Header("Data")]
        [SerializeField]
        private VictoryRewardCatalog _rewardCatalog;

        [SerializeField]
        [Tooltip("Shown while the base finished untouched.")]
        private string _perfectClearText = "Perfect Clear";

        [SerializeField]
        private string _completedText = "Level Complete";

        /// <summary>Raised when Next is tapped (once - the button disables itself).</summary>
        public event Action NextClicked;

        private LevelVictoryResult _result;
        private Sequence _introSequence;
        private Tween _popupTween;
        private bool _listenersWired;
        private bool _nextRaised;

        private void Awake()
        {
            WireListeners();
        }

        private void WireListeners()
        {
            if (_listenersWired)
            {
                return;
            }

            _listenersWired = true;
            if (_nextButton != null)
            {
                _nextButton.onClick.AddListener(HandleNextClicked);
            }

            if (_statisticsButton != null)
            {
                _statisticsButton.onClick.AddListener(OpenStatistics);
            }

            if (_statsPopupClose != null)
            {
                _statsPopupClose.onClick.AddListener(CloseStatistics);
            }

            if (_rewardPopupClose != null)
            {
                _rewardPopupClose.onClick.AddListener(CloseRewardDetail);
            }

            for (int i = 0; i < _rewardItems.Length; i++)
            {
                int index = i;
                if (_rewardItems[i].Button != null)
                {
                    _rewardItems[i].Button.onClick.AddListener(() => OpenRewardDetail(index));
                }
            }
        }

        /// <summary>Fills the panel from the finished level and plays the reveal. Safe to call again (a re-show
        /// restarts the animation rather than stacking tweens).</summary>
        public void Show(LevelVictoryResult result)
        {
            WireListeners();
            _result = result;

            HidePopupImmediate(_rewardPopup);
            HidePopupImmediate(_statsPopup);

            _nextRaised = false;
            if (_nextButton != null)
            {
                _nextButton.interactable = true;
            }

            PopulateHeader();
            PopulateLeaders();
            PopulateRewards();
            PopulateStatistics();
            PlayIntro();
        }

        private void PopulateHeader()
        {
            if (_levelText != null)
            {
                _levelText.text = _result != null ? _result.LevelDisplayName : string.Empty;
            }

            if (_resultText != null)
            {
                _resultText.text = _result != null && _result.IsPerfectClear ? _perfectClearText : _completedText;
            }
        }

        private void PopulateLeaders()
        {
            IReadOnlyList<CombatStatsService.Contributor> sources = _result != null
                ? _result.DamageSources
                : Array.Empty<CombatStatsService.Contributor>();

            float best = sources.Count > 0 ? Mathf.Max(1f, sources[0].Damage) : 1f;
            for (int i = 0; i < _leaderRows.Length; i++)
            {
                LeaderRow row = _leaderRows[i];
                bool used = i < sources.Count;
                if (row.Root != null)
                {
                    row.Root.SetActive(used);
                }

                if (!used)
                {
                    continue;
                }

                CombatStatsService.Contributor source = sources[i];
                if (row.NameText != null)
                {
                    row.NameText.text = source.Name;
                }

                if (row.ValueText != null)
                {
                    row.ValueText.text = CurrencyFormatter.Format(Mathf.RoundToInt(source.Damage));
                }

                if (row.Icon != null)
                {
                    row.Icon.sprite = source.Icon;
                    row.Icon.enabled = source.Icon != null;
                }

                if (row.Bar != null)
                {
                    row.Bar.fillAmount = Mathf.Clamp01(source.Damage / best);
                }
            }

            if (_noDamageLabel != null)
            {
                _noDamageLabel.SetActive(sources.Count == 0);
            }

            if (_statisticsButton != null)
            {
                _statisticsButton.gameObject.SetActive(sources.Count > 0);
            }
        }

        private void PopulateRewards()
        {
            IReadOnlyList<VictoryReward> rewards = _result != null ? _result.Rewards : Array.Empty<VictoryReward>();
            for (int i = 0; i < _rewardItems.Length; i++)
            {
                RewardItem item = _rewardItems[i];
                bool used = i < rewards.Count;
                if (item.Root != null)
                {
                    item.Root.SetActive(used);
                }

                if (!used)
                {
                    continue;
                }

                VictoryReward reward = rewards[i];
                VictoryRewardCatalog.Entry entry = GetEntry(reward.Type);

                if (item.Icon != null)
                {
                    item.Icon.sprite = entry.Icon;
                    item.Icon.enabled = entry.Icon != null;
                }

                if (item.Frame != null)
                {
                    item.Frame.color = entry.HeaderColor;
                }

                if (item.AmountText != null)
                {
                    item.AmountText.text = CurrencyFormatter.Format(reward.Amount);
                }
            }

            if (_noRewardLabel != null)
            {
                _noRewardLabel.SetActive(rewards.Count == 0);
            }
        }

        private void PopulateStatistics()
        {
            IReadOnlyList<CombatStatsService.Contributor> sources = _result != null
                ? _result.DamageSources
                : Array.Empty<CombatStatsService.Contributor>();
            float total = _result != null ? _result.TotalDamage : 0f;

            if (_statsTotalText != null)
            {
                _statsTotalText.text = "All Damage: " + CurrencyFormatter.Format(Mathf.RoundToInt(total));
            }

            for (int i = 0; i < _statRows.Length; i++)
            {
                StatRow row = _statRows[i];
                bool used = i < sources.Count;
                if (row.Root != null)
                {
                    row.Root.SetActive(used);
                }

                if (!used)
                {
                    continue;
                }

                CombatStatsService.Contributor source = sources[i];
                float share = total > 0f ? Mathf.Clamp01(source.Damage / total) : 0f;

                if (row.NameText != null)
                {
                    row.NameText.text = source.Name;
                }

                if (row.PercentText != null)
                {
                    row.PercentText.text = Mathf.RoundToInt(share * 100f) + "%";
                }

                if (row.ValueText != null)
                {
                    row.ValueText.text = CurrencyFormatter.Format(Mathf.RoundToInt(source.Damage));
                }

                if (row.Icon != null)
                {
                    row.Icon.sprite = source.Icon;
                    row.Icon.enabled = source.Icon != null;
                }

                if (row.Bar != null)
                {
                    row.Bar.fillAmount = share;
                }
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Reveal
        // ------------------------------------------------------------------------------------------------------------

        /// <summary>Overlay, panel, header, leaders, then the rewards popping in one after another, then Next.
        /// Unscaled so the reveal still plays if something froze the game clock.</summary>
        private void PlayIntro()
        {
            _introSequence?.Kill();

            if (_overlayGroup != null)
            {
                _overlayGroup.alpha = 0f;
            }

            if (_mainPanel != null)
            {
                _mainPanel.localScale = Vector3.one * 0.85f;
            }

            if (_victoryVfx != null)
            {
                _victoryVfx.alpha = 0f;
            }

            SetAlpha(_levelText, 0f);
            SetAlpha(_resultText, 0f);
            foreach (LeaderRow row in _leaderRows)
            {
                SetScale(row.Root, Vector3.one * 0.9f);
                SetGroupAlpha(row.Root, 0f);
            }

            foreach (RewardItem item in _rewardItems)
            {
                SetScale(item.Root, Vector3.zero);
            }

            if (_nextButton != null)
            {
                _nextButton.transform.localScale = Vector3.zero;
            }

            _introSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

            if (_overlayGroup != null)
            {
                _introSequence.Insert(0f, Fade(_overlayGroup, 1f, 0.18f));
            }

            if (_mainPanel != null)
            {
                _introSequence.Insert(0.10f, _mainPanel.DOScale(1.03f, 0.18f).SetEase(Ease.OutQuad));
                _introSequence.Insert(0.28f, _mainPanel.DOScale(1f, 0.12f).SetEase(Ease.OutQuad));
            }

            if (_victoryVfx != null)
            {
                _introSequence.Insert(0.35f, Fade(_victoryVfx, 1f, 0.45f));
            }

            _introSequence.Insert(0.55f, FadeIn(_levelText, 0.25f));
            _introSequence.Insert(0.70f, FadeIn(_resultText, 0.25f));

            float leaderTime = 0.85f;
            foreach (LeaderRow row in _leaderRows)
            {
                if (row.Root == null || !row.Root.activeSelf)
                {
                    continue;
                }

                _introSequence.Insert(leaderTime, row.Root.transform.DOScale(1f, 0.22f).SetEase(Ease.OutBack));
                Tween fade = FadeInGroup(row.Root, 0.22f);
                if (fade != null)
                {
                    _introSequence.Insert(leaderTime, fade);
                }

                leaderTime += 0.09f;
            }

            float rewardTime = 1.15f;
            foreach (RewardItem item in _rewardItems)
            {
                if (item.Root == null || !item.Root.activeSelf)
                {
                    continue;
                }

                _introSequence.Insert(rewardTime, item.Root.transform.DOScale(1.15f, 0.16f).SetEase(Ease.OutQuad));
                _introSequence.Insert(rewardTime + 0.16f, item.Root.transform.DOScale(1f, 0.10f).SetEase(Ease.OutQuad));
                rewardTime += 0.08f;
            }

            AppendCountUps(_introSequence, 1.15f);

            if (_nextButton != null)
            {
                _introSequence.Insert(Mathf.Max(1.8f, rewardTime + 0.15f),
                    _nextButton.transform.DOScale(1f, 0.28f).SetEase(Ease.OutBack));
            }
        }

        /// <summary>Coins and XP count up instead of appearing at their final value.</summary>
        private void AppendCountUps(Sequence sequence, float startTime)
        {
            IReadOnlyList<VictoryReward> rewards = _result != null ? _result.Rewards : Array.Empty<VictoryReward>();
            for (int i = 0; i < _rewardItems.Length && i < rewards.Count; i++)
            {
                TMP_Text label = _rewardItems[i].AmountText;
                VictoryReward reward = rewards[i];
                if (label == null || reward.Amount < 100)
                {
                    continue; // small counts read better as a plain number
                }

                int shown = 0;
                label.text = "0";
                sequence.Insert(startTime + i * 0.08f, DOTween.To(() => shown, value =>
                {
                    shown = value;
                    label.text = CurrencyFormatter.Format(shown);
                }, reward.Amount, CountUpDuration).SetEase(Ease.OutCubic));
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Popups
        // ------------------------------------------------------------------------------------------------------------

        private void OpenRewardDetail(int index)
        {
            IReadOnlyList<VictoryReward> rewards = _result != null ? _result.Rewards : Array.Empty<VictoryReward>();
            if (index < 0 || index >= rewards.Count)
            {
                return;
            }

            VictoryReward reward = rewards[index];
            VictoryRewardCatalog.Entry entry = GetEntry(reward.Type);

            if (_rewardPopupTitle != null)
            {
                _rewardPopupTitle.text = entry.DisplayName;
            }

            if (_rewardPopupDescription != null)
            {
                _rewardPopupDescription.text = entry.Description;
            }

            if (_rewardPopupAmount != null)
            {
                _rewardPopupAmount.text = CurrencyFormatter.Format(reward.Amount);
            }

            if (_rewardPopupIcon != null)
            {
                _rewardPopupIcon.sprite = entry.Icon;
                _rewardPopupIcon.enabled = entry.Icon != null;
            }

            if (_rewardPopupHeader != null)
            {
                _rewardPopupHeader.color = entry.HeaderColor;
            }

            OpenPopup(_rewardPopup, _rewardPopupPanel);
        }

        private void CloseRewardDetail()
        {
            ClosePopup(_rewardPopup, _rewardPopupPanel);
        }

        private void OpenStatistics()
        {
            OpenPopup(_statsPopup, _statsPopupPanel);
        }

        private void CloseStatistics()
        {
            ClosePopup(_statsPopup, _statsPopupPanel);
        }

        /// <summary>Popups are never destroyed: they fade/scale in and out, and block the panel underneath while
        /// open (their own full-screen group takes the raycasts).</summary>
        private void OpenPopup(CanvasGroup group, RectTransform panel)
        {
            if (group == null)
            {
                return;
            }

            _popupTween?.Kill();
            group.gameObject.SetActive(true);
            group.alpha = 0f;
            group.blocksRaycasts = true;
            group.interactable = true;

            if (panel != null)
            {
                panel.localScale = Vector3.one * 0.85f;
                _popupTween = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                    .Insert(0f, Fade(group, 1f, 0.15f))
                    .Insert(0f, panel.DOScale(1.05f, 0.16f).SetEase(Ease.OutBack))
                    .Insert(0.16f, panel.DOScale(1f, 0.09f));
                return;
            }

            _popupTween = Fade(group, 1f, 0.15f).SetUpdate(true).SetLink(gameObject);
        }

        private void ClosePopup(CanvasGroup group, RectTransform panel)
        {
            if (group == null)
            {
                return;
            }

            _popupTween?.Kill();
            group.blocksRaycasts = false;
            group.interactable = false;

            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            sequence.Insert(0f, Fade(group, 0f, 0.15f));
            if (panel != null)
            {
                sequence.Insert(0f, panel.DOScale(0.9f, 0.15f).SetEase(Ease.InQuad));
            }

            sequence.OnComplete(() => group.gameObject.SetActive(false));
            _popupTween = sequence;
        }

        private static void HidePopupImmediate(CanvasGroup group)
        {
            if (group == null)
            {
                return;
            }

            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            group.gameObject.SetActive(false);
        }

        private void HandleNextClicked()
        {
            if (_nextRaised)
            {
                return; // a second tap before the scene swaps must not start a second transition
            }

            _nextRaised = true;
            if (_nextButton != null)
            {
                _nextButton.interactable = false; // one shot: the scene load follows
            }

            NextClicked?.Invoke();
        }

        private VictoryRewardCatalog.Entry GetEntry(VictoryRewardType type)
        {
            if (_rewardCatalog != null)
            {
                _rewardCatalog.TryGet(type, out VictoryRewardCatalog.Entry entry);
                return entry;
            }

            return new VictoryRewardCatalog.Entry
            {
                Type = type,
                DisplayName = type.ToString(),
                Description = string.Empty,
                HeaderColor = Color.white,
            };
        }

        // ------------------------------------------------------------------------------------------------------------

        /// <summary>DOTween's UI module shortcuts are not generated in this project, so alpha tweens go through
        /// DOTween.To like the rest of the codebase.</summary>
        private static Tween Fade(CanvasGroup group, float target, float duration)
        {
            return DOTween.To(() => group.alpha, value => group.alpha = value, target, duration);
        }

        private static void SetAlpha(TMP_Text text, float alpha)
        {
            if (text != null)
            {
                text.alpha = alpha;
            }
        }

        private static Tween FadeIn(TMP_Text text, float duration)
        {
            return text != null ? DOTween.To(() => text.alpha, value => text.alpha = value, 1f, duration) : null;
        }

        private static void SetScale(GameObject target, Vector3 scale)
        {
            if (target != null)
            {
                target.transform.localScale = scale;
            }
        }

        private static void SetGroupAlpha(GameObject target, float alpha)
        {
            if (target == null)
            {
                return;
            }

            var group = target.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = alpha;
            }
        }

        private static Tween FadeInGroup(GameObject target, float duration)
        {
            if (target == null)
            {
                return null;
            }

            var group = target.GetComponent<CanvasGroup>();
            return group != null ? Fade(group, 1f, duration) : null;
        }

#if UNITY_EDITOR
        /// <summary>Editor-only preview: fills the panel with sample data so the layout and the reveal can be checked
        /// without finishing a level. Never compiled into a build.</summary>
        [ContextMenu("Debug Show Victory")]
        private void DebugShowVictory()
        {
            gameObject.SetActive(true);
            var rewards = new List<VictoryReward>
            {
                new VictoryReward(VictoryRewardType.Coins, 5800),
                new VictoryReward(VictoryRewardType.Experience, 3000),
                new VictoryReward(VictoryRewardType.Gems, 5),
            };

            var damage = new List<CombatStatsService.Contributor>
            {
                new CombatStatsService.Contributor("Mortar Tower", null, 1600f),
                new CombatStatsService.Contributor("Blaster Tower", null, 800f),
                new CombatStatsService.Contributor("Frost Tower", null, 450f),
                new CombatStatsService.Contributor("UFO", null, 120f),
            };

            Show(new LevelVictoryResult("level_debug", "CAMPAIGN LEVEL 1", 3, true, 20, 20, rewards, damage, 2970f, true));
        }
#endif

        private void OnDestroy()
        {
            _introSequence?.Kill();
            _popupTween?.Kill();
        }
    }
}
