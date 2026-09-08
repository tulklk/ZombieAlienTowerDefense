using System;
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
        public readonly bool Affordable;

        public TowerCardData(string name, string description, string costLabel, Sprite icon, bool affordable)
        {
            Name = name;
            Description = description;
            CostLabel = costLabel;
            Icon = icon;
            Affordable = affordable;
        }
    }

    /// <summary>Pure display for the "Choose Tower" popup shown after a successful build-channel (see
    /// PlayerBuildNodeProximityController) - fixed 3 card slots (this project's whole catalog is 3 towers). No
    /// business logic: never decides which towers to offer, never spends Energy or spawns anything - see
    /// TowerChoicePresenter.</summary>
    public sealed class TowerChoiceView : MonoBehaviour
    {
        [SerializeField]
        private GameObject _root;

        [SerializeField]
        private TMP_Text[] _nameTexts = new TMP_Text[3];

        [SerializeField]
        private TMP_Text[] _descriptionTexts = new TMP_Text[3];

        [SerializeField]
        private TMP_Text[] _costTexts = new TMP_Text[3];

        [SerializeField]
        private Image[] _iconImages = new Image[3];

        [SerializeField]
        private Button[] _cardButtons = new Button[3];

        [SerializeField]
        [Tooltip("Optional - the reference UI's 'Free' reshuffle button. Present for visual parity only; this " +
            "project's catalog is fixed at 3 towers so there is nothing to actually reroll.")]
        private Button _rerollButton;

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

            if (_rerollButton != null)
            {
                _rerollButton.gameObject.SetActive(false); // nothing to reroll - see field tooltip
            }
        }

        public void Show(TowerCardData[] cards)
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
                    _cardButtons[i].interactable = hasCard && cards[i].Affordable;
                }

                if (!hasCard)
                {
                    continue;
                }

                TowerCardData card = cards[i];
                if (_nameTexts[i] != null) _nameTexts[i].text = card.Name;
                if (_descriptionTexts[i] != null) _descriptionTexts[i].text = card.Description;
                if (_costTexts[i] != null) _costTexts[i].text = card.CostLabel;
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
