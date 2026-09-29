using AlienDefense.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Inventory
{
    /// <summary>One icon on the equipment category bar.
    ///
    /// "All" is modelled as Is All rather than as a fake slot value, so EquipmentSlotType never needs a sentinel
    /// member that would then have to be filtered out everywhere else.</summary>
    public sealed class EquipmentCategoryButtonView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Tick this on the leftmost button. Its Slot is then ignored and the grid shows everything.")]
        private bool _isAll;

        [SerializeField]
        private EquipmentSlotType _slot = EquipmentSlotType.Controls;

        [SerializeField]
        private Button _button;

        [SerializeField]
        [Tooltip("Swapped between the selected and unselected background sprites.")]
        private Image _background;

        [SerializeField]
        private Sprite _selectedSprite;

        [SerializeField]
        private Sprite _unselectedSprite;

        [SerializeField]
        private Image _icon;

        [SerializeField]
        private Color _selectedIconColor = Color.white;

        [SerializeField]
        private Color _unselectedIconColor = new Color(0.75f, 0.85f, 1f, 1f);

        private System.Action<EquipmentCategoryButtonView> _clicked;

        public bool IsAll => _isAll;
        public EquipmentSlotType Slot => _slot;

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

        public void Configure(System.Action<EquipmentCategoryButtonView> clicked)
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

            if (_icon != null)
            {
                _icon.color = selected ? _selectedIconColor : _unselectedIconColor;
            }
        }

        private void HandleClicked()
        {
            _clicked?.Invoke(this);
        }
    }
}
