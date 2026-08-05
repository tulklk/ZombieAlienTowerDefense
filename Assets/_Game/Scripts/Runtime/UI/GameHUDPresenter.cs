using AlienDefense.Base;
using AlienDefense.Core;
using AlienDefense.Economy;
using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Wires GameHUDView to Economy, BaseHealth, and GameSpeed. Requests Pause via GameFlow/GameSpeed; never sets Time.timeScale itself.</summary>
    public sealed class GameHUDPresenter : MonoBehaviour
    {
        [SerializeField]
        private GameHUDView _view;

        private EconomyService _economy;
        private BaseHealthService _baseHealth;
        private GameSpeedController _gameSpeed;
        private GameFlowController _gameFlow;

        public void Initialize(EconomyService economy, BaseHealthService baseHealth, GameSpeedController gameSpeed, GameFlowController gameFlow)
        {
            Unsubscribe();

            _economy = economy;
            _baseHealth = baseHealth;
            _gameSpeed = gameSpeed;
            _gameFlow = gameFlow;

            if (_economy != null)
            {
                _economy.ResourceChanged += HandleResourceChanged;
            }

            if (_baseHealth != null)
            {
                _baseHealth.HealthChanged += HandleBaseHealthChanged;
            }

            if (_gameSpeed != null)
            {
                _gameSpeed.SpeedChanged += HandleSpeedChanged;
            }

            if (_view != null)
            {
                _view.SpeedButtonClicked += HandleSpeedButtonClicked;
                _view.PauseButtonClicked += HandlePauseButtonClicked;
            }

            RefreshAll();
        }

        private void RefreshAll()
        {
            if (_view == null)
            {
                return;
            }

            if (_economy != null)
            {
                _view.SetResource(_economy.CurrentResource);
            }

            if (_baseHealth != null)
            {
                _view.SetBaseHealth(_baseHealth.CurrentHealth, _baseHealth.MaxHealth);
            }

            if (_gameSpeed != null)
            {
                _view.SetSpeed(_gameSpeed.CurrentSpeed);
            }
        }

        private void HandleResourceChanged(int amount)
        {
            _view?.SetResource(amount);
        }

        private void HandleBaseHealthChanged(int current, int max)
        {
            _view?.SetBaseHealth(current, max);
        }

        private void HandleSpeedChanged(int speed)
        {
            _view?.SetSpeed(speed);
        }

        private void HandleSpeedButtonClicked()
        {
            if (_gameSpeed == null)
            {
                return;
            }

            int nextSpeed = _gameSpeed.CurrentSpeed == 1 ? 2 : 1;
            _gameSpeed.SetSpeed(nextSpeed);
        }

        private void HandlePauseButtonClicked()
        {
            _gameFlow?.Pause();
            _gameSpeed?.Pause();
        }

        private void Unsubscribe()
        {
            if (_economy != null)
            {
                _economy.ResourceChanged -= HandleResourceChanged;
            }

            if (_baseHealth != null)
            {
                _baseHealth.HealthChanged -= HandleBaseHealthChanged;
            }

            if (_gameSpeed != null)
            {
                _gameSpeed.SpeedChanged -= HandleSpeedChanged;
            }

            if (_view != null)
            {
                _view.SpeedButtonClicked -= HandleSpeedButtonClicked;
                _view.PauseButtonClicked -= HandlePauseButtonClicked;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
