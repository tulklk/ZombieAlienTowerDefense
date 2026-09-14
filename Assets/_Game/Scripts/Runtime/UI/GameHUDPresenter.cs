using AlienDefense.Base;
using AlienDefense.Core;
using AlienDefense.Economy;
using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Wires GameHUDView to Economy, EnergyWallet, BaseHealth, and GameSpeed. Requests Pause via GameFlow/GameSpeed; never sets Time.timeScale itself.</summary>
    public sealed class GameHUDPresenter : MonoBehaviour
    {
        [SerializeField]
        private GameHUDView _view;

        private EconomyService _economy;
        private EnergyWalletService _energyWallet;
        private BaseHealthService _baseHealth;
        private GameSpeedController _gameSpeed;
        private GameFlowController _gameFlow;
        private bool _wasFull;

        public void Initialize(EconomyService economy, EnergyWalletService energyWallet, BaseHealthService baseHealth, GameSpeedController gameSpeed, GameFlowController gameFlow)
        {
            Unsubscribe();

            _economy = economy;
            _energyWallet = energyWallet;
            _baseHealth = baseHealth;
            _gameSpeed = gameSpeed;
            _gameFlow = gameFlow;
            _wasFull = false;

            if (_economy != null)
            {
                _economy.ResourceChanged += HandleResourceChanged;
            }

            if (_energyWallet != null)
            {
                _energyWallet.EnergyChanged += HandleEnergyChanged;
                _energyWallet.MaxEnergyChanged += HandleMaxEnergyChanged;
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

        /// <summary>Called when the tractor beam is over Idle EnergyPickups while cargo is full.
        /// Drives presence banner hold + forbidden icon (not a timed auto-dismiss toast).</summary>
        public void NotifyCargoFullRefuse()
        {
            _view?.NotifyCargoFullPresence();
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

            if (_energyWallet != null)
            {
                _view.SetEnergy(_energyWallet.CurrentEnergy, _energyWallet.MaxEnergy);
                _wasFull = _energyWallet.IsFull;
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

        private void HandleEnergyChanged(int amount)
        {
            if (_energyWallet == null)
            {
                return;
            }

            _view?.SetEnergy(amount, _energyWallet.MaxEnergy);

            bool isFull = _energyWallet.IsFull;
            if (isFull && !_wasFull)
            {
                // First time hitting the cap — toast once without requiring another beam refuse.
                _view?.NotifyCargoFullRefuse();
            }

            _wasFull = isFull;
        }

        private void HandleMaxEnergyChanged(int max)
        {
            if (_energyWallet != null)
            {
                _view?.SetEnergy(_energyWallet.CurrentEnergy, max);
                _wasFull = _energyWallet.IsFull;
            }
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

            if (_energyWallet != null)
            {
                _energyWallet.EnergyChanged -= HandleEnergyChanged;
                _energyWallet.MaxEnergyChanged -= HandleMaxEnergyChanged;
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
