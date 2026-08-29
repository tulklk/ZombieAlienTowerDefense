using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>One level card. Dumb view: displays title/lock/stars and forwards Selected. Never decides
    /// unlock state and never loads Gameplay itself.</summary>
    public sealed class LevelCardView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _titleText;

        [SerializeField]
        [Tooltip("Optional.")]
        private Image _previewImage;

        [SerializeField]
        [Tooltip("Optional.")]
        private GameObject _lockedOverlay;

        [SerializeField]
        private Button _playButton;

        [SerializeField]
        [Tooltip("Optional star placeholder icons, left to right.")]
        private GameObject[] _starIcons = Array.Empty<GameObject>();

        public event Action Selected;

        private void Awake()
        {
            if (_playButton != null)
            {
                _playButton.onClick.AddListener(HandleClicked);
            }
        }

        public void Configure(string title, Sprite preview, bool isLocked, int starsEarned)
        {
            if (_titleText != null)
            {
                _titleText.text = title;
            }

            if (_previewImage != null && preview != null)
            {
                _previewImage.sprite = preview;
            }

            SetLocked(isLocked);
            SetStars(starsEarned);
        }

        public void SetLocked(bool isLocked)
        {
            if (_lockedOverlay != null)
            {
                _lockedOverlay.SetActive(isLocked);
            }

            if (_playButton != null)
            {
                _playButton.interactable = !isLocked;
            }
        }

        private void SetStars(int starsEarned)
        {
            for (int i = 0; i < _starIcons.Length; i++)
            {
                if (_starIcons[i] != null)
                {
                    _starIcons[i].SetActive(i < starsEarned);
                }
            }
        }

        private void HandleClicked()
        {
            Selected?.Invoke();
        }

        private void OnDestroy()
        {
            if (_playButton != null)
            {
                _playButton.onClick.RemoveListener(HandleClicked);
            }
        }
    }
}
