using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Pure display for wave number, player-level progress and preparation countdown. The
    /// level/progress fields used to show "how many enemies resolved this wave" - now show "how close to the
    /// next player Level" instead (see WaveHUDPresenter.Initialize(PlayerLevelProgressionService)), per the
    /// spec that this panel's bar stop being an enemy counter and become a Level bar.
    ///
    /// The bar is drawn as two stacked fills: a white "preview" one that slides to the new value almost
    /// immediately, and the gold one on top of it that follows a beat later and more slowly - so a burst of XP
    /// reads as "white jumps ahead, gold catches up", the gap between them showing exactly what was just
    /// gained. Both are DOTween tweens with easing rather than per-frame MoveTowards: XP arrives in small
    /// bursts (one per absorbed prop), and re-targeting a linear catch-up every burst is what made the bar look
    /// like it was stepping instead of sliding. Tweens run on unscaled time so the bar still resolves while the
    /// level-up popup has the game paused (see GameSpeedController).</summary>
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
        [Tooltip("The gold fill - trails the white one below, and is drawn over it.")]
        private Image _progressFillImage;

        [SerializeField]
        [Tooltip("Optional. The white fill drawn UNDER the gold one; leads it so the freshly gained XP shows as " +
            "a white gap ahead of the gold. Leave empty to get the old single-fill behaviour.")]
        private Image _progressPreviewFillImage;

        [Header("Fill Animation")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Seconds the white fill takes to reach a new value - it leads, so keep this short.")]
        private float _previewFillDuration = 0.22f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Seconds the gold fill takes to reach the same value - it trails the white one.")]
        private float _mainFillDuration = 0.55f;

        [SerializeField, Min(0f)]
        [Tooltip("Beat the gold fill waits before starting to chase the white one.")]
        private float _mainFillDelay = 0.15f;

        [Header("Countdown")]
        [SerializeField]
        private GameObject _countdownPanel;

        [SerializeField]
        private TMP_Text _countdownText;

        private Tweener _previewTween;
        private Tweener _mainTween;
        private float _lastTarget;

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

            float target = Mathf.Clamp01(normalizedProgress);

            // A drop means the bar wrapped (a level-up reset it to near 0) - tweening backwards would read as
            // "losing XP", so both fills snap instead and the next gain animates forward from there.
            if (target < _lastTarget)
            {
                KillTweens();
                SetFillsImmediate(target);
                _lastTarget = target;
                return;
            }

            _lastTarget = target;
            KillTweens();

            if (_progressPreviewFillImage != null)
            {
                _previewTween = DOTween
                    .To(() => _progressPreviewFillImage.fillAmount, v => _progressPreviewFillImage.fillAmount = v, target, _previewFillDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true); // unscaled: the level-up popup pauses the game while these are still running
            }

            if (_progressFillImage != null)
            {
                _mainTween = DOTween
                    .To(() => _progressFillImage.fillAmount, v => _progressFillImage.fillAmount = v, target, _mainFillDuration)
                    .SetEase(Ease.OutCubic)
                    .SetDelay(_mainFillDelay)
                    .SetUpdate(true);
            }
        }

        private void SetFillsImmediate(float value)
        {
            if (_progressPreviewFillImage != null)
            {
                _progressPreviewFillImage.fillAmount = value;
            }

            if (_progressFillImage != null)
            {
                _progressFillImage.fillAmount = value;
            }
        }

        /// <summary>Re-targeting mid-flight rather than stacking tweens: each XP burst replaces the previous
        /// pair, so the fills keep sliding from wherever they are instead of snapping or fighting each other.</summary>
        private void KillTweens()
        {
            _previewTween?.Kill();
            _mainTween?.Kill();
            _previewTween = null;
            _mainTween = null;
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

        private void OnDestroy()
        {
            KillTweens();
        }
    }
}
