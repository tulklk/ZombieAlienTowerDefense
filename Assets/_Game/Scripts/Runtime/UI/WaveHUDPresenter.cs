using AlienDefense.Economy;
using AlienDefense.Waves;
using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Subscribes to a fixed WaveController (wave number + preparation countdown only - see
    /// WaveHUDView's own doc comment for why this panel's progress bar no longer tracks WaveController's own
    /// enemy-resolved progress) and, once Initialize is called with it, a PlayerLevelProgressionService for the
    /// Level text + Level-progress bar.</summary>
    public sealed class WaveHUDPresenter : MonoBehaviour
    {
        [SerializeField]
        private WaveController _waveController;

        [SerializeField]
        private WaveHUDView _view;

        private PlayerLevelProgressionService _levelProgression;

        private void Awake()
        {
            if (_waveController == null || _view == null)
            {
                Debug.LogError("[WaveHUDPresenter] WaveController and WaveHUDView must both be assigned.", this);
                enabled = false;
                return;
            }

            _waveController.WavePrepared += HandleWavePrepared;
            _waveController.PreparationTimeChanged += HandlePreparationTimeChanged;
            _waveController.WaveStarted += HandleWaveStarted;

            _view.HideCountdown();
        }

        /// <summary>Optional second init step - PlayerLevelProgressionService is a plain runtime service built
        /// by LevelCompositionRoot, not something this presenter can hold a [SerializeField] to, so it's pushed
        /// in explicitly once composition finishes (mirrors GameHUDPresenter.Initialize's own pattern).</summary>
        public void Initialize(PlayerLevelProgressionService levelProgression)
        {
            UnsubscribeLevelProgression();

            _levelProgression = levelProgression;
            if (_levelProgression == null)
            {
                return;
            }

            _levelProgression.ExperienceChanged += HandleExperienceChanged;
            _levelProgression.LevelChanged += HandleLevelChanged;

            _view.SetLevel(_levelProgression.CurrentLevel);
            RefreshLevelProgress();
        }

        private void OnDestroy()
        {
            if (_waveController != null)
            {
                _waveController.WavePrepared -= HandleWavePrepared;
                _waveController.PreparationTimeChanged -= HandlePreparationTimeChanged;
                _waveController.WaveStarted -= HandleWaveStarted;
            }

            UnsubscribeLevelProgression();
        }

        private void UnsubscribeLevelProgression()
        {
            if (_levelProgression == null)
            {
                return;
            }

            _levelProgression.ExperienceChanged -= HandleExperienceChanged;
            _levelProgression.LevelChanged -= HandleLevelChanged;
        }

        private void HandleWavePrepared(int waveNumber, int totalWaves)
        {
            _view.SetWaveNumber(waveNumber, totalWaves);
        }

        private void HandlePreparationTimeChanged(float secondsRemaining)
        {
            if (secondsRemaining <= 0f)
            {
                _view.HideCountdown();
                return;
            }

            _view.ShowCountdown(secondsRemaining);
        }

        private void HandleWaveStarted(int waveNumber, int totalWaves)
        {
            _view.SetWaveNumber(waveNumber, totalWaves);
            _view.HideCountdown();
        }

        private void HandleExperienceChanged(int totalExperience)
        {
            RefreshLevelProgress();
        }

        private void HandleLevelChanged(int newLevel)
        {
            _view.SetLevel(newLevel);
            RefreshLevelProgress();
        }

        private void RefreshLevelProgress()
        {
            (int currentInLevel, int neededForLevel, float normalized) = _levelProgression.GetProgressInCurrentLevel();
            _view.SetLevelProgress(currentInLevel, neededForLevel, normalized);
        }
    }
}
