using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Displays resource, base health, and current game speed; forwards Pause/Speed button clicks. No business logic.</summary>
    public sealed class GameHUDView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _resourceText;

        [SerializeField]
        [Tooltip("Optional. Shows EnergyWalletService.CurrentEnergy - the separate 'energy ball' currency only " +
            "gained by tractor-beaming an EnergyPickup into the UFO (see EnergyWalletService's own doc comment " +
            "for why it's kept apart from _resourceText/EconomyService, the tower build currency).")]
        private TMP_Text _energyText;

        [SerializeField]
        private TMP_Text _baseHealthText;

        [SerializeField]
        [Tooltip("Optional.")]
        private Image _baseHealthFillImage;

        [SerializeField]
        private TMP_Text _speedText;

        [SerializeField]
        private Button _speedButton;

        [SerializeField]
        private Button _pauseButton;

        public event Action SpeedButtonClicked;
        public event Action PauseButtonClicked;

        private void Awake()
        {
            if (_speedButton != null)
            {
                _speedButton.onClick.AddListener(HandleSpeedClicked);
            }

            if (_pauseButton != null)
            {
                _pauseButton.onClick.AddListener(HandlePauseClicked);
            }
        }

        public void SetResource(int amount)
        {
            if (_resourceText != null)
            {
                _resourceText.text = amount.ToString();
            }
        }

        /// <summary>Energy is capped by the Capacity ("Tải") skill's current rank (see EnergyWalletService.MaxEnergy) -
        /// shown as "current/max", same convention as SetBaseHealth.</summary>
        public void SetEnergy(int current, int max)
        {
            if (_energyText != null)
            {
                _energyText.text = current + "/" + max;
            }
        }

        /// <summary>Text shows only the current value (e.g. "100", not "100/100") - the ring's own fillAmount
        /// is what conveys "out of max" visually, so the max number would just be redundant clutter here.</summary>
        public void SetBaseHealth(int current, int max)
        {
            if (_baseHealthText != null)
            {
                _baseHealthText.text = current.ToString();
            }

            if (_baseHealthFillImage != null)
            {
                _baseHealthFillImage.fillAmount = max > 0 ? (float)current / max : 0f;
            }
        }

        public void SetSpeed(int speed)
        {
            if (_speedText != null)
            {
                _speedText.text = "x" + speed;
            }
        }

        private void HandleSpeedClicked()
        {
            SpeedButtonClicked?.Invoke();
        }

        private void HandlePauseClicked()
        {
            PauseButtonClicked?.Invoke();
        }

        private void OnDestroy()
        {
            if (_speedButton != null)
            {
                _speedButton.onClick.RemoveListener(HandleSpeedClicked);
            }

            if (_pauseButton != null)
            {
                _pauseButton.onClick.RemoveListener(HandlePauseClicked);
            }
        }
    }
}
