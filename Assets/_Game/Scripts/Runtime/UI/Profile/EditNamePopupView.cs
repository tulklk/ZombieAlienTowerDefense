using System;
using AlienDefense.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Profile
{
    public sealed class EditNamePopupView : MonoBehaviour
    {
        [SerializeField]
        private GameObject _root;

        [SerializeField]
        private TMP_InputField _input;

        [SerializeField]
        private Button _cancelButton;

        [SerializeField]
        private Button _saveButton;

        [SerializeField]
        private TMP_Text _errorText;

        public event Action Cancelled;
        public event Action<string> Saved;

        public void Wire(GameObject root, TMP_InputField input, Button cancelButton, Button saveButton, TMP_Text errorText)
        {
            _root = root;
            _input = input;
            _cancelButton = cancelButton;
            _saveButton = saveButton;
            _errorText = errorText;
            BindButtons();
        }

        private void Awake()
        {
            BindButtons();
            Hide();
        }

        private void BindButtons()
        {
            if (_cancelButton != null)
            {
                _cancelButton.onClick.RemoveListener(InvokeCancelled);
                _cancelButton.onClick.AddListener(InvokeCancelled);
            }

            if (_saveButton != null)
            {
                _saveButton.onClick.RemoveListener(HandleSaveClicked);
                _saveButton.onClick.AddListener(HandleSaveClicked);
            }
        }

        private void InvokeCancelled() => Cancelled?.Invoke();

        public void Show(string currentName)
        {
            if (_root != null)
            {
                _root.SetActive(true);
            }

            if (_input != null)
            {
                _input.characterLimit = ProfileIdentityUtility.MaxDisplayNameLength;
                _input.text = currentName ?? string.Empty;
                _input.ActivateInputField();
            }

            SetError(null);
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }

            SetError(null);
        }

        public void SetError(string message)
        {
            if (_errorText == null)
            {
                return;
            }

            _errorText.text = message ?? string.Empty;
            _errorText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private void HandleSaveClicked()
        {
            Saved?.Invoke(_input != null ? _input.text : string.Empty);
        }
    }
}
