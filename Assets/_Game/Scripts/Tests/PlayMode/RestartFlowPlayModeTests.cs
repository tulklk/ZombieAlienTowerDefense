using AlienDefense.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    /// <summary>Covers LevelRestartService.Restart()'s Time.timeScale reset. Uses an intentionally nonexistent
    /// scene name so SceneManager.LoadScene fails harmlessly (logged and expected) instead of attempting a real
    /// scene swap, which is unsafe to automate against Unity Test Runner's own host scene.</summary>
    public class RestartFlowPlayModeTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
        }

        [Test]
        public void Restart_ResetsTimeScale_ToOne_BeforeAttemptingLoad()
        {
            Time.timeScale = 0.5f;
            var service = new LevelRestartService("__NonExistentSceneForRestartTest__");

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*"));
            service.Restart();

            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void Restart_WhilePaused_StillResetsTimeScale()
        {
            Time.timeScale = 0f;
            var service = new LevelRestartService("__NonExistentSceneForRestartTest__");

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*"));
            service.Restart();

            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
