using System;
using System.Collections.Generic;
using System.Text;
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
    /// Show is called, so nothing here can hand them out twice (tapping a reward only opens its detail popup), and
    /// Next only raises an event for GameStateUIController to navigate with.</summary>
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

            [Tooltip("Optional. The tower's upgrade tier; hidden for sources without one.")]
            public Image[] Stars;
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
        [Tooltip("Pre-built reward tiles. When a level grants more rewards than there are tiles, the first one is " +
            "cloned (once - the clones are kept and reused).")]
        private RewardItem[] _rewardItems = new RewardItem[0];

        [SerializeField]
        [Tooltip("Optional. The area the tiles are laid out in; defaults to the first tile's parent.")]
        private RectTransform _rewardGrid;

        [SerializeField]
        [Tooltip("Gap between reward tiles (x, y) in canvas units.")]
        private Vector2 _rewardSpacing = new Vector2(26f, 24f);

        [SerializeField, Min(1)]
        private int _maxRewardColumns = 5;

        [SerializeField, Min(0f)]
        [Tooltip("Inner margin kept free around the grid.")]
        private float _rewardGridPadding = 12f;

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

        [SerializeField]
        [Tooltip("Filled star of a statistics row.")]
        private Sprite _starSprite;

        [SerializeField]
        [Tooltip("Empty star of a statistics row.")]
        private Sprite _noStarSprite;

        [Header("Data")]
        [SerializeField]
        private VictoryRewardCatalog _rewardCatalog;

        [SerializeField]
        [Tooltip("Shown while the base finished untouched.")]
        private string _perfectClearText = "Perfect Clear";

        [SerializeField]
        [Tooltip("Shown when the base finished below 50% HP.")]
        private string _clearText = "Clear";

        [SerializeField]
        private string _completedText = "Level Complete";

        /// <summary>Raised when Next is tapped (once - the button disables itself).</summary>
        public event Action NextClicked;

        private readonly List<RewardItem> _rewardPool = new List<RewardItem>();
        private LevelVictoryResult _result;
        private Sequence _introSequence;
        private Tween _popupTween;
        private bool _listenersWired;
        private bool _resultShown;
        private bool _nextRaised;
        private float _rewardItemScale = 1f;
        private Vector2 _iconInsetSize;
        private bool _hasIconInsetSize;

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

            _rewardPool.Clear();
            for (int i = 0; i < _rewardItems.Length; i++)
            {
                AddToPool(_rewardItems[i]);
            }
        }

        /// <summary>Fills the panel from the finished level and plays the reveal. Safe to call again (a re-show
        /// restarts the animation rather than stacking tweens).</summary>
        public void Show(LevelVictoryResult result)
        {
            WireListeners();
            _result = result;
            _resultShown = false;
            _introSequence?.Kill(); // before anything else, so the old reveal cannot re-enable Next
            _introSequence = null;

            HidePopupImmediate(_rewardPopup);
            HidePopupImmediate(_statsPopup);

            _nextRaised = false;
            if (_nextButton != null)
            {
                _nextButton.interactable = false; // enabled once the reveal has put everything on screen
            }

            PopulateHeader();
            PopulateLeaders();
            PopulateRewards();
            PopulateStatistics();

            // The result is on the panel from here on; Next may leave (the reveal only animates it in).
            _resultShown = result != null;
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
                _resultText.text = BuildResultLine();
            }
        }

        /// <summary>100% base HP -> "Perfect Clear"; 50-99% -> "Remaining HP: 62%" (same wording and colour
        /// thresholds MainMenu prints under the level title); below 50% -> "Clear".</summary>
        private string BuildResultLine()
        {
            if (_result == null)
            {
                return string.Empty;
            }

            if (_result.IsPerfectClear)
            {
                return _perfectClearText;
            }

            if (_result.MaxBaseHealth <= 0)
            {
                return _completedText;
            }

            int percent = _result.RemainingHpPercent;
            if (percent < 50)
            {
                return _clearText;
            }

            return $"Remaining HP: <color={LevelStatusFormatter.PercentColorHex(percent)}>{percent}%</color>";
        }

        private IReadOnlyList<DamageResultEntry> Sources =>
            _result != null ? _result.DamageSources : Array.Empty<DamageResultEntry>();

        private IReadOnlyList<VictoryReward> Rewards =>
            _result != null ? _result.Rewards : Array.Empty<VictoryReward>();

        private void PopulateLeaders()
        {
            IReadOnlyList<DamageResultEntry> sources = Sources;
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

                DamageResultEntry source = sources[i];
                if (row.NameText != null)
                {
                    row.NameText.text = source.Name;
                }

                if (row.ValueText != null)
                {
                    row.ValueText.text = FormatDamage(source.Damage);
                }

                SetIcon(row.Icon, source.Icon);

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
            IReadOnlyList<VictoryReward> rewards = Rewards;
            EnsureRewardSlots(rewards.Count);

            for (int i = 0; i < _rewardPool.Count; i++)
            {
                RewardItem item = _rewardPool[i];
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

                SetIcon(item.Icon, entry.Icon);
                FitIcon(item, entry.IconHasOwnFrame);

                if (item.Frame != null)
                {
                    item.Frame.color = entry.IconHasOwnFrame ? Color.clear : entry.HeaderColor; // clear still takes taps
                }

                if (item.AmountText != null)
                {
                    item.AmountText.text = CurrencyFormatter.Format(reward.Amount);
                }
            }

            LayoutRewards(rewards.Count);

            if (_noRewardLabel != null)
            {
                _noRewardLabel.SetActive(rewards.Count == 0);
            }
        }

        /// <summary>Card art brings its own frame, so it fills the whole tile; plain icons (coins, XP) keep the inset
        /// they were built with inside the coloured frame.</summary>
        private void FitIcon(RewardItem item, bool fillTile)
        {
            var icon = item.Icon != null ? item.Icon.rectTransform : null;
            var frame = item.Frame != null ? item.Frame.rectTransform : null;
            if (icon == null || frame == null)
            {
                return;
            }

            if (!_hasIconInsetSize)
            {
                _iconInsetSize = icon.sizeDelta;
                _hasIconInsetSize = true;
            }

            icon.sizeDelta = fillTile ? frame.sizeDelta : _iconInsetSize;
        }

        /// <summary>More rewards than pre-built tiles: clone the first tile until there are enough. Clones are kept,
        /// so re-opening the panel never instantiates again.</summary>
        private void EnsureRewardSlots(int count)
        {
            if (_rewardPool.Count >= count || _rewardPool.Count == 0 || _rewardPool[0].Root == null)
            {
                return;
            }

            RewardItem template = _rewardPool[0];
            Transform parent = template.Root.transform.parent;
            while (_rewardPool.Count < count)
            {
                GameObject clone = Instantiate(template.Root, parent, false);
                clone.name = template.Root.name + "_" + (_rewardPool.Count + 1);
                Transform templateRoot = template.Root.transform;
                AddToPool(new RewardItem
                {
                    Root = clone,
                    Button = Counterpart(template.Button, templateRoot, clone.transform),
                    Frame = Counterpart(template.Frame, templateRoot, clone.transform),
                    Icon = Counterpart(template.Icon, templateRoot, clone.transform),
                    AmountText = Counterpart(template.AmountText, templateRoot, clone.transform),
                });
            }
        }

        private void AddToPool(RewardItem item)
        {
            int index = _rewardPool.Count;
            _rewardPool.Add(item);
            if (item.Button != null)
            {
                item.Button.onClick.AddListener(() => OpenRewardDetail(index));
            }
        }

        /// <summary>The component in <paramref name="cloneRoot"/> sitting where <paramref name="original"/> sits
        /// under <paramref name="templateRoot"/>.</summary>
        private static T Counterpart<T>(T original, Transform templateRoot, Transform cloneRoot) where T : Component
        {
            if (original == null)
            {
                return null;
            }

            var path = new StringBuilder();
            for (Transform cursor = original.transform; cursor != null && cursor != templateRoot; cursor = cursor.parent)
            {
                path.Insert(0, path.Length > 0 ? cursor.name + "/" : cursor.name);
            }

            Transform match = path.Length == 0 ? cloneRoot : cloneRoot.Find(path.ToString());
            return match != null ? match.GetComponent<T>() : null;
        }

        /// <summary>Centres the used tiles in the reward area: one row up to 4, two balanced rows up to 8 (4+4,
        /// 3+3, ...), then rows of <see cref="_maxRewardColumns"/>. The whole block is scaled down when it would
        /// not fit, so tiles never overlap or leave the panel on any aspect ratio.</summary>
        private void LayoutRewards(int count)
        {
            _rewardItemScale = 1f;
            if (count <= 0 || _rewardPool.Count == 0 || _rewardPool[0].Root == null)
            {
                return;
            }

            RectTransform area = _rewardGrid != null ? _rewardGrid : _rewardPool[0].Root.transform.parent as RectTransform;
            var templateRect = _rewardPool[0].Root.transform as RectTransform;
            if (area == null || templateRect == null)
            {
                return;
            }

            Vector2 tile = templateRect.sizeDelta;
            int maxColumns = Mathf.Max(1, _maxRewardColumns);
            int columns = count <= 4 ? count : count <= 8 ? Mathf.CeilToInt(count * 0.5f) : maxColumns;
            columns = Mathf.Clamp(columns, 1, maxColumns);
            int rows = Mathf.CeilToInt(count / (float)columns);

            float gridWidth = columns * tile.x + (columns - 1) * _rewardSpacing.x;
            float gridHeight = rows * tile.y + (rows - 1) * _rewardSpacing.y;
            Rect bounds = area.rect;
            float availableWidth = Mathf.Max(1f, bounds.width - 2f * _rewardGridPadding);
            float availableHeight = Mathf.Max(1f, bounds.height - 2f * _rewardGridPadding);
            float scale = Mathf.Min(1f, availableWidth / Mathf.Max(1f, gridWidth), availableHeight / Mathf.Max(1f, gridHeight));
            _rewardItemScale = scale;

            // Positions are relative to the area's centre, whatever its pivot is.
            Vector2 centre = new Vector2((0.5f - area.pivot.x) * bounds.width, (0.5f - area.pivot.y) * bounds.height);
            for (int i = 0; i < count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                int inRow = Mathf.Min(columns, count - row * columns);
                float rowWidth = inRow * tile.x + (inRow - 1) * _rewardSpacing.x;
                float x = -rowWidth * 0.5f + tile.x * 0.5f + column * (tile.x + _rewardSpacing.x);
                float y = gridHeight * 0.5f - tile.y * 0.5f - row * (tile.y + _rewardSpacing.y);

                var rect = _rewardPool[i].Root.transform as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                rect.anchorMin = new Vector2(area.pivot.x, area.pivot.y);
                rect.anchorMax = rect.anchorMin;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = centre + new Vector2(x, y) * scale;
                rect.localScale = Vector3.one * scale;
            }
        }

        private void PopulateStatistics()
        {
            IReadOnlyList<DamageResultEntry> sources = Sources;
            float total = _result != null ? _result.TotalDamage : 0f;

            if (_statsTotalText != null)
            {
                _statsTotalText.text = "All Damage: " + FormatDamage(total);
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

                DamageResultEntry source = sources[i];
                float share = DamageResultEntry.ShareOf(source.Damage, total);

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
                    row.ValueText.text = FormatDamage(source.Damage);
                }

                SetIcon(row.Icon, source.Icon);

                if (row.Bar != null)
                {
                    row.Bar.fillAmount = share;
                }

                PopulateStars(row.Stars, source);
            }
        }

        /// <summary>Stars mean the tower's permanent upgrade tier - the only star-like rating the game has. A
        /// source without a tier (the UFO), or a row with more tiers than star images, hides the stars instead of
        /// showing a made-up rating.</summary>
        private void PopulateStars(Image[] stars, DamageResultEntry source)
        {
            if (stars == null)
            {
                return;
            }

            bool show = source.MaxStars > 0 && source.MaxStars <= stars.Length && _starSprite != null;
            for (int i = 0; i < stars.Length; i++)
            {
                Image star = stars[i];
                if (star == null)
                {
                    continue;
                }

                bool visible = show && i < source.MaxStars;
                star.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                bool filled = i < source.Stars;
                Sprite sprite = filled ? _starSprite : (_noStarSprite != null ? _noStarSprite : _starSprite);
                star.sprite = sprite;
                star.color = filled || _noStarSprite != null ? Color.white : new Color(0.25f, 0.25f, 0.25f, 0.8f);
            }
        }

        private static string FormatDamage(float damage)
        {
            return CurrencyFormatter.Format((long)Math.Round(Math.Max(0f, damage)));
        }

        private static void SetIcon(Image image, Sprite sprite)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.enabled = sprite != null;
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

            foreach (RewardItem item in _rewardPool)
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
            float rewardScale = _rewardItemScale;
            foreach (RewardItem item in _rewardPool)
            {
                if (item.Root == null || !item.Root.activeSelf)
                {
                    continue;
                }

                _introSequence.Insert(rewardTime, item.Root.transform.DOScale(rewardScale * 1.15f, 0.16f).SetEase(Ease.OutQuad));
                _introSequence.Insert(rewardTime + 0.16f, item.Root.transform.DOScale(rewardScale, 0.10f).SetEase(Ease.OutQuad));
                rewardTime += 0.08f;
            }

            AppendCountUps(_introSequence, 1.15f);

            if (_nextButton != null)
            {
                _introSequence.Insert(Mathf.Max(1.8f, rewardTime + 0.15f),
                    _nextButton.transform.DOScale(1f, 0.28f).SetEase(Ease.OutBack));
            }

            _introSequence.OnComplete(EnableNext);
            _introSequence.OnKill(EnableNext); // a killed reveal (re-show, scene change) must not strand the button
        }

        private void EnableNext()
        {
            if (_nextButton == null || _nextRaised || !_resultShown)
            {
                return;
            }

            _nextButton.transform.localScale = Vector3.one;
            _nextButton.interactable = true;
        }

        /// <summary>Coins and XP count up instead of appearing at their final value.</summary>
        private void AppendCountUps(Sequence sequence, float startTime)
        {
            IReadOnlyList<VictoryReward> rewards = Rewards;
            for (int i = 0; i < _rewardPool.Count && i < rewards.Count; i++)
            {
                TMP_Text label = _rewardPool[i].AmountText;
                VictoryReward reward = rewards[i];
                if (label == null || reward.Amount < 100)
                {
                    continue; // small counts read better as a plain number
                }

                // No "0" is written up front: the tile is still scaled to zero until its tween starts from 0.
                int shown = 0;
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

        /// <summary>Display only: shows what the tile is. Never grants anything.</summary>
        private void OpenRewardDetail(int index)
        {
            IReadOnlyList<VictoryReward> rewards = Rewards;
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

            SetIcon(_rewardPopupIcon, entry.Icon);

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

        /// <summary>Display only: the statistics were filled once in Show from the frozen result.</summary>
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
            if (_nextRaised || !_resultShown)
            {
                return; // nothing to leave yet, or a second tap before the scene swaps
            }

            _nextRaised = true;
            if (_nextButton != null)
            {
                _nextButton.interactable = false; // one shot: the scene load follows
            }

            HidePopupImmediate(_rewardPopup);
            HidePopupImmediate(_statsPopup);
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
        /// <summary>Editor-only layout preview with sample numbers, so the panel can be checked without finishing a
        /// level. Never compiled into a build, and never used by the game flow (which always passes the real
        /// result).</summary>
        [ContextMenu("Debug Show Victory (layout preview)")]
        private void DebugShowVictory()
        {
            gameObject.SetActive(true);
            var rewards = new List<VictoryReward>
            {
                new VictoryReward(VictoryRewardType.Coins, 5800),
                new VictoryReward(VictoryRewardType.Experience, 3000),
                new VictoryReward(VictoryRewardType.UfoBaseCard, 5),
                new VictoryReward(VictoryRewardType.BlasterCard, 10),
                new VictoryReward(VictoryRewardType.FrostCard, 20),
                new VictoryReward(VictoryRewardType.MortarCard, 10),
                new VictoryReward(VictoryRewardType.TeslaCard, 5),
                new VictoryReward(VictoryRewardType.ReactorBlueprint, 1),
                new VictoryReward(VictoryRewardType.AntiGravityBlueprint, 3),
            };

            var damage = new List<DamageResultEntry>
            {
                new DamageResultEntry("Blaster", null, 6400f, 1, 3),
                new DamageResultEntry("Mortar", null, 4500f, 2, 3),
                new DamageResultEntry("Frost", null, 1500f, 1, 3),
            };

            Show(new LevelVictoryResult("level_debug", "CAMPAIGN LEVEL 1", 2, false, 13, 20, rewards, damage, 12400f, true));
        }
#endif

        private void OnDestroy()
        {
            _introSequence?.Kill();
            _popupTween?.Kill();
        }
    }
}
