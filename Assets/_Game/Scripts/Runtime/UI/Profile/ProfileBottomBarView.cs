using System;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Profile
{
    /// <summary>3-slot chrome shown only while Profile is open: Back (MainMenu), Profile, Settings.</summary>
    public sealed class ProfileBottomBarView : MonoBehaviour
    {
        [SerializeField]
        private GameObject _root;

        [SerializeField]
        private Button _backButton;

        [SerializeField]
        private Button _profileButton;

        [SerializeField]
        private Button _settingsButton;

        [SerializeField]
        private Image _profileTile;

        [SerializeField]
        private Image _settingsTile;

        [SerializeField]
        private Color _selectedColor = new Color(0.95f, 0.85f, 0.2f, 1f);

        [SerializeField]
        private Color _idleColor = new Color(0.2f, 0.45f, 0.85f, 1f);

        public event Action BackClicked;
        public event Action ProfileClicked;
        public event Action SettingsClicked;

        public void Wire(GameObject root, Button back, Button profile, Button settings, Image profileTile, Image settingsTile)
        {
            _root = root;
            _backButton = back;
            _profileButton = profile;
            _settingsButton = settings;
            _profileTile = profileTile;
            _settingsTile = settingsTile;
            BindButtons();
            Hide();
        }

        private void Awake()
        {
            BindButtons();
        }

        private void BindButtons()
        {
            if (_backButton != null)
            {
                _backButton.onClick.RemoveListener(InvokeBack);
                _backButton.onClick.AddListener(InvokeBack);
            }

            if (_profileButton != null)
            {
                _profileButton.onClick.RemoveListener(InvokeProfile);
                _profileButton.onClick.AddListener(InvokeProfile);
            }

            if (_settingsButton != null)
            {
                _settingsButton.onClick.RemoveListener(InvokeSettings);
                _settingsButton.onClick.AddListener(InvokeSettings);
            }
        }

        public void Show()
        {
            if (_root != null)
            {
                _root.SetActive(true);
            }
            else
            {
                gameObject.SetActive(true);
            }

            SetProfileSelected(true);
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void SetProfileSelected(bool profileSelected)
        {
            if (_profileTile != null)
            {
                _profileTile.color = profileSelected ? _selectedColor : _idleColor;
            }

            if (_settingsTile != null)
            {
                _settingsTile.color = profileSelected ? _idleColor : _selectedColor;
            }
        }

        private void InvokeBack() => BackClicked?.Invoke();
        private void InvokeProfile() => ProfileClicked?.Invoke();
        private void InvokeSettings() => SettingsClicked?.Invoke();
    }
}
