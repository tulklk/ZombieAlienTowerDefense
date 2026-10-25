using System;
using AlienDefense.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Defense
{
    /// <summary>One stat panel (icon, label, value, optional green next-level delta, optional "?" help). Dumb view.</summary>
    public sealed class TowerStatCellView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private TMP_Text _value;
        [SerializeField] private Button _helpButton;

        private string _tooltip;

        /// <summary>Raised with (this cell, tooltip text) when the "?" is tapped.</summary>
        public event Action<TowerStatCellView, string> HelpClicked;

        private void Awake()
        {
            if (_helpButton != null)
            {
                _helpButton.onClick.AddListener(HandleHelp);
            }
        }

        private void OnDestroy()
        {
            if (_helpButton != null)
            {
                _helpButton.onClick.RemoveListener(HandleHelp);
            }
        }

        public void Configure(DefenseStat stat)
        {
            if (_icon != null)
            {
                _icon.sprite = stat.Icon;
                _icon.enabled = stat.Icon != null;
            }

            if (_label != null)
            {
                _label.text = stat.Label;
            }

            if (_value != null)
            {
                // Current value white, next-level delta green - one TMP object, so the pair always lines up.
                _value.text = string.IsNullOrEmpty(stat.Delta) ? stat.Value : stat.Value + " <color=#5CFF4A>" + stat.Delta + "</color>";
            }

            _tooltip = stat.Tooltip;
            if (_helpButton != null)
            {
                _helpButton.gameObject.SetActive(!string.IsNullOrEmpty(_tooltip));
            }
        }

        public RectTransform ValueRect => _value != null ? _value.rectTransform : null;

        private void HandleHelp()
        {
            HelpClicked?.Invoke(this, _tooltip);
        }
    }
}
