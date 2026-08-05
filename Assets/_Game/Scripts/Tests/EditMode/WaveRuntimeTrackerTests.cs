using AlienDefense.Waves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.EditMode
{
    public class WaveRuntimeTrackerTests
    {
        [Test]
        public void Initialize_SetsPlannedEnemyCount()
        {
            var tracker = new WaveRuntimeTracker();

            tracker.Initialize(5);

            Assert.AreEqual(5, tracker.PlannedEnemyCount);
        }

        [Test]
        public void Initialize_ClampsNegativePlannedCountToZero()
        {
            var tracker = new WaveRuntimeTracker();

            LogAssert.Expect(LogType.Warning, "[WaveRuntimeTracker] plannedEnemyCount -3 is negative, clamped to 0.");
            tracker.Initialize(-3);

            Assert.AreEqual(0, tracker.PlannedEnemyCount);
        }

        [Test]
        public void RecordSpawnSuccess_IncreasesSuccessfulAndActiveCounts()
        {
            var tracker = new WaveRuntimeTracker();
            tracker.Initialize(5);

            tracker.RecordSpawnSuccess();

            Assert.AreEqual(1, tracker.SuccessfulSpawnCount);
            Assert.AreEqual(1, tracker.ActiveEnemyCount);
        }

        [Test]
        public void RecordSpawnFailure_IncreasesFailedCount_DoesNotIncreaseActiveCount()
        {
            var tracker = new WaveRuntimeTracker();
            tracker.Initialize(5);

            tracker.RecordSpawnFailure();

            Assert.AreEqual(1, tracker.FailedSpawnCount);
            Assert.AreEqual(0, tracker.ActiveEnemyCount);
        }

        [Test]
        public void RecordEnemyResolved_DecreasesActiveAndIncreasesResolved()
        {
            var tracker = new WaveRuntimeTracker();
            tracker.Initialize(5);
            tracker.RecordSpawnSuccess();

            bool result = tracker.RecordEnemyResolved();

            Assert.IsTrue(result);
            Assert.AreEqual(0, tracker.ActiveEnemyCount);
            Assert.AreEqual(1, tracker.ResolvedEnemyCount);
        }

        [Test]
        public void RecordEnemyResolved_WithNoActiveEnemies_IsRejected()
        {
            var tracker = new WaveRuntimeTracker();
            tracker.Initialize(5);

            LogAssert.Expect(LogType.Warning, "[WaveRuntimeTracker] RecordEnemyResolved called with no active enemies; ignoring duplicate/stale call.");
            bool result = tracker.RecordEnemyResolved();

            Assert.IsFalse(result);
            Assert.AreEqual(0, tracker.ActiveEnemyCount);
        }

        [Test]
        public void RecordEnemyResolved_DuplicateCall_IsRejectedAndActiveCountNeverGoesNegative()
        {
            var tracker = new WaveRuntimeTracker();
            tracker.Initialize(5);
            tracker.RecordSpawnSuccess();

            tracker.RecordEnemyResolved();
            LogAssert.Expect(LogType.Warning, "[WaveRuntimeTracker] RecordEnemyResolved called with no active enemies; ignoring duplicate/stale call.");
            bool secondCall = tracker.RecordEnemyResolved();

            Assert.IsFalse(secondCall);
            Assert.AreEqual(0, tracker.ActiveEnemyCount);
            Assert.AreEqual(1, tracker.ResolvedEnemyCount);
        }

        [Test]
        public void IsCompleted_IsFalse_WhenSchedulingNotCompleted()
        {
            var tracker = new WaveRuntimeTracker();
            tracker.Initialize(1);
            tracker.RecordSpawnSuccess();
            tracker.RecordEnemyResolved();

            Assert.IsFalse(tracker.IsCompleted);
        }

        [Test]
        public void IsCompleted_IsFalse_WhenEnemiesStillActive()
        {
            var tracker = new WaveRuntimeTracker();
            tracker.Initialize(1);
            tracker.RecordSpawnSuccess();
            tracker.MarkSpawnSchedulingCompleted();

            Assert.IsFalse(tracker.IsCompleted);
        }

        [Test]
        public void IsCompleted_IsTrue_WhenSchedulingCompletedAndNoActiveEnemies()
        {
            var tracker = new WaveRuntimeTracker();
            tracker.Initialize(1);
            tracker.RecordSpawnSuccess();
            tracker.RecordEnemyResolved();
            tracker.MarkSpawnSchedulingCompleted();

            Assert.IsTrue(tracker.IsCompleted);
        }

        [Test]
        public void IsCompleted_IsTrue_WhenAllSpawnsFailed()
        {
            var tracker = new WaveRuntimeTracker();
            tracker.Initialize(3);
            tracker.RecordSpawnFailure();
            tracker.RecordSpawnFailure();
            tracker.RecordSpawnFailure();
            tracker.MarkSpawnSchedulingCompleted();

            Assert.IsTrue(tracker.IsCompleted);
        }

        [Test]
        public void Reset_ClearsAllRuntimeState()
        {
            var tracker = new WaveRuntimeTracker();
            tracker.Initialize(5);
            tracker.RecordSpawnSuccess();
            tracker.RecordSpawnFailure();
            tracker.MarkSpawnSchedulingCompleted();

            tracker.Reset();

            Assert.AreEqual(0, tracker.PlannedEnemyCount);
            Assert.AreEqual(0, tracker.SuccessfulSpawnCount);
            Assert.AreEqual(0, tracker.FailedSpawnCount);
            Assert.AreEqual(0, tracker.ActiveEnemyCount);
            Assert.AreEqual(0, tracker.ResolvedEnemyCount);
            Assert.IsFalse(tracker.IsSpawnSchedulingCompleted);
        }

        [Test]
        public void NormalizedProgress_StartsAtZero_WhenNothingProcessedYet()
        {
            var tracker = new WaveRuntimeTracker();
            tracker.Initialize(4);
            tracker.RecordSpawnSuccess();

            Assert.AreEqual(0f, tracker.NormalizedProgress, 0.0001f);
        }

        [Test]
        public void NormalizedProgress_ReachesOne_WhenAllPlannedEnemiesProcessed()
        {
            var tracker = new WaveRuntimeTracker();
            tracker.Initialize(2);
            tracker.RecordSpawnSuccess();
            tracker.RecordEnemyResolved();
            tracker.RecordSpawnFailure();

            Assert.AreEqual(1f, tracker.NormalizedProgress, 0.0001f);
        }

        [Test]
        public void NormalizedProgress_NeverExceedsOne()
        {
            var tracker = new WaveRuntimeTracker();
            tracker.Initialize(1);
            tracker.RecordSpawnSuccess();
            tracker.RecordSpawnSuccess();
            tracker.RecordEnemyResolved();
            tracker.RecordEnemyResolved();

            Assert.LessOrEqual(tracker.NormalizedProgress, 1f);
        }

        [Test]
        public void NormalizedProgress_IsOne_WhenNoEnemiesPlanned()
        {
            var tracker = new WaveRuntimeTracker();
            tracker.Initialize(0);
            tracker.MarkSpawnSchedulingCompleted();

            Assert.AreEqual(1f, tracker.NormalizedProgress, 0.0001f);
            Assert.IsTrue(tracker.IsCompleted);
        }
    }
}
