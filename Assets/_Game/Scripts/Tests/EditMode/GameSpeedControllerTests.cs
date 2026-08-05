using AlienDefense.Core;
using NUnit.Framework;

namespace AlienDefense.Tests.EditMode
{
    public class GameSpeedControllerTests
    {
        private sealed class FakeTimeScaleTarget : ITimeScaleTarget
        {
            public float TimeScale { get; set; } = 1f;
        }

        [Test]
        public void Constructor_DefaultsToSpeedOne_AndAppliesToTarget()
        {
            var target = new FakeTimeScaleTarget();

            var controller = new GameSpeedController(target);

            Assert.AreEqual(1, controller.CurrentSpeed);
            Assert.AreEqual(1f, target.TimeScale);
        }

        [Test]
        public void SetSpeed_AppliesAllowedSpeedToTarget()
        {
            var target = new FakeTimeScaleTarget();
            var controller = new GameSpeedController(target);

            bool result = controller.SetSpeed(2);

            Assert.IsTrue(result);
            Assert.AreEqual(2, controller.CurrentSpeed);
            Assert.AreEqual(2f, target.TimeScale);
        }

        [Test]
        public void SetSpeed_RejectsUnsupportedSpeed()
        {
            var target = new FakeTimeScaleTarget();
            var controller = new GameSpeedController(target);

            bool result = controller.SetSpeed(5);

            Assert.IsFalse(result);
            Assert.AreEqual(1, controller.CurrentSpeed);
        }

        [Test]
        public void Pause_SavesPreviousSpeed_AndZeroesTimeScale()
        {
            var target = new FakeTimeScaleTarget();
            var controller = new GameSpeedController(target);
            controller.SetSpeed(2);

            bool result = controller.Pause();

            Assert.IsTrue(result);
            Assert.IsTrue(controller.IsPaused);
            Assert.AreEqual(0f, target.TimeScale);
        }

        [Test]
        public void Resume_RestoresSpeedThatWasActiveBeforePause()
        {
            var target = new FakeTimeScaleTarget();
            var controller = new GameSpeedController(target);
            controller.SetSpeed(2);
            controller.Pause();

            bool result = controller.Resume();

            Assert.IsTrue(result);
            Assert.IsFalse(controller.IsPaused);
            Assert.AreEqual(2, controller.CurrentSpeed);
            Assert.AreEqual(2f, target.TimeScale);
        }

        [Test]
        public void Lock_PreventsSpeedChanges_UntilUnlocked()
        {
            var target = new FakeTimeScaleTarget();
            var controller = new GameSpeedController(target);
            controller.Lock();

            bool setSpeedResult = controller.SetSpeed(2);
            bool pauseResult = controller.Pause();

            Assert.IsFalse(setSpeedResult);
            Assert.IsFalse(pauseResult);
            Assert.AreEqual(1, controller.CurrentSpeed);
        }

        [Test]
        public void Pause_WhileAlreadyPaused_ReturnsFalse()
        {
            var target = new FakeTimeScaleTarget();
            var controller = new GameSpeedController(target);
            controller.Pause();

            bool result = controller.Pause();

            Assert.IsFalse(result);
        }
    }
}
