using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Large center-bottom CTA. Dumb view: forwards clicks, never loads a scene itself. Swaps its own
    /// background sprite + label between "Start" (StartBtn, never completed this level before) and "Play"
    /// (PlayBtn, already completed it at least once) — purely cosmetic, matches the reference composition.
    /// The energy-cost slot (icon + amount) shows what a start costs; MainMenuLevelSelectionPresenter spends it
    /// through PlayEnergyService on click, and calls PlayDenied when the player cannot afford it.</summary>
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
        [Tooltip("Optional. Hidden unless ShowEnergyCost is called.")]
        private GameObject _energyCostRoot;

        [SerializeField]
        private TMP_Text _energyCostText;

        public event Action Clicked;

        private Tween _deniedTween;

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

        /// <summary>Hides the whole button (e.g. the selected level is still locked — there is nothing to
        /// Start/Play yet) instead of showing a non-interactable button that still looks clickable.</summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        /// <summary>The energy a start costs, next to the lightning icon. Display only - the spend happens in the
        /// presenter.</summary>
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

        /// <summary>Not enough energy: a short sideways shake, no scene load.</summary>
        public void PlayDenied()
        {
            _deniedTween?.Kill(true);
            _deniedTween = transform.DOShakePosition(0.35f, new Vector3(18f, 0f, 0f), 18, 0f, false, true)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void HandleClicked()
        {
            Clicked?.Invoke();
        }

        private void OnDestroy()
        {
            _deniedTween?.Kill();
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClicked);
            }
        }
    }
}
