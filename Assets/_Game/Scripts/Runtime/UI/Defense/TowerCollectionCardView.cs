using System;
using AlienDefense.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Defense
{
    /// <summary>One card of the Tower Collection grid. States: unlocked (level strip, full-colour art), locked
    /// (dimmed art, padlock, a label saying how it is obtained) and the special UFO Base look. Dumb view.</summary>
    public sealed class TowerCollectionCardView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private Image _towerIcon;
        [SerializeField] private GameObject _levelStrip;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private GameObject _upgradeArrow;
        [SerializeField] private Image _typeIcon;
        [SerializeField] private GameObject _lockIcon;
        [SerializeField] private TMP_Text _lockLabel;

        [Header("Backgrounds")]
        [SerializeField] private Sprite _normalBackground;
        [SerializeField] private Sprite _specialBackground;
        [SerializeField] private Sprite _lockedBackground;
        [SerializeField] private Sprite _eventBackground;

        private int _index;

        public event Action<int> Clicked;

        private void Awake()
        {
            if (_button != null)
            {
                _button.onClick.AddListener(HandleClick);
            }
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClick);
            }
        }

        public void Configure(int index, DefenseEntry entry)
        {
            _index = index;
            bool unlocked = entry.IsUnlocked;

            if (_background != null)
            {
                _background.sprite = !unlocked ? _lockedBackground : entry.IsBase ? _specialBackground : _normalBackground;
            }

            if (_towerIcon != null)
            {
                _towerIcon.sprite = entry.Icon;
                _towerIcon.enabled = entry.Icon != null;
                _towerIcon.color = unlocked ? Color.white : new Color(0.78f, 0.88f, 1f, 0.72f);
            }

            if (_levelStrip != null)
            {
                _levelStrip.SetActive(unlocked);
            }

            if (_levelText != null)
            {
                _levelText.text = entry.IsMaxLevel ? DefenseStrings.Max : string.Format(DefenseStrings.LevelFormat, entry.LevelIndex + 1);
            }

            if (_upgradeArrow != null)
            {
                // The arrow means "you can level this up right now".
                _upgradeArrow.SetActive(unlocked && entry.UpgradeCheck == Towers.MetaUpgradeResult.Success);
            }

            if (_typeIcon != null)
            {
                _typeIcon.sprite = entry.TypeIcon;
                _typeIcon.transform.parent.gameObject.SetActive(entry.TypeIcon != null);
            }

            if (_lockIcon != null)
            {
                _lockIcon.SetActive(!unlocked);
            }

            if (_lockLabel != null)
            {
                _lockLabel.gameObject.SetActive(!unlocked);
                _lockLabel.text = DefenseStrings.Unlock + "\n" + CurrencyFormatter.Format(entry.UnlockCost);
            }
        }

        /// <summary>An announced tower: its picture dimmed behind a padlock and a line saying how it will be obtained.
        /// It reports no index, so a tap opens nothing.</summary>
        public void ConfigureComingSoon(string label, Sprite icon, Sprite typeIcon, bool isEvent)
        {
            _index = -1;
            if (_background != null)
            {
                _background.sprite = isEvent ? _eventBackground : _lockedBackground;
            }

            if (_towerIcon != null)
            {
                _towerIcon.sprite = icon;
                _towerIcon.enabled = icon != null;
                _towerIcon.color = isEvent ? new Color(1f, 0.72f, 0.9f, 0.6f) : new Color(0.7f, 0.86f, 1f, 0.55f);
            }

            if (_levelStrip != null)
            {
                _levelStrip.SetActive(false);
            }

            if (_typeIcon != null)
            {
                _typeIcon.sprite = typeIcon;
                _typeIcon.transform.parent.gameObject.SetActive(typeIcon != null);
            }

            if (_lockIcon != null)
            {
                _lockIcon.SetActive(true);
            }

            if (_lockLabel != null)
            {
                _lockLabel.gameObject.SetActive(true);
                _lockLabel.text = label;
            }
        }

        private void HandleClick()
        {
            Clicked?.Invoke(_index);
        }
    }
}
