using AlienDefense.Base;
using AlienDefense.Data;
using AlienDefense.Economy;
using AlienDefense.Player;
using UnityEngine;

namespace AlienDefense.Core
{
    /// <summary>
    /// Wires up the pure C# services a level needs, in explicit order, from a
    /// <see cref="LevelDefinition"/> and whatever fixed Scene objects are assigned in the Inspector.
    /// This is the only MonoBehaviour allowed to construct these services with `new` — everything
    /// else receives them already built, via constructor/method injection or [SerializeField].
    ///
    /// Grows in later phases (Player, Camera, Enemy, Wave, Build systems get wired in here too)
    /// but must stay a thin wiring layer: validate dependencies, create services, inject, start,
    /// clean up. No gameplay rules belong in this class.
    /// </summary>
    public sealed class LevelCompositionRoot : MonoBehaviour
    {
        [SerializeField]
        private LevelDefinition _levelDefinition;

        [SerializeField]
        [Tooltip("Optional (not present until Phase 2's Player is in the scene).")]
        private PlayerController _player;

        public GameFlowController GameFlow { get; private set; }
        public GameSpeedController GameSpeed { get; private set; }
        public EconomyService Economy { get; private set; }
        public BaseHealthService BaseHealth { get; private set; }

        private void Awake()
        {
            if (_levelDefinition == null)
            {
                Debug.LogError("[LevelCompositionRoot] No LevelDefinition assigned. Level will not start.", this);
                enabled = false;
                return;
            }

            GameFlow = new GameFlowController();
            GameSpeed = new GameSpeedController(new UnityTimeScaleTarget());
            Economy = new EconomyService(_levelDefinition.StartingResource);
            BaseHealth = new BaseHealthService(_levelDefinition.BaseMaxHealth);

            Application.targetFrameRate = _levelDefinition.TargetFrameRate;

            BaseHealth.Destroyed += HandleBaseDestroyed;
            GameFlow.GameStateChanged += HandleGameStateChanged;
        }

        private void Start()
        {
            if (!enabled)
            {
                return;
            }

            GameFlow.BeginPreparingWave();
        }

        private void OnDestroy()
        {
            if (BaseHealth != null)
            {
                BaseHealth.Destroyed -= HandleBaseDestroyed;
            }

            if (GameFlow != null)
            {
                GameFlow.GameStateChanged -= HandleGameStateChanged;
            }
        }

        private void HandleBaseDestroyed()
        {
            GameFlow.ReportDefeat();
        }

        private void HandleGameStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Victory || current == GameState.Defeat)
            {
                GameSpeed.Lock();
            }

            if (_player != null)
            {
                bool movementEnabled = current != GameState.Paused
                    && current != GameState.Victory
                    && current != GameState.Defeat;
                _player.SetMovementEnabled(movementEnabled);
            }
        }
    }
}
