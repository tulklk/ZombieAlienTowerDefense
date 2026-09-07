using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Pure display for wave number, player-level progress and preparation countdown. The
    /// level/progress fields used to show "how many enemies resolved this wave" - now show "how close to the
    /// next player Level" instead (see WaveHUDPresenter.Initialize(PlayerLevelProgressionService)), per the
    /// spec that this panel's bar stop being an enemy counter and become a Level bar.</summary>
    public sealed class WaveHUDView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _waveText;

        [SerializeField]
        [Tooltip("Was a static 'Level 1' placeholder - now bound to PlayerLevelProgressionService.CurrentLevel.")]
        private TMP_Text _levelText;

        [SerializeField]
        [Tooltip("Was 'resolvedEnemyCount/plannedEnemyCount' for the current wave - now 'XP into this level/XP " +
            "needed for the next level'.")]
        private TMP_Text _levelProgressText;

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

        public void SetLevel(int level)
        {
            if (_levelText != null)
            {
                _levelText.text = "Level " + level;
            }
        }

        public void SetLevelProgress(int currentXpInLevel, int xpPerLevel, float normalizedProgress)
        {
            if (_levelProgressText != null)
            {
                _levelProgressText.text = $"{currentXpInLevel}/{xpPerLevel}";
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
