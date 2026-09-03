using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Large center-bottom CTA. Dumb view: forwards clicks, never loads a scene itself. Swaps its own
    /// background sprite + label between "Start" (StartBtn, never completed this level before) and "Play"
    /// (PlayBtn, already completed it at least once) — purely cosmetic, matches the reference composition.
    /// The energy-cost slot (icon + amount) is shown by MainMenuLevelSelectionPresenter as a flat cosmetic
    /// "cost 5" display — no lobby/play-stamina system exists in this project, so nothing is ever actually
    /// deducted on click; see ShowEnergyCost's own doc comment.</summary>
    public sealed class PlayButtonView : MonoBehaviour
    {
        [SerializeField]
        private Button _button;

        [SerializeField]
        private TMP_Text _label;

        [SerializeField]
        [Tooltip("The button's own background — swapped between the Start/Play sprites below.")]
        private Image _backgroundImage;

        [SerializeField]
        private Sprite _startSprite;

        [SerializeField]
        private Sprite _playSprite;

        [SerializeField]
        [Tooltip("Optional. Hidden unless ShowEnergyCost is called — no lobby stamina system exists yet, this " +
            "is a flat cosmetic display only, nothing is ever actually spent on click.")]
        private GameObject _energyCostRoot;

        [SerializeField]
        private TMP_Text _energyCostText;

        public event Action Clicked;

        private void Awake()
        {
            if (_button != null)
            {
                _button.onClick.AddListener(HandleClicked);
            }

            if (_energyCostRoot != null)
            {
                _energyCostRoot.SetActive(false);
            }
        }

        public void SetLabel(string label)
        {
            if (_label != null)
            {
                _label.text = label;
            }
        }

        /// <summary>true = this level has been completed before ("Play" + PlayBtn sprite), false = never
        /// completed yet ("Start" + StartBtn sprite). Purely visual — doesn't touch save data.</summary>
        public void SetPlayedBefore(bool hasCompletedBefore)
        {
            SetLabel(hasCompletedBefore ? "Play" : "Start");

            if (_backgroundImage != null)
            {
                Sprite target = hasCompletedBefore ? _playSprite : _startSprite;
                if (target != null)
                {
                    _backgroundImage.sprite = target;
                }
            }
        }

        public void SetInteractable(bool interactable)
        {
            if (_button != null)
            {
                _button.interactable = interactable;
            }
        }

        /// <summary>Only ever a flat cosmetic "cost" display next to the lightning icon — no lobby-stamina
        /// system exists in this project, so nothing is actually deducted when Play is clicked.</summary>
        public void ShowEnergyCost(int cost)
        {
            if (_energyCostRoot != null)
            {
                _energyCostRoot.SetActive(true);
            }

            if (_energyCostText != null)
            {
                _energyCostText.text = cost.ToString();
            }
        }

        private void HandleClicked()
        {
            Clicked?.Invoke();
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClicked);
            }
        }
    }
}
