using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Large center-bottom CTA. Dumb view: forwards clicks, never loads a scene itself. The energy-cost
    /// slot is kept but hidden by default (see MainMenuLevelSelectionPresenter's doc comment) — this project has
    /// no lobby/play-stamina system distinct from the in-level UFO tractor Energy, so Play never actually spends
    /// anything on click; ShowEnergyCost exists only for a future real stamina system to opt into.</summary>
    public sealed class PlayButtonView : MonoBehaviour
    {
        [SerializeField]
        private Button _button;

        [SerializeField]
        private TMP_Text _label;

        [SerializeField]
        [Tooltip("Optional. Hidden unless ShowEnergyCost is called — no lobby stamina system exists yet.")]
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

        public void SetInteractable(bool interactable)
        {
            if (_button != null)
            {
                _button.interactable = interactable;
            }
        }

        /// <summary>Only call once a real lobby-stamina system exists and Play genuinely costs something.</summary>
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
