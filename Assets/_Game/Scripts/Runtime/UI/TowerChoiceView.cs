using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Immutable per-card data TowerChoicePresenter hands to TowerChoiceView.Show - never mutated, never
    /// read back (index is round-tripped through CardClicked instead).</summary>
    public readonly struct TowerCardData
    {
        public readonly string Name;
        public readonly string Description;
        public readonly string CostLabel;
        public readonly Sprite Icon;

        /// <summary>Full card artwork (TowerDefinition.ChoiceCardSprite). When the panel has a card Image for this
        /// slot the whole card is drawn from this one sprite - the name, art and description are painted into it,
        /// so the separate name/description/cost labels are left empty rather than printed twice.</summary>
        public readonly Sprite CardSprite;
        public readonly bool Affordable;

        public TowerCardData(string name, string description, string costLabel, Sprite icon, bool affordable, Sprite cardSprite = null)
        {
            Name = name;
            Description = description;
            CostLabel = costLabel;
            Icon = icon;
            Affordable = affordable;
            CardSprite = cardSprite;
        }
    }

    /// <summary>Pure display for the "Choose Tower" panel shown after a successful build-channel (see
    /// PlayerBuildNodeProximityController) - a bottom sheet with three portrait tower cards (this project's whole
    /// catalog is 3 towers). No business logic: never decides which towers to offer, never spends Energy or spawns
    /// anything - see TowerChoicePresenter.
    ///
    /// Every label is optional: with the card artwork assigned (_cardImages) the sprite already carries the name and
    /// description, so the panel built by TowerChoicePanelBuilder leaves the text fields empty. The older
    /// text-and-icon layout still works if those fields are wired instead.</summary>
    public sealed class TowerChoiceView : MonoBehaviour
    {
        [SerializeField]
        private GameObject _root;

        [SerializeField]
        [Tooltip("Optional. Full card artwork per slot - preferred over the icon + labels below.")]
        private Image[] _cardImages = new Image[3];

        [SerializeField]
        [Tooltip("Optional. Only used by the older layout where the card art carries no text.")]
        private TMP_Text[] _nameTexts = new TMP_Text[3];

        [SerializeField]
        [Tooltip("Optional.")]
        private TMP_Text[] _descriptionTexts = new TMP_Text[3];

        [SerializeField]
        [Tooltip("Optional.")]
        private TMP_Text[] _costTexts = new TMP_Text[3];

        [SerializeField]
        [Tooltip("Optional.")]
        private Image[] _iconImages = new Image[3];

        [SerializeField]
        private Button[] _cardButtons = new Button[3];

        [SerializeField]
        [Tooltip("Optional - the reference UI's 'Free' reshuffle button. This project's catalog is fixed at 3 " +
            "towers, so it stays hidden unless Show Reroll Button is ticked and something wires it up.")]
        private Button _rerollButton;

        [SerializeField]
        [Tooltip("The button re-reads the choices (their Energy affordability); the catalog is fixed at 3 towers, so " +
            "the three cards themselves cannot change.")]
        private bool _showRerollButton = true;

        [Header("Gems")]
        [SerializeField]
        [Tooltip("Optional. The player's saved Gem total shown on the bar above the sheet.")]
        private TMP_Text _gemText;

        [Header("Animation")]
        [SerializeField]
        [Tooltip("Optional. The bottom sheet that slides up; leave empty for no animation.")]
        private RectTransform _panelRect;

        [SerializeField]
        [Tooltip("Optional. Faded in with the slide.")]
        private CanvasGroup _panelGroup;

        [SerializeField, Range(0f, 240f)]
        private float _slideDistance = 110f;

        [SerializeField, Range(0.05f, 0.6f)]
        private float _openDuration = 0.24f;

        [SerializeField, Range(0.05f, 0.4f)]
        private float _closeDuration = 0.16f;

        public event Action<int> CardClicked;

        /// <summary>Raised by the Free button; the presenter decides what refreshing means.</summary>
        public event Action RerollClicked;

        private Tween _panelTween;
        private float _panelRestY;
        private bool _panelRestCaptured;

        private void Awake()
        {
            for (int i = 0; i < _cardButtons.Length; i++)
            {
                if (_cardButtons[i] == null)
                {
                    continue;
                }

                int capturedIndex = i;
                _cardButtons[i].onClick.AddListener(() => CardClicked?.Invoke(capturedIndex));
            }

            if (_rerollButton != null)
            {
                _rerollButton.gameObject.SetActive(_showRerollButton);
                _rerollButton.onClick.AddListener(() => RerollClicked?.Invoke());
            }

            CapturePanelRest();
        }

        public void Show(TowerCardData[] cards)
        {
            if (_root != null)
            {
                _root.SetActive(true);
            }

            for (int i = 0; i < _cardButtons.Length; i++)
            {
                bool hasCard = cards != null && i < cards.Length && IsUsable(cards[i]);

                if (_cardButtons[i] != null)
                {
                    _cardButtons[i].gameObject.SetActive(hasCard);
                    _cardButtons[i].interactable = hasCard && cards[i].Affordable;
                    _cardButtons[i].transform.localScale = Vector3.one; // a press that never got its pointer-up
                }

                if (!hasCard)
                {
                    continue;
                }

                TowerCardData card = cards[i];
                SetSprite(Element(_cardImages, i), card.CardSprite);
                SetSprite(Element(_iconImages, i), card.Icon);
                SetText(Element(_nameTexts, i), card.Name);
                SetText(Element(_descriptionTexts, i), card.Description);
                SetText(Element(_costTexts, i), card.CostLabel);

                // An unaffordable tower reads as greyed out rather than simply dead.
                Image art = Element(_cardImages, i);
                if (art != null)
                {
                    art.color = card.Affordable ? Color.white : new Color(0.55f, 0.6f, 0.68f, 1f);
                }
            }

            PlayOpen();
        }

        /// <summary>The Gem total on the top bar, already formatted by the caller.</summary>
        public void SetGems(string formattedAmount)
        {
            if (_gemText != null)
            {
                _gemText.text = formattedAmount;
            }
        }

        public void Hide()
        {
            if (_root == null || !_root.activeSelf)
            {
                return;
            }

            if (_panelRect == null || _closeDuration <= 0f || !gameObject.activeInHierarchy)
            {
                KillPanelTween();
                RestorePanel();
                _root.SetActive(false);
                return;
            }

            // Scaled time is paused while a choice is pending (GameSpeed), so the panel animates unscaled.
            KillPanelTween();
            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            sequence.Join(PanelSlide(_panelRestY - _slideDistance * 0.75f, _closeDuration).SetEase(Ease.InQuad));
            if (_panelGroup != null)
            {
                sequence.Join(DOTween.To(() => _panelGroup.alpha, value => _panelGroup.alpha = value, 0f, _closeDuration));
            }

            sequence.OnComplete(() =>
            {
                RestorePanel();
                _root.SetActive(false);
            });
            _panelTween = sequence;
        }

        /// <summary>Slides the sheet up from just below its resting place and fades it in.</summary>
        private void PlayOpen()
        {
            if (_panelRect == null)
            {
                return;
            }

            CapturePanelRest();
            KillPanelTween();

            _panelRect.anchoredPosition = new Vector2(_panelRect.anchoredPosition.x, _panelRestY - _slideDistance);
            if (_panelGroup != null)
            {
                _panelGroup.alpha = 0f;
            }

            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            sequence.Join(PanelSlide(_panelRestY, _openDuration).SetEase(Ease.OutCubic));
            if (_panelGroup != null)
            {
                sequence.Join(DOTween.To(() => _panelGroup.alpha, value => _panelGroup.alpha = value, 1f, _openDuration));
            }

            _panelTween = sequence;
        }

        /// <summary>DOTween's UI module shortcuts are not generated in this project, so the slide goes through
        /// DOTween.To on the anchored position, like the rest of the codebase.</summary>
        private Tween PanelSlide(float targetY, float duration)
        {
            return DOTween.To(() => _panelRect.anchoredPosition.y,
                value => _panelRect.anchoredPosition = new Vector2(_panelRect.anchoredPosition.x, value),
                targetY, duration);
        }

        private void CapturePanelRest()
        {
            if (_panelRestCaptured || _panelRect == null)
            {
                return;
            }

            _panelRestY = _panelRect.anchoredPosition.y;
            _panelRestCaptured = true;
        }

        private void RestorePanel()
        {
            if (_panelRect != null && _panelRestCaptured)
            {
                _panelRect.anchoredPosition = new Vector2(_panelRect.anchoredPosition.x, _panelRestY);
            }

            if (_panelGroup != null)
            {
                _panelGroup.alpha = 1f;
            }
        }

        private void KillPanelTween()
        {
            _panelTween?.Kill();
            _panelTween = null;
        }

        /// <summary>A slot is only shown when there is something to draw in it.</summary>
        private static bool IsUsable(TowerCardData card)
        {
            return card.CardSprite != null || card.Icon != null || !string.IsNullOrEmpty(card.Name);
        }

        private static T Element<T>(T[] array, int index) where T : Component
        {
            return array != null && index < array.Length ? array[index] : null;
        }

        private static void SetSprite(Image image, Sprite sprite)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        private void OnDestroy()
        {
            KillPanelTween();
            for (int i = 0; i < _cardButtons.Length; i++)
            {
                _cardButtons[i]?.onClick.RemoveAllListeners();
            }

            _rerollButton?.onClick.RemoveAllListeners();
        }
    }
}
