using System;

namespace AlienDefense.Core
{
    /// <summary>
    /// Single owner of "how fast is time moving right now". Wraps an <see cref="ITimeScaleTarget"/>
    /// so nothing else in the codebase is allowed to touch Time.timeScale directly.
    /// Locked externally (by the composition root, in reaction to GameFlowController reaching
    /// Victory/Defeat) so speed can no longer change once the match is over.
    /// </summary>
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

        /// <summary>Selects a new gameplay speed multiplier (e.g. x1, x2). Ignored while locked or paused input is invalid.</summary>
        public bool SetSpeed(int speed)
        {
            if (_isLocked)
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

        /// <summary>Freezes speed/pause controls. Called once the match reaches Victory or Defeat.</summary>
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
