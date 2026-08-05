using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>One build-bar button: icon, cost, affordable/selected visual state. No business logic.</summary>
    public sealed class TowerBuildButtonView : MonoBehaviour
    {
        [SerializeField]
        private Button _button;

        [SerializeField]
        [Tooltip("Optional.")]
        private Image _icon;

        [SerializeField]
        private TMP_Text _costText;

        [SerializeField]
        [Tooltip("Optional.")]
        private GameObject _selectedIndicator;

        /// <summary>Fired with this view as sender, so the presenter can map it back to a TowerDefinition.</summary>
        public event Action<TowerBuildButtonView> Clicked;

        private void Awake()
        {
            if (_button != null)
            {
                _button.onClick.AddListener(HandleClicked);
            }
        }

        public void SetIcon(Sprite icon)
        {
            if (_icon != null)
            {
                _icon.sprite = icon;
            }
        }

        public void SetCost(int cost)
        {
            if (_costText != null)
            {
                _costText.text = cost.ToString();
            }
        }

        public void SetAffordable(bool affordable)
        {
            if (_button != null)
            {
                _button.interactable = affordable;
            }
        }

        public void SetSelected(bool selected)
        {
            if (_selectedIndicator != null)
            {
                _selectedIndicator.SetActive(selected);
            }
        }

        private void HandleClicked()
        {
            Clicked?.Invoke(this);
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
