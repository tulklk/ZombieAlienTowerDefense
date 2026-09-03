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
        public void Format_ThreeStars_ReturnsPerfectText()
        {
            var progress = new LevelProgressSnapshot("level_01", true, 3, 1, 20);

            Assert.AreEqual("Perfect", LevelStatusFormatter.Format(progress, 20));
        }

        [Test]
        public void Format_TwoStars_ReturnsRemainingHealthPercentage()
        {
            var progress = new LevelProgressSnapshot("level_01", true, 2, 1, 19);

            Assert.AreEqual("HP remaining: 95%", LevelStatusFormatter.Format(progress, 20));
        }

        [Test]
        public void Format_OneStar_ReturnsRemainingHealthPercentage()
        {
            var progress = new LevelProgressSnapshot("level_01", true, 1, 1, 2);

            Assert.AreEqual("HP remaining: 10%", LevelStatusFormatter.Format(progress, 20));
        }

        [Test]
        public void Format_ZeroMaxHealth_DoesNotDivideByZero()
        {
            var progress = new LevelProgressSnapshot("level_01", true, 1, 1, 0);

            Assert.AreEqual("HP remaining: 0%", LevelStatusFormatter.Format(progress, 0));
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
