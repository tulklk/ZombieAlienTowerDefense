using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Defense
{
    /// <summary>One tower's unlock card. Dumb view: displays whatever it's configured with and forwards Unlock
    /// clicks — never reads/writes save data, never computes cost itself.</summary>
    public sealed class TowerUnlockCardView : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _nameText;

        [SerializeField]
        private TMP_Text _costText;

        [SerializeField]
        private Button _unlockButton;

        [SerializeField]
        [Tooltip("Optional. Shown instead of the Unlock button once owned.")]
        private GameObject _unlockedLabel;

        public event Action Clicked;

        private void Awake()
        {
            if (_unlockButton != null)
            {
                _unlockButton.onClick.AddListener(HandleClicked);
            }
        }

        public void Configure(string displayName, Sprite icon, int unlockCost, bool isUnlocked, bool canAfford)
        {
            if (_nameText != null)
            {
                _nameText.text = displayName;
            }

            if (_icon != null && icon != null)
            {
                _icon.sprite = icon;
            }

            if (_costText != null)
            {
                _costText.text = unlockCost.ToString();
            }

            if (_unlockedLabel != null)
            {
                _unlockedLabel.SetActive(isUnlocked);
            }

            if (_unlockButton != null)
            {
                _unlockButton.gameObject.SetActive(!isUnlocked);
                _unlockButton.interactable = canAfford;
            }
        }

        private void HandleClicked()
        {
            Clicked?.Invoke();
        }

        private void OnDestroy()
        {
            if (_unlockButton != null)
            {
                _unlockButton.onClick.RemoveListener(HandleClicked);
            }
        }
    }
}
