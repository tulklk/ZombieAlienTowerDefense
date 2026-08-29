using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.PlayMode
{
    /// <summary>Resets Time.timeScale before and after the PlayMode test run, so a value left over from a manual
    /// Play Mode session (e.g. the game was paused via GameSpeedController and Stop was pressed without resuming)
    /// can never leak into frame-timing-dependent tests. Time.timeScale is a global engine setting that Unity does
    /// not reset when Play Mode stops.</summary>
    [SetUpFixture]
    public class PlayModeTestEnvironmentSetup
    {
        [OneTimeSetUp]
        public void ResetTimeScaleBeforeRun()
        {
            Time.timeScale = 1f;
        }

        [OneTimeTearDown]
        public void ResetTimeScaleAfterRun()
        {
            Time.timeScale = 1f;
        }
    }
}
