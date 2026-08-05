using AlienDefense.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.EditMode
{
    public class GameFlowControllerTests
    {
        [Test]
        public void InitialState_IsInitializing()
        {
            var flow = new GameFlowController();

            Assert.AreEqual(GameState.Initializing, flow.CurrentState);
        }

        [Test]
        public void BeginPreparingWave_FromInitializing_Succeeds()
        {
            var flow = new GameFlowController();

            bool result = flow.BeginPreparingWave();

            Assert.IsTrue(result);
            Assert.AreEqual(GameState.PreparingWave, flow.CurrentState);
        }

        [Test]
        public void BeginPlayingWave_WithoutPreparingFirst_IsRejected()
        {
            var flow = new GameFlowController();

            LogAssert.Expect(LogType.Warning, "[GameFlowController] Rejected transition 'BeginPlayingWave': not valid from current state.");
            bool result = flow.BeginPlayingWave();

            Assert.IsFalse(result);
            Assert.AreEqual(GameState.Initializing, flow.CurrentState);
        }

        [Test]
        public void FullWaveCycle_TransitionsThroughExpectedStates()
        {
            var flow = new GameFlowController();

            flow.BeginPreparingWave();
            flow.BeginPlayingWave();
            Assert.AreEqual(GameState.PlayingWave, flow.CurrentState);

            flow.BeginPreparingWave();
            Assert.AreEqual(GameState.PreparingWave, flow.CurrentState);
        }

        [Test]
        public void Pause_ThenResume_ReturnsToStateBeforePause()
        {
            var flow = new GameFlowController();
            flow.BeginPreparingWave();
            flow.BeginPlayingWave();

            flow.Pause();
            Assert.AreEqual(GameState.Paused, flow.CurrentState);

            flow.Resume();
            Assert.AreEqual(GameState.PlayingWave, flow.CurrentState);
        }

        [Test]
        public void ReportVictory_FiresGameStateChangedExactlyOnce_EvenIfCalledTwice()
        {
            var flow = new GameFlowController();
            flow.BeginPreparingWave();
            int eventCount = 0;
            flow.GameStateChanged += (previous, current) => eventCount++;

            bool first = flow.ReportVictory();
            bool second = flow.ReportVictory();

            Assert.IsTrue(first);
            Assert.IsFalse(second);
            Assert.AreEqual(1, eventCount);
            Assert.AreEqual(GameState.Victory, flow.CurrentState);
        }

        [Test]
        public void ReportDefeat_AfterVictory_IsRejected_VictoryIsTerminal()
        {
            var flow = new GameFlowController();
            flow.ReportVictory();

            bool result = flow.ReportDefeat();

            Assert.IsFalse(result);
            Assert.AreEqual(GameState.Victory, flow.CurrentState);
        }

        [Test]
        public void Pause_WhileGameOver_IsRejected()
        {
            var flow = new GameFlowController();
            flow.ReportDefeat();

            LogAssert.Expect(LogType.Warning, "[GameFlowController] Rejected transition 'Pause': not valid from current state.");
            bool result = flow.Pause();

            Assert.IsFalse(result);
            Assert.AreEqual(GameState.Defeat, flow.CurrentState);
        }

        [Test]
        public void GameStateChanged_ReportsCorrectPreviousAndCurrent()
        {
            var flow = new GameFlowController();
            GameState observedPrevious = default;
            GameState observedCurrent = default;
            flow.GameStateChanged += (previous, current) =>
            {
                observedPrevious = previous;
                observedCurrent = current;
            };

            flow.BeginPreparingWave();

            Assert.AreEqual(GameState.Initializing, observedPrevious);
            Assert.AreEqual(GameState.PreparingWave, observedCurrent);
        }
    }
}
