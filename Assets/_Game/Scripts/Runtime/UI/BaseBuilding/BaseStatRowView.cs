using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.BaseBuilding
{
    /// <summary>"[icon] Building power      100 +90" - one row of the popup's Upgrade Bonus or Extraction section.
    /// The delta is optional and only appears when the next level actually changes the value.</summary>
    public sealed class BaseStatRowView : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _label;

        [SerializeField]
        private TMP_Text _value;

        [SerializeField]
        [Tooltip("\"+90\" in green. Hidden when empty.")]
        private TMP_Text _delta;

        public void Bind(Sprite icon, string label, string value, string delta)
        {
            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.gameObject.SetActive(icon != null);
            }

            if (_label != null)
            {
                _label.text = label;
            }

            if (_value != null)
            {
                _value.text = value;
            }

            if (_delta != null)
            {
                bool show = !string.IsNullOrEmpty(delta);
                _delta.gameObject.SetActive(show);
                _delta.text = delta;
            }
        }
    }
}
