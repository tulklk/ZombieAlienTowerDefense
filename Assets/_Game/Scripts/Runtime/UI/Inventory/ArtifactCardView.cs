using AlienDefense.Meta;
using AlienDefense.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Inventory
{
    /// <summary>One artifact stack in the Artifacts grid. An artifact has no level and no equipped state in this
    /// game, so the card shows rarity and how many copies are held - which is exactly what the player needs in
    /// order to judge whether a merge is close.</summary>
    public sealed class ArtifactCardView : MonoBehaviour
    {
        [SerializeField]
        private Image _rarityFrame;

        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _amountText;

        [SerializeField]
        [Tooltip("Shown when this stack already has enough copies to merge - the same green cue the equipment " +
            "cards use for an available upgrade.")]
        private GameObject _mergeReadyBadge;

        [SerializeField]
        private Button _button;

        private string _artifactId;
        private System.Action<string> _clicked;

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

        public void Bind(ArtifactEntry entry, RarityPalette palette, System.Action<string> clicked)
        {
            _clicked = clicked;
            _artifactId = entry.Definition != null ? entry.Definition.Id : null;

            if (_icon != null)
            {
                _icon.sprite = entry.Definition != null ? entry.Definition.Icon : null;
                _icon.enabled = _icon.sprite != null;
            }

            if (_amountText != null)
            {
                _amountText.text = CurrencyFormatter.Format(entry.Amount);
            }

            if (_mergeReadyBadge != null)
            {
                bool ready = entry.Definition != null &&
                             entry.Definition.CanMergeFrom(entry.Rarity) &&
                             entry.Amount >= entry.Definition.MergeInputCount;
                _mergeReadyBadge.SetActive(ready);
            }

            InventoryItemView.ApplyRarity(_rarityFrame, palette, entry.Rarity);
        }

        private void HandleClicked()
        {
            _clicked?.Invoke(_artifactId);
        }
    }
}
