using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Inventory
{
    /// <summary>One of the three Equipment/Artifacts/Materials buttons. Styling only - which tab it selects is
    /// authored in the Inspector and the presenter owns the actual switch.</summary>
    public sealed class InventoryTabButtonView : MonoBehaviour
    {
        [SerializeField]
        private InventoryTab _tab = InventoryTab.Equipment;

        [SerializeField]
        private Button _button;

        [SerializeField]
        private Image _background;

        [SerializeField]
        [Tooltip("Green highlight used for the active tab.")]
        private Sprite _selectedSprite;

        [SerializeField]
        [Tooltip("The existing blue style, kept for inactive tabs.")]
        private Sprite _unselectedSprite;

        [SerializeField]
        private TMP_Text _label;

        [SerializeField]
        private Color _selectedLabelColor = Color.white;

        [SerializeField]
        private Color _unselectedLabelColor = new Color(0.78f, 0.88f, 1f, 1f);

        [SerializeField]
        [Tooltip("Optional. The little green arrow badge shown when this tab has something new.")]
        private GameObject _notificationBadge;

        private System.Action<InventoryTab> _clicked;

        public InventoryTab Tab => _tab;

        private void Awake()
        {
            if (_button != null)
            {
                _button.onClick.AddListener(HandleClicked);
            }
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClicked);
            }
        }

        public void Configure(System.Action<InventoryTab> clicked)
        {
            _clicked = clicked;
        }

        public void SetSelected(bool selected)
        {
            if (_background != null)
            {
                Sprite sprite = selected ? _selectedSprite : _unselectedSprite;
                if (sprite != null)
                {
                    _background.sprite = sprite;
                }
            }

            if (_label != null)
            {
                _label.color = selected ? _selectedLabelColor : _unselectedLabelColor;
            }
        }

        public void SetNotification(bool visible)
        {
            if (_notificationBadge != null)
            {
                _notificationBadge.SetActive(visible);
            }
        }

        private void HandleClicked()
        {
            _clicked?.Invoke(_tab);
        }
    }
}
