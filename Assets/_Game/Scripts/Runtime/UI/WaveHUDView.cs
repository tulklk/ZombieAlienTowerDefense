using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Pure display for wave number, enemy progress and preparation countdown.</summary>
    public sealed class WaveHUDView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _waveText;

        [SerializeField]
        private TMP_Text _enemyProgressText;

        [SerializeField]
        private Image _progressFillImage;

        [SerializeField]
        private GameObject _countdownPanel;

        [SerializeField]
        private TMP_Text _countdownText;

        public void SetWaveNumber(int currentWave, int totalWaves)
        {
            if (_waveText != null)
            {
                _waveText.text = $"Wave {currentWave}/{totalWaves}";
            }
        }

        public void SetProgress(int resolvedCount, int plannedCount, float normalizedProgress)
        {
            if (_enemyProgressText != null)
            {
                _enemyProgressText.text = $"{resolvedCount}/{plannedCount}";
            }

            if (_progressFillImage != null)
            {
                _progressFillImage.fillAmount = normalizedProgress;
            }
        }

        public void ShowCountdown(float secondsRemaining)
        {
            if (_countdownPanel != null)
            {
                _countdownPanel.SetActive(true);
            }

            if (_countdownText != null)
            {
                _countdownText.text = Mathf.CeilToInt(Mathf.Max(0f, secondsRemaining)).ToString();
            }
        }

        public void HideCountdown()
        {
            if (_countdownPanel != null)
            {
                _countdownPanel.SetActive(false);
            }
        }
    }
}
