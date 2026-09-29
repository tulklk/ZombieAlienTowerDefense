using System.Globalization;
using TMPro;
using UnityEngine;

namespace AlienDefense.UI.Inventory
{
    /// <summary>One "Weapon Power  200 > 400 ^" line on the craft screen.
    ///
    /// The row decides its own "did this go up?" styling from the two numbers it is given, so a stat that happens
    /// not to change between tiers simply shows no arrow and no green - the panel above does not have to special
    /// case it.</summary>
    public sealed class StatComparisonRowView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _nameText;

        [SerializeField]
        private TMP_Text _currentValueText;

        [SerializeField]
        private TMP_Text _nextValueText;

        [SerializeField]
        [Tooltip("The '>' between the two values. Hidden when there is no next tier.")]
        private GameObject _arrow;

        [SerializeField]
        [Tooltip("The green up-arrow badge after the next value.")]
        private GameObject _increaseBadge;

        [SerializeField]
        private Color _increaseColor = new Color(0.36f, 0.93f, 0.36f, 1f);

        [SerializeField]
        private Color _neutralColor = Color.white;

        /// <summary>Draws the row. Pass hasNext = false for a maxed piece: the next column and the arrow vanish
        /// and only the current value is shown.</summary>
        public void Bind(string statName, float current, float next, bool hasNext, bool isPercent)
        {
            if (_nameText != null)
            {
                _nameText.text = statName;
            }

            if (_currentValueText != null)
            {
                _currentValueText.text = FormatValue(current, isPercent);
            }

            bool increased = hasNext && next > current;

            if (_arrow != null)
            {
                _arrow.SetActive(hasNext);
            }

            if (_nextValueText != null)
            {
                _nextValueText.gameObject.SetActive(hasNext);
                if (hasNext)
                {
                    _nextValueText.text = FormatValue(next, isPercent);
                    _nextValueText.color = increased ? _increaseColor : _neutralColor;
                }
            }

            if (_increaseBadge != null)
            {
                _increaseBadge.SetActive(increased);
            }
        }

        /// <summary>Whole numbers print bare (200, not 200.0); a fractional value keeps one decimal so a small
        /// tuning difference is not rounded into invisibility.</summary>
        private static string FormatValue(float value, bool isPercent)
        {
            string number = Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.#", CultureInfo.InvariantCulture);

            return isPercent ? number + "%" : number;
        }
    }
}
