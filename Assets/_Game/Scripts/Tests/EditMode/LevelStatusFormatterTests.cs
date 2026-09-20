using AlienDefense.Save;
using AlienDefense.UI.MainMenu;
using NUnit.Framework;

namespace AlienDefense.Tests.EditMode
{
    public class LevelStatusFormatterTests
    {
        [Test]
        public void Format_NotCompleted_ReturnsNotStartedText()
        {
            LevelProgressSnapshot progress = LevelProgressSnapshot.NotStarted("level_01");

            Assert.AreEqual("Not completed", LevelStatusFormatter.Format(progress, 20));
        }

        [Test]
        public void Format_CompletedWithPercent_ReturnsRemainingHpRichText()
        {
            var progress = new LevelProgressSnapshot("level_01", true, 2, 1, 12, 62);

            Assert.AreEqual("Remaining HP: <color=#FFD43B>62%</color>", LevelStatusFormatter.Format(progress, 20));
        }

        [Test]
        public void Format_PerfectPercent_UsesGreenColor()
        {
            var progress = new LevelProgressSnapshot("level_01", true, 3, 1, 20, 100);

            Assert.AreEqual("Remaining HP: <color=#45E55B>100%</color>", LevelStatusFormatter.Format(progress, 20));
        }

        [Test]
        public void Format_LowPercent_UsesOrangeColor()
        {
            var progress = new LevelProgressSnapshot("level_01", true, 1, 1, 2, 10);

            Assert.AreEqual("Remaining HP: <color=#FF9B3D>10%</color>", LevelStatusFormatter.Format(progress, 20));
        }

        [Test]
        public void Format_MissingPercent_FallsBackToAbsoluteHealth()
        {
            var progress = new LevelProgressSnapshot("level_01", true, 2, 1, 19, 0);

            Assert.AreEqual("Remaining HP: <color=#FFD43B>95%</color>", LevelStatusFormatter.Format(progress, 20));
        }

        [Test]
        public void Format_ZeroMaxHealth_DoesNotDivideByZero()
        {
            var progress = new LevelProgressSnapshot("level_01", true, 1, 1, 0, 0);

            Assert.AreEqual("Remaining HP: <color=#FF9B3D>0%</color>", LevelStatusFormatter.Format(progress, 0));
        }

        [Test]
        public void FormatLockedRequirement_WithPreviousTitle_ReferencesIt()
        {
            Assert.AreEqual("Complete Level 1 to unlock.", LevelStatusFormatter.FormatLockedRequirement("Level 1"));
        }

        [Test]
        public void FormatLockedRequirement_NoPreviousTitle_ReturnsGenericLockedText()
        {
            Assert.AreEqual("Locked", LevelStatusFormatter.FormatLockedRequirement(null));
        }
    }
}
