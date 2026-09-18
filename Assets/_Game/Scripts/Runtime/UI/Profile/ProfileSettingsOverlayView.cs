using System;
using AlienDefense.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Profile
{
    /// <summary>Minimal MainMenu settings overlay: SFX / Music / Vibration via SettingsService.</summary>
    public sealed class ProfileSettingsOverlayView : MonoBehaviour
    {
        private const float MutedVolume = 0f;
        private const float DefaultSfxVolume = 0.8f;
        private const float DefaultMusicVolume = 0.6f;

        private static readonly Color OnTint = Color.white;
        private static readonly Color OffTint = new Color(0.45f, 0.45f, 0.5f, 1f);

        [SerializeField]
        private GameObject _root;

        [SerializeField]
        private Button _closeButton;

        [SerializeField]
        private Button _sfxButton;

        [SerializeField]
        private Button _musicButton;

        [SerializeField]
        private Button _vibrationButton;

        [SerializeField]
        private Image _sfxIcon;

        [SerializeField]
        private Image _musicIcon;

        [SerializeField]
        private Image _vibrationIcon;

        [SerializeField]
        private TMP_Text _sfxLabel;

        [SerializeField]
        private TMP_Text _musicLabel;

        [SerializeField]
        private TMP_Text _vibrationLabel;

        public event Action Closed;

        private SettingsService _settings;

        public void Wire(
            GameObject root,
            Button close,
            Button sfx,
            Button music,
            Button vibration,
            Image sfxIcon,
            Image musicIcon,
            Image vibrationIcon,
            TMP_Text sfxLabel,
            TMP_Text musicLabel,
            TMP_Text vibrationLabel)
        {
            _root = root;
            _closeButton = close;
            _sfxButton = sfx;
            _musicButton = music;
            _vibrationButton = vibration;
            _sfxIcon = sfxIcon;
            _musicIcon = musicIcon;
            _vibrationIcon = vibrationIcon;
            _sfxLabel = sfxLabel;
            _musicLabel = musicLabel;
            _vibrationLabel = vibrationLabel;
            BindButtons();
            Hide();
        }

        public void Bind(SettingsService settings)
        {
            BindButtons();
            _settings = settings;
            if (IsVisible)
            {
                Refresh();
            }
        }

        public bool IsVisible => _root != null && _root.activeSelf;

        private void Awake()
        {
            BindButtons();
        }

        private void OnDestroy()
        {
            UnbindButtons();
        }

        private void BindButtons()
        {
            UnbindButtons();
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(HandleClose);
            }

            if (_sfxButton != null)
            {
                _sfxButton.onClick.AddListener(HandleSfx);
            }

            if (_musicButton != null)
            {
                _musicButton.onClick.AddListener(HandleMusic);
            }

            if (_vibrationButton != null)
            {
                _vibrationButton.onClick.AddListener(HandleVibration);
            }
        }

        private void UnbindButtons()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(HandleClose);
            }

            if (_sfxButton != null)
            {
                _sfxButton.onClick.RemoveListener(HandleSfx);
            }

            if (_musicButton != null)
            {
                _musicButton.onClick.RemoveListener(HandleMusic);
            }

            if (_vibrationButton != null)
            {
                _vibrationButton.onClick.RemoveListener(HandleVibration);
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

            Refresh();
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

        public void Refresh()
        {
            if (_settings == null)
            {
                ApplyToggle(_sfxIcon, _sfxLabel, "SFX", true);
                ApplyToggle(_musicIcon, _musicLabel, "Music", true);
                ApplyToggle(_vibrationIcon, _vibrationLabel, "Vibration", true);
                return;
            }

            ApplyToggle(_sfxIcon, _sfxLabel, "SFX", _settings.Current.SfxVolume > 0f);
            ApplyToggle(_musicIcon, _musicLabel, "Music", _settings.Current.MusicVolume > 0f);
            ApplyToggle(_vibrationIcon, _vibrationLabel, "Vibration", _settings.Current.HapticsEnabled);
        }

        private static void ApplyToggle(Image icon, TMP_Text label, string name, bool isOn)
        {
            if (icon != null)
            {
                icon.color = isOn ? OnTint : OffTint;
            }

            if (label != null)
            {
                label.text = name + (isOn ? "  ON" : "  OFF");
            }
        }

        private void HandleSfx()
        {
            if (_settings == null)
            {
                return;
            }

            bool isOn = _settings.Current.SfxVolume > 0f;
            _settings.SetSfxVolume(isOn ? MutedVolume : DefaultSfxVolume);
            Refresh();
        }

        private void HandleMusic()
        {
            if (_settings == null)
            {
                return;
            }

            bool isOn = _settings.Current.MusicVolume > 0f;
            _settings.SetMusicVolume(isOn ? MutedVolume : DefaultMusicVolume);
            Refresh();
        }

        private void HandleVibration()
        {
            if (_settings == null)
            {
                return;
            }

            _settings.SetHapticsEnabled(!_settings.Current.HapticsEnabled);
            Refresh();
        }

        private void HandleClose()
        {
            Hide();
            Closed?.Invoke();
        }
    }
}
