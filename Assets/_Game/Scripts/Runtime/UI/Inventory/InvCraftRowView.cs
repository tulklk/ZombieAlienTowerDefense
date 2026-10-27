using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Inventory
{
    /// <summary>One recipe in the Craft popup: result card, name, the materials needed (owned / required, red when
    /// short), the coin cost and a Craft button that is green when affordable and grey otherwise.</summary>
    public sealed class InvCraftRowView : MonoBehaviour
    {
        [SerializeField]
        private InvItemCardView _result;

        [SerializeField]
        private TMP_Text _name;

        [SerializeField]
        private TMP_Text _requirements;

        [SerializeField]
        private Button _button;

        [SerializeField]
        private Image _buttonImage;

        [SerializeField]
        private TMP_Text _buttonLabel;

        private bool _wired;

        public InvItemCardView Result => _result;
        public string Key { get; set; }

        public Action<InvCraftRowView> ClickHandler { get; set; }

        public void Bind(string name, string requirements, bool affordable, string buttonText)
        {
            if (!_wired && _button != null)
            {
                _wired = true;
                _button.onClick.AddListener(() => ClickHandler?.Invoke(this));
            }

            if (_name != null) _name.text = name;
            if (_requirements != null) _requirements.text = requirements;
            if (_button != null) _button.interactable = affordable;
            if (_buttonImage != null) _buttonImage.color = affordable ? Color.white : new Color(0.55f, 0.58f, 0.62f, 1f);
            if (_buttonLabel != null) _buttonLabel.text = buttonText;
        }
    }
}
