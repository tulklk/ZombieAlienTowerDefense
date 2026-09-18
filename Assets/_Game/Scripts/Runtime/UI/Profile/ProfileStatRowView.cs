using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Profile
{
    public sealed class ProfileStatRowView : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _labelText;

        [SerializeField]
        private TMP_Text _valueText;

        public void Wire(Image icon, TMP_Text labelText, TMP_Text valueText)
        {
            _icon = icon;
            _labelText = labelText;
            _valueText = valueText;
        }

        public void Set(string label, string value, Sprite icon = null)
        {
            if (_labelText != null)
            {
                _labelText.text = label ?? string.Empty;
            }

            if (_valueText != null)
            {
                _valueText.text = value ?? "0";
            }

            // Only replace sprite when explicitly provided — SetStats omits icons and must not disable them.
            if (_icon != null && icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = true;
            }
        }
    }
}
