using System;

namespace AlienDefense.Core
{
    /// <summary>Owns gameplay speed (pause/resume, x1/x2) via an injected time scale target.</summary>
    public sealed class GameSpeedController
    {
        private static readonly int[] DefaultAllowedSpeeds = { 1, 2 };

        private readonly ITimeScaleTarget _timeScaleTarget;
        private readonly int[] _allowedSpeeds;

        private int _speedBeforePause;
        private bool _isLocked;

        public int CurrentSpeed { get; private set; } = 1;
        public bool IsPaused { get; private set; }

        public event Action<int> SpeedChanged;
        public event Action<bool> PauseChanged;

        public GameSpeedController(ITimeScaleTarget timeScaleTarget, int[] allowedSpeeds = null)
        {
            _timeScaleTarget = timeScaleTarget ?? throw new ArgumentNullException(nameof(timeScaleTarget));
            _allowedSpeeds = allowedSpeeds ?? DefaultAllowedSpeeds;
            _speedBeforePause = CurrentSpeed;
            _timeScaleTarget.TimeScale = CurrentSpeed;
        }

        /// <summary>Selects a new gameplay speed multiplier (e.g. x1, x2).</summary>
        public bool SetSpeed(int speed)
        {
            if (_isLocked || IsPaused)
            {
                return false;
            }

            if (Array.IndexOf(_allowedSpeeds, speed) < 0)
            {
                return false;
            }

            CurrentSpeed = speed;

            if (!IsPaused)
            {
                _timeScaleTarget.TimeScale = speed;
            }

            SpeedChanged?.Invoke(speed);
            return true;
        }

        public bool Pause()
        {
            if (_isLocked || IsPaused)
            {
                return false;
            }

            _speedBeforePause = CurrentSpeed;
            IsPaused = true;
            _timeScaleTarget.TimeScale = 0f;
            PauseChanged?.Invoke(true);
            return true;
        }

        public bool Resume()
        {
            if (_isLocked || !IsPaused)
            {
                return false;
            }

            IsPaused = false;
            CurrentSpeed = _speedBeforePause;
            _timeScaleTarget.TimeScale = _speedBeforePause;
            PauseChanged?.Invoke(false);
            return true;
        }

        /// <summary>Freezes speed/pause controls.</summary>
        public void Lock()
        {
            _isLocked = true;
        }

        public void Unlock()
        {
            _isLocked = false;
        }
    }
}
