using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Upgrade
{
    /// <summary>One tower's permanent-upgrade card. Dumb view: displays whatever it's configured with and
    /// forwards Upgrade clicks — never reads/writes save data, never computes cost itself.</summary>
    public sealed class TowerUpgradeCardView : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _nameText;

        [SerializeField]
        private TMP_Text _levelText;

        [SerializeField]
        private TMP_Text _costText;

        [SerializeField]
        private Button _upgradeButton;

        [SerializeField]
        [Tooltip("Optional. Shown when the tower isn't unlocked yet.")]
        private GameObject _lockedOverlay;

        public event Action Clicked;

        private void Awake()
        {
            if (_upgradeButton != null)
            {
                _upgradeButton.onClick.AddListener(HandleClicked);
            }
        }

        public void Configure(string displayName, Sprite icon, int currentLevelNumber, int maxLevelNumber, int nextLevelCost, bool isMaxLevel, bool isLocked, bool canAfford)
        {
            if (_nameText != null)
            {
                _nameText.text = displayName;
            }

            if (_icon != null && icon != null)
            {
                _icon.sprite = icon;
            }

            if (_levelText != null)
            {
                _levelText.text = $"Cấp {currentLevelNumber}/{maxLevelNumber}";
            }

            if (_costText != null)
            {
                _costText.text = isMaxLevel ? "Tối đa" : nextLevelCost.ToString();
            }

            if (_lockedOverlay != null)
            {
                _lockedOverlay.SetActive(isLocked);
            }

            if (_upgradeButton != null)
            {
                _upgradeButton.interactable = !isLocked && !isMaxLevel && canAfford;
            }
        }

        private void HandleClicked()
        {
            Clicked?.Invoke();
        }

        private void OnDestroy()
        {
            if (_upgradeButton != null)
            {
                _upgradeButton.onClick.RemoveListener(HandleClicked);
            }
        }
    }
}
