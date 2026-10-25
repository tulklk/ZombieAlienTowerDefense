using System;
using AlienDefense.Towers;
using AlienDefense.UI.Base;
using AlienDefense.UI.MainMenu;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Defense
{
    /// <summary>The Defense tab's detail popup: dark backdrop, title, close, left / right arrows and three pages
    /// (Core Stats with the level-up panel, Full Stats, About). Dumb view: it shows a DefenseEntry and raises
    /// events; the presenter owns selection, progression and saving. Animations run on unscaled time.</summary>
    public sealed class TowerDetailPopupView : MonoBehaviour
    {
        public enum Page
        {
            Core = 0,
            Full = 1,
            About = 2,
        }

        [Header("Frame")]
        [SerializeField] private CanvasGroup _backdrop;
        [SerializeField] private Button _backdropButton;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private CanvasGroup _panelGroup;
        [SerializeField] private RectTransform _content;
        [SerializeField] private CanvasGroup _contentGroup;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _previousButton;
        [SerializeField] private Button _nextButton;

        [Header("Tabs")]
        [SerializeField] private Button[] _tabButtons = Array.Empty<Button>();
        [SerializeField] private Image[] _tabImages = Array.Empty<Image>();
        [SerializeField] private Sprite _tabSelected;
        [SerializeField] private Sprite _tabNormal;
        [SerializeField] private GameObject[] _pages = Array.Empty<GameObject>();

        [Header("Core Stats")]
        [SerializeField] private Image _preview;
        [SerializeField] private Image _previewFrame;
        [SerializeField] private Sprite _previewFrameNormal;
        [SerializeField] private Sprite _previewFrameSpecial;
        [SerializeField] private RawImage _preview3D;
        [SerializeField] private TowerPreviewRenderer _previewRenderer;
        [SerializeField] private Image _previewTypeIcon;
        [SerializeField] private TMP_Text _coreLevel;
        [SerializeField] private Transform _coreCellRow;
        [SerializeField] private TowerStatCellView _coreCellTemplate;
        [SerializeField] private GameObject _costRow;
        [SerializeField] private TMP_Text _coinText;
        [SerializeField] private GameObject _cardGroup;
        [SerializeField] private Image _cardIcon;
        [SerializeField] private TMP_Text _cardText;
        [SerializeField] private Button _levelUpButton;
        [SerializeField] private CanvasGroup _levelUpGroup;
        [SerializeField] private TMP_Text _levelUpLabel;
        [SerializeField] private GameObject _levelUpArrow;
        [SerializeField] private GameObject _maxLabel;
        [SerializeField] private TMP_Text _bonusText;

        [Header("Full Stats")]
        [SerializeField] private TMP_Text _fullLevel;
        [SerializeField] private Transform _fullGrid;
        [SerializeField] private TowerStatCellView _fullCellTemplate;

        [Header("About")]
        [SerializeField] private TMP_Text _aboutText;

        [Header("Force gain (after a level-up)")]
        [SerializeField] private CanvasGroup _force;
        [SerializeField] private RectTransform _forceContent;
        [SerializeField] private TMP_Text _forceValue;
        [SerializeField] private TMP_Text _forceDelta;
        [SerializeField] private RectTransform[] _forceSparkles = Array.Empty<RectTransform>();

        [Header("Tooltip / toast")]
        [SerializeField] private GameObject _tooltip;
        [SerializeField] private TMP_Text _tooltipText;
        [SerializeField] private Button _tooltipDismiss;
        [SerializeField] private CanvasGroup _toast;
        [SerializeField] private TMP_Text _toastText;

        private UiViewPool<TowerStatCellView> _corePool;
        private UiViewPool<TowerStatCellView> _fullPool;
        private Tween _openTween;
        private Tween _slideTween;
        private Tween _toastTween;
        private Tween _forceTween;
        private Page _page;
        private bool _has3D;
        private bool _wired;

        public event Action CloseRequested;
        public event Action PreviousRequested;
        public event Action NextRequested;
        public event Action LevelUpRequested;

        public bool IsOpen => gameObject.activeSelf;

        private void Wire()
        {
            if (_wired)
            {
                return;
            }

            _wired = true;
            _corePool = new UiViewPool<TowerStatCellView>(_coreCellTemplate, _coreCellRow);
            _fullPool = new UiViewPool<TowerStatCellView>(_fullCellTemplate, _fullGrid);
            if (_closeButton != null) _closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            if (_backdropButton != null) _backdropButton.onClick.AddListener(() => CloseRequested?.Invoke());
            if (_previousButton != null) _previousButton.onClick.AddListener(() => PreviousRequested?.Invoke());
            if (_nextButton != null) _nextButton.onClick.AddListener(() => NextRequested?.Invoke());
            if (_levelUpButton != null) _levelUpButton.onClick.AddListener(() => LevelUpRequested?.Invoke());
            if (_tooltipDismiss != null) _tooltipDismiss.onClick.AddListener(HideTooltip);
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int page = i;
                if (_tabButtons[i] != null)
                {
                    _tabButtons[i].onClick.AddListener(() => ShowPage((Page)page));
                }
            }
        }

        public void Open(DefenseEntry entry, bool showArrows)
        {
            Wire();
            gameObject.SetActive(true);
            if (_previousButton != null) _previousButton.gameObject.SetActive(showArrows);
            if (_nextButton != null) _nextButton.gameObject.SetActive(showArrows);
            HideTooltip();
            if (_toast != null) _toast.alpha = 0f;
            if (_force != null) _force.alpha = 0f;
            _page = Page.Core;
            Bind(entry);
            ShowPage(Page.Core);

            _openTween?.Kill();
            _backdrop.alpha = 0f;
            _panelGroup.alpha = 0f;
            _panel.localScale = Vector3.one * 0.92f;
            _openTween = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Append(Fade(_backdrop, 1f, 0.18f))
                .Join(Fade(_panelGroup, 1f, 0.16f))
                .Join(_panel.DOScale(1.03f, 0.16f).SetEase(Ease.OutQuad))
                .Append(_panel.DOScale(1f, 0.09f).SetEase(Ease.InOutSine));
        }

        public void Close()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            _openTween?.Kill();
            _openTween = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Append(Fade(_backdrop, 0f, 0.14f))
                .Join(Fade(_panelGroup, 0f, 0.12f))
                .Join(_panel.DOScale(0.94f, 0.14f).SetEase(Ease.InQuad))
                .OnComplete(() =>
                {
                    gameObject.SetActive(false);
                    UpdatePreviewCamera();
                });
        }

        public void HideImmediate()
        {
            _openTween?.Kill();
            gameObject.SetActive(false);
            UpdatePreviewCamera();
        }

        /// <summary>Swaps to another entry with a short slide (direction: +1 = next, -1 = previous). The popup and the
        /// selected page stay as they are.</summary>
        public void SwitchTo(DefenseEntry entry, int direction)
        {
            HideTooltip();
            _slideTween?.Kill();
            float offset = 22f * Mathf.Sign(direction == 0 ? 1 : direction);
            _slideTween = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Append(SlideX(-offset, 0.07f).SetEase(Ease.InQuad))
                .Join(Fade(_contentGroup, 0f, 0.07f))
                .AppendCallback(() =>
                {
                    Bind(entry);
                    _content.anchoredPosition = new Vector2(offset, _content.anchoredPosition.y);
                })
                .Append(SlideX(0f, 0.08f).SetEase(Ease.OutQuad))
                .Join(Fade(_contentGroup, 1f, 0.08f));
        }

        /// <summary>Redraws everything for the entry in place (after a level-up, or a wallet change).</summary>
        public void Bind(DefenseEntry entry)
        {
            Wire();
            if (entry == null)
            {
                return;
            }

            if (_title != null) _title.text = entry.Title;

            string level = string.Format(DefenseStrings.LevelFormat, entry.LevelIndex + 1);
            if (_coreLevel != null) _coreLevel.text = entry.IsUnlocked ? level : DefenseStrings.Locked;
            if (_fullLevel != null) _fullLevel.text = entry.IsUnlocked ? level : DefenseStrings.Locked;

            // 3D showcase when the entry has a model and a renderer is wired; otherwise the 2D icon as before.
            _has3D = _previewRenderer != null && _preview3D != null && entry.PreviewModel != null;
            if (_preview3D != null)
            {
                _preview3D.gameObject.SetActive(_has3D);
                if (_has3D)
                {
                    _preview3D.texture = _previewRenderer.Texture;
                    _preview3D.color = entry.IsUnlocked ? Color.white : new Color(0.62f, 0.72f, 0.9f, 1f);
                }
            }

            if (_previewRenderer != null)
            {
                _previewRenderer.Show(_has3D ? entry.PreviewModel : null);
            }

            if (_preview != null)
            {
                _preview.sprite = entry.Icon;
                _preview.enabled = !_has3D && entry.Icon != null;
                _preview.color = entry.IsUnlocked ? Color.white : new Color(0.78f, 0.88f, 1f, 0.75f);
            }

            UpdatePreviewCamera();

            if (_previewFrame != null)
            {
                _previewFrame.sprite = entry.IsBase ? _previewFrameSpecial : _previewFrameNormal;
            }

            if (_previewTypeIcon != null)
            {
                _previewTypeIcon.sprite = entry.TypeIcon;
                _previewTypeIcon.transform.parent.gameObject.SetActive(entry.TypeIcon != null);
            }

            FillCells(_corePool, entry.Core);
            FillCells(_fullPool, entry.Full);
            if (_aboutText != null) _aboutText.text = entry.About;

            BindUpgradePanel(entry);
        }

        private void BindUpgradePanel(DefenseEntry entry)
        {
            bool canAct = !entry.IsMaxLevel || !entry.IsUnlocked;
            if (_costRow != null) _costRow.SetActive(canAct);
            if (_maxLabel != null) _maxLabel.SetActive(!canAct);
            if (_levelUpButton != null) _levelUpButton.gameObject.SetActive(canAct);

            if (canAct)
            {
                bool coinsOk = entry.OwnedCoins >= entry.RequiredCoins;
                bool cardsOk = entry.OwnedCards >= entry.RequiredCards;
                if (_coinText != null)
                {
                    _coinText.text = CurrencyFormatter.Format(entry.OwnedCoins) + "/" + Colour(CurrencyFormatter.Format(entry.RequiredCoins), coinsOk);
                }

                bool needsCards = entry.IsUnlocked && entry.RequiredCards > 0;
                if (_cardGroup != null) _cardGroup.SetActive(needsCards);
                if (needsCards)
                {
                    if (_cardIcon != null) _cardIcon.sprite = entry.CardIcon;
                    if (_cardText != null)
                    {
                        _cardText.text = CurrencyFormatter.Format(entry.OwnedCards) + "/" + Colour(CurrencyFormatter.Format(entry.RequiredCards), cardsOk);
                    }
                }

                bool affordable = entry.IsUnlocked ? entry.UpgradeCheck == MetaUpgradeResult.Success : coinsOk;
                if (_levelUpLabel != null) _levelUpLabel.text = entry.IsUnlocked ? DefenseStrings.LevelUp : DefenseStrings.Unlock;
                if (_levelUpArrow != null) _levelUpArrow.SetActive(entry.IsUnlocked);

                // Stays tappable when unaffordable so the tap can say why; it only looks disabled.
                if (_levelUpGroup != null) _levelUpGroup.alpha = affordable ? 1f : 0.45f;
            }

            if (_bonusText != null)
            {
                _bonusText.gameObject.SetActive(!entry.IsBase);
                _bonusText.text = DefenseStrings.UpgradeBonus + "  <color=#FFD84A>+" + DefenseStatBuilder.Percent(entry.UpgradeBonus) + "</color>";
            }
        }

        // The DOTween UI module is not referenced by this assembly, so CanvasGroup / RectTransform tweens go through
        // the generic getter-setter form.
        private static Tweener Fade(CanvasGroup group, float alpha, float duration)
        {
            return DOTween.To(() => group.alpha, a => group.alpha = a, alpha, duration);
        }

        private Tweener SlideX(float x, float duration)
        {
            return DOTween.To(() => _content.anchoredPosition.x, v => _content.anchoredPosition = new Vector2(v, _content.anchoredPosition.y), x, duration);
        }

        private static string Colour(string text, bool ok)
        {
            return ok ? text : "<color=#FF6B5E>" + text + "</color>";
        }

        private void FillCells(UiViewPool<TowerStatCellView> pool, System.Collections.Generic.List<DefenseStat> stats)
        {
            pool.Begin();
            for (int i = 0; i < stats.Count; i++)
            {
                TowerStatCellView cell = pool.Take();
                if (cell == null)
                {
                    continue;
                }

                cell.HelpClicked -= HandleHelp;
                cell.HelpClicked += HandleHelp;
                cell.Configure(stats[i]);
            }

            pool.End();
        }

        /// <summary>The showcase camera only renders while its picture is on screen: popup open, Core Stats page.</summary>
        private void UpdatePreviewCamera()
        {
            if (_previewRenderer != null)
            {
                _previewRenderer.SetVisible(gameObject.activeSelf && _has3D && _page == Page.Core);
            }
        }

        public void ShowPage(Page page)
        {
            _page = page;
            HideTooltip();
            for (int i = 0; i < _pages.Length; i++)
            {
                if (_pages[i] != null)
                {
                    _pages[i].SetActive(i == (int)page);
                }
            }

            for (int i = 0; i < _tabImages.Length; i++)
            {
                if (_tabImages[i] != null)
                {
                    _tabImages[i].sprite = i == (int)page ? _tabSelected : _tabNormal;
                }
            }

            UpdatePreviewCamera();
        }

        /// <summary>Punch on the preview, pop on the level label and a flash on the stat values after a level-up.</summary>
        public void PlayUpgradeFeedback()
        {
            if (_has3D && _previewRenderer != null)
            {
                _previewRenderer.PlayPunch();
            }

            if (_preview != null)
            {
                _preview.rectTransform.DOKill(true);
                _preview.rectTransform.DOPunchScale(Vector3.one * 0.09f, 0.32f, 6, 0.6f).SetUpdate(true).SetLink(gameObject);
            }

            if (_coreLevel != null)
            {
                _coreLevel.rectTransform.DOKill(true);
                _coreLevel.rectTransform.DOPunchScale(Vector3.one * 0.22f, 0.3f, 5, 0.6f).SetUpdate(true).SetLink(gameObject);
            }

            if (_corePool != null)
            {
                for (int i = 0; i < _corePool.ActiveCount; i++)
                {
                    RectTransform value = _corePool.Instances[i].ValueRect;
                    if (value != null)
                    {
                        value.DOKill(true);
                        value.DOPunchScale(Vector3.one * 0.18f, 0.28f, 5, 0.6f).SetUpdate(true).SetLink(gameObject);
                    }
                }
            }
        }

        public void ShowToast(string message)
        {
            if (_toast == null || _toastText == null)
            {
                return;
            }

            _toastText.text = message;
            _toastTween?.Kill();
            _toast.alpha = 0f;
            _toastTween = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Append(Fade(_toast, 1f, 0.12f))
                .AppendInterval(1.1f)
                .Append(Fade(_toast, 0f, 0.25f));

            if (_levelUpButton != null)
            {
                _levelUpButton.transform.DOKill(true);
                _levelUpButton.transform.DOShakePosition(0.25f, new Vector3(10f, 0f, 0f), 18, 0f).SetUpdate(true).SetLink(gameObject);
            }
        }

        public void ShowTooltip(string text)
        {
            if (_tooltip == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            _tooltipText.text = text;
            _tooltip.SetActive(true);
        }

        private void HandleHelp(TowerStatCellView cell, string text)
        {
            ShowTooltip(text);
        }

        private void HideTooltip()
        {
            if (_tooltip != null)
            {
                _tooltip.SetActive(false);
            }
        }

        private void OnDisable()
        {
            _openTween?.Kill();
            _slideTween?.Kill();
            _toastTween?.Kill();
            _forceTween?.Kill();
            if (_force != null)
            {
                _force.alpha = 0f;
            }
        }

        /// <summary>The gold "Force 1234 ^ 85" banner over the preview after a level-up: fades in with a pop, counts
        /// from the old total to the new one, shows the gain in green, sparkles, then fades out. Unscaled time.</summary>
        public void PlayForceGain(int oldForce, int newForce)
        {
            if (_force == null || _forceValue == null || newForce <= oldForce)
            {
                return;
            }

            _forceTween?.Kill();
            int shown = oldForce;
            _forceValue.text = oldForce.ToString();
            if (_forceDelta != null)
            {
                _forceDelta.text = (newForce - oldForce).ToString();
                _forceDelta.transform.parent.localScale = Vector3.zero;
            }

            _force.alpha = 0f;
            if (_forceContent != null)
            {
                _forceContent.localScale = Vector3.one * 0.8f;
            }

            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            sequence.Append(Fade(_force, 1f, 0.15f));
            if (_forceContent != null)
            {
                sequence.Join(_forceContent.DOScale(1.1f, 0.18f).SetEase(Ease.OutQuad));
                sequence.Append(_forceContent.DOScale(1f, 0.1f));
            }

            sequence.Append(DOTween.To(() => shown, v =>
            {
                shown = v;
                _forceValue.SetText("{0}", v); // no string per frame while it counts
            }, newForce, 0.45f).SetEase(Ease.OutQuad));
            if (_forceDelta != null)
            {
                sequence.Join(_forceDelta.transform.parent.DOScale(1f, 0.25f).SetEase(Ease.OutBack));
            }

            for (int i = 0; i < _forceSparkles.Length; i++)
            {
                RectTransform sparkle = _forceSparkles[i];
                if (sparkle == null)
                {
                    continue;
                }

                sparkle.localScale = Vector3.zero;
                sequence.Insert(0.1f + i * 0.09f, sparkle.DOScale(1f, 0.2f).SetEase(Ease.OutBack));
                sequence.Insert(0.35f + i * 0.09f, sparkle.DOScale(0f, 0.3f));
            }

            sequence.AppendInterval(0.55f);
            sequence.Append(Fade(_force, 0f, 0.3f));
            _forceTween = sequence;
        }
    }
}
