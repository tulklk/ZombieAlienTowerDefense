using System;
using UnityEngine;

namespace AlienDefense.Core
{
    /// <summary>
    /// Pure C# state machine that owns the current <see cref="GameState"/> for a level.
    /// It does not know about gold, base health, player movement, enemies, towers or UI —
    /// it only decides which phase the match is in and notifies listeners.
    /// Other systems (WaveController, BaseHealthService via composition root, UI presenters)
    /// react to <see cref="GameStateChanged"/> instead of polling a singleton.
    /// </summary>
    public sealed class GameFlowController
    {
        public GameState CurrentState { get; private set; } = GameState.Initializing;

        /// <summary>Fired with (previousState, newState) whenever the state actually changes.</summary>
        public event Action<GameState, GameState> GameStateChanged;

        private GameState _stateBeforePause = GameState.PreparingWave;

        private bool IsGameOver => CurrentState == GameState.Victory || CurrentState == GameState.Defeat;

        /// <summary>Initializing -> PreparingWave, or PlayingWave -> PreparingWave (next wave).</summary>
        public bool BeginPreparingWave()
        {
            if (IsGameOver)
            {
                LogRejected(nameof(BeginPreparingWave));
                return false;
            }

            if (CurrentState != GameState.Initializing && CurrentState != GameState.PlayingWave)
            {
                LogRejected(nameof(BeginPreparingWave));
                return false;
            }

            SetState(GameState.PreparingWave);
            return true;
        }

        /// <summary>PreparingWave -> PlayingWave.</summary>
        public bool BeginPlayingWave()
        {
            if (CurrentState != GameState.PreparingWave)
            {
                LogRejected(nameof(BeginPlayingWave));
                return false;
            }

            SetState(GameState.PlayingWave);
            return true;
        }

        /// <summary>PreparingWave/PlayingWave -> Paused. Remembers the state to return to.</summary>
        public bool Pause()
        {
            if (CurrentState != GameState.PreparingWave && CurrentState != GameState.PlayingWave)
            {
                LogRejected(nameof(Pause));
                return false;
            }

            _stateBeforePause = CurrentState;
            SetState(GameState.Paused);
            return true;
        }

        /// <summary>Paused -> the state that was active before pausing.</summary>
        public bool Resume()
        {
            if (CurrentState != GameState.Paused)
            {
                LogRejected(nameof(Resume));
                return false;
            }

            SetState(_stateBeforePause);
            return true;
        }

        /// <summary>Any non-terminal state -> Victory. Idempotent once reached.</summary>
        public bool ReportVictory()
        {
            if (IsGameOver)
            {
                return false;
            }

            SetState(GameState.Victory);
            return true;
        }

        /// <summary>Any non-terminal state -> Defeat. Idempotent once reached.</summary>
        public bool ReportDefeat()
        {
            if (IsGameOver)
            {
                return false;
            }

            SetState(GameState.Defeat);
            return true;
        }

        private void SetState(GameState newState)
        {
            if (newState == CurrentState)
            {
                return;
            }

            GameState previous = CurrentState;
            CurrentState = newState;
            GameStateChanged?.Invoke(previous, newState);
        }

        private static void LogRejected(string transition)
        {
            Debug.LogWarning($"[GameFlowController] Rejected transition '{transition}': not valid from current state.");
        }
    }
}
