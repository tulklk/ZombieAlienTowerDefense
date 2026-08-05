using AlienDefense.Waves;
using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Subscribes to a fixed WaveController and forwards updates to a WaveHUDView.</summary>
    public sealed class WaveHUDPresenter : MonoBehaviour
    {
        [SerializeField]
        private WaveController _waveController;

        [SerializeField]
        private WaveHUDView _view;

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
            _waveController.WaveProgressChanged += HandleProgressChanged;

            _view.HideCountdown();
        }

        private void OnDestroy()
        {
            if (_waveController == null)
            {
                return;
            }

            _waveController.WavePrepared -= HandleWavePrepared;
            _waveController.PreparationTimeChanged -= HandlePreparationTimeChanged;
            _waveController.WaveStarted -= HandleWaveStarted;
            _waveController.WaveProgressChanged -= HandleProgressChanged;
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

        private void HandleProgressChanged(WaveProgressSnapshot snapshot)
        {
            _view.SetProgress(snapshot.ResolvedEnemyCount, snapshot.PlannedEnemyCount, snapshot.NormalizedProgress);
        }
    }
}
