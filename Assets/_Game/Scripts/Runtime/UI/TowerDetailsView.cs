using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Displays one tower's identity, stats, and upgrade/sell affordances. No business logic.</summary>
    public sealed class TowerDetailsView : MonoBehaviour
    {
        [SerializeField]
        private GameObject _root;

        [SerializeField]
        [Tooltip("Optional.")]
        private Image _icon;

        [SerializeField]
        private TMP_Text _nameText;

        [SerializeField]
        private TMP_Text _levelText;

        [SerializeField]
        private TMP_Text _damageText;

        [SerializeField]
        private TMP_Text _rangeText;

        [SerializeField]
        private TMP_Text _attackSpeedText;

        [SerializeField]
        private TMP_Text _targetingText;

        [SerializeField]
        private TMP_Text _upgradeCostText;

        [SerializeField]
        private TMP_Text _sellValueText;

        [SerializeField]
        private Button _upgradeButton;

        [SerializeField]
        private Button _sellButton;

        [SerializeField]
        private Button _closeButton;

        public event Action UpgradeClicked;
        public event Action SellClicked;
        public event Action CloseClicked;

        private void Awake()
        {
            if (_upgradeButton != null)
            {
                _upgradeButton.onClick.AddListener(HandleUpgradeClicked);
            }

            if (_sellButton != null)
            {
                _sellButton.onClick.AddListener(HandleSellClicked);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(HandleCloseClicked);
            }
        }

        public void SetIdentity(Sprite icon, string displayName)
        {
            if (_icon != null)
            {
                _icon.sprite = icon;
            }

            if (_nameText != null)
            {
                _nameText.text = displayName;
            }
        }

        public void SetLevel(int levelNumber)
        {
            if (_levelText != null)
            {
                _levelText.text = "Lv " + levelNumber;
            }
        }

        public void SetStats(float damage, float range, float attacksPerSecond, string targetingModeLabel)
        {
            if (_damageText != null)
            {
                _damageText.text = damage.ToString("0");
            }

            if (_rangeText != null)
            {
                _rangeText.text = range.ToString("0.0");
            }

            if (_attackSpeedText != null)
            {
                _attackSpeedText.text = attacksPerSecond.ToString("0.0") + "/s";
            }

            if (_targetingText != null)
            {
                _targetingText.text = targetingModeLabel;
            }
        }

        public void SetUpgradeState(bool isMaxLevel, int upgradeCost, bool canAfford)
        {
            if (_upgradeCostText != null)
            {
                _upgradeCostText.text = isMaxLevel ? "MAX" : upgradeCost.ToString();
            }

            if (_upgradeButton != null)
            {
                _upgradeButton.interactable = !isMaxLevel && canAfford;
            }
        }

        public void SetSellValue(int sellValue)
        {
            if (_sellValueText != null)
            {
                _sellValueText.text = sellValue.ToString();
            }
        }

        public void Show()
        {
            if (_root != null)
            {
                _root.SetActive(true);
            }
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        private void HandleUpgradeClicked()
        {
            UpgradeClicked?.Invoke();
        }

        private void HandleSellClicked()
        {
            SellClicked?.Invoke();
        }

        private void HandleCloseClicked()
        {
            CloseClicked?.Invoke();
        }

        private void OnDestroy()
        {
            if (_upgradeButton != null)
            {
                _upgradeButton.onClick.RemoveListener(HandleUpgradeClicked);
            }

            if (_sellButton != null)
            {
                _sellButton.onClick.RemoveListener(HandleSellClicked);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(HandleCloseClicked);
            }
        }
    }
}
