using AlienDefense.Audio;
using AlienDefense.Building;
using AlienDefense.Towers;
using AlienDefense.Waves;
using UnityEngine;

namespace AlienDefense.Core
{
    /// <summary>Plays SFX in response to Build/Upgrade/Sell/Wave/Victory/Defeat events. No gameplay logic, no volume hard-coded here.</summary>
    public sealed class GameplayAudioController
    {
        private readonly AudioService _audio;
        private readonly AudioClip _buildClip;
        private readonly AudioClip _upgradeClip;
        private readonly AudioClip _sellClip;
        private readonly AudioClip _waveStartClip;
        private readonly AudioClip _victoryClip;
        private readonly AudioClip _defeatClip;

        private BuildService _buildService;
        private TowerUpgradeService _upgradeService;
        private TowerSellService _sellService;
        private WaveController _waveController;
        private GameFlowController _gameFlow;

        public GameplayAudioController(
            AudioService audio,
            AudioClip buildClip,
            AudioClip upgradeClip,
            AudioClip sellClip,
            AudioClip waveStartClip,
            AudioClip victoryClip,
            AudioClip defeatClip)
        {
            _audio = audio;
            _buildClip = buildClip;
            _upgradeClip = upgradeClip;
            _sellClip = sellClip;
            _waveStartClip = waveStartClip;
            _victoryClip = victoryClip;
            _defeatClip = defeatClip;
        }

        public void Initialize(BuildService buildService, TowerUpgradeService upgradeService, TowerSellService sellService, WaveController waveController, GameFlowController gameFlow)
        {
            Unsubscribe();

            _buildService = buildService;
            _upgradeService = upgradeService;
            _sellService = sellService;
            _waveController = waveController;
            _gameFlow = gameFlow;

            if (_buildService != null)
            {
                _buildService.BuildCompleted += HandleBuildCompleted;
            }

            if (_upgradeService != null)
            {
                _upgradeService.TowerUpgraded += HandleTowerUpgraded;
            }

            if (_sellService != null)
            {
                _sellService.TowerSold += HandleTowerSold;
            }

            if (_waveController != null)
            {
                _waveController.WaveStarted += HandleWaveStarted;
            }

            if (_gameFlow != null)
            {
                _gameFlow.GameStateChanged += HandleGameStateChanged;
            }
        }

        private void HandleBuildCompleted(BuildNode node, TowerController tower)
        {
            _audio?.PlaySfx(_buildClip);
        }

        private void HandleTowerUpgraded(TowerController tower)
        {
            _audio?.PlaySfx(_upgradeClip);
        }

        private void HandleTowerSold(TowerController tower)
        {
            _audio?.PlaySfx(_sellClip);
        }

        private void HandleWaveStarted(int waveNumber, int totalWaves)
        {
            _audio?.PlaySfx(_waveStartClip);
        }

        private void HandleGameStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Victory)
            {
                _audio?.PlaySfx(_victoryClip);
            }
            else if (current == GameState.Defeat)
            {
                _audio?.PlaySfx(_defeatClip);
            }
        }

        public void Unsubscribe()
        {
            if (_buildService != null)
            {
                _buildService.BuildCompleted -= HandleBuildCompleted;
            }

            if (_upgradeService != null)
            {
                _upgradeService.TowerUpgraded -= HandleTowerUpgraded;
            }

            if (_sellService != null)
            {
                _sellService.TowerSold -= HandleTowerSold;
            }

            if (_waveController != null)
            {
                _waveController.WaveStarted -= HandleWaveStarted;
            }

            if (_gameFlow != null)
            {
                _gameFlow.GameStateChanged -= HandleGameStateChanged;
            }
        }
    }
}
