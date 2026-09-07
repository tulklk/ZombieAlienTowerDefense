using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Immutable per-card data SkillChoicePresenter hands to SkillChoiceView.Show - never mutated,
    /// never read back by the presenter (index is round-tripped through CardClicked instead).</summary>
    public readonly struct SkillCardData
    {
        public readonly string Name;
        public readonly string Description;
        public readonly string RankLabel;
        public readonly Sprite Icon;

        public SkillCardData(string name, string description, string rankLabel, Sprite icon)
        {
            Name = name;
            Description = description;
            RankLabel = rankLabel;
            Icon = icon;
        }
    }

    /// <summary>Pure display for the "pick 1 of 3" level-up popup - always exactly 3 fixed card slots (never a
    /// dynamic list; the offer size is fixed by spec), each with a name/description/rank-label/icon and a
    /// Button. No business logic: never picks the offer, never applies an upgrade - see SkillChoicePresenter.</summary>
    public sealed class SkillChoiceView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The whole popup root - shown/hidden as a unit.")]
        private GameObject _root;

        [SerializeField]
        private TMP_Text[] _nameTexts = new TMP_Text[3];

        [SerializeField]
        private TMP_Text[] _descriptionTexts = new TMP_Text[3];

        [SerializeField]
        private TMP_Text[] _rankLabelTexts = new TMP_Text[3];

        [SerializeField]
        private Image[] _iconImages = new Image[3];

        [SerializeField]
        private Button[] _cardButtons = new Button[3];

        /// <summary>Fired with the clicked card's index (0-2) - SkillChoicePresenter maps this back to which
        /// SkillType it actually offered at that slot.</summary>
        public event Action<int> CardClicked;

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
        }

        public void Show(SkillCardData[] cards)
        {
            if (_root != null)
            {
                _root.SetActive(true);
            }

            for (int i = 0; i < _cardButtons.Length; i++)
            {
                bool hasCard = cards != null && i < cards.Length;

                if (_cardButtons[i] != null)
                {
                    _cardButtons[i].gameObject.SetActive(hasCard);
                }

                if (!hasCard)
                {
                    continue;
                }

                SkillCardData card = cards[i];
                if (_nameTexts[i] != null) _nameTexts[i].text = card.Name;
                if (_descriptionTexts[i] != null) _descriptionTexts[i].text = card.Description;
                if (_rankLabelTexts[i] != null) _rankLabelTexts[i].text = card.RankLabel;
                if (_iconImages[i] != null)
                {
                    _iconImages[i].sprite = card.Icon;
                    _iconImages[i].enabled = card.Icon != null;
                }
            }
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _cardButtons.Length; i++)
            {
                _cardButtons[i]?.onClick.RemoveAllListeners();
            }
        }
    }
}
