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

            Assert.AreEqual("Chưa hoàn thành", LevelStatusFormatter.Format(progress, 20));
        }

        [Test]
        public void Format_ThreeStars_ReturnsPerfectText()
        {
            var progress = new LevelProgressSnapshot("level_01", true, 3, 1, 20);

            Assert.AreEqual("Hoàn hảo", LevelStatusFormatter.Format(progress, 20));
        }

        [Test]
        public void Format_TwoStars_ReturnsRemainingHealthPercentage()
        {
            var progress = new LevelProgressSnapshot("level_01", true, 2, 1, 19);

            Assert.AreEqual("HP còn lại: 95%", LevelStatusFormatter.Format(progress, 20));
        }

        [Test]
        public void Format_OneStar_ReturnsRemainingHealthPercentage()
        {
            var progress = new LevelProgressSnapshot("level_01", true, 1, 1, 2);

            Assert.AreEqual("HP còn lại: 10%", LevelStatusFormatter.Format(progress, 20));
        }

        [Test]
        public void Format_ZeroMaxHealth_DoesNotDivideByZero()
        {
            var progress = new LevelProgressSnapshot("level_01", true, 1, 1, 0);

            Assert.AreEqual("HP còn lại: 0%", LevelStatusFormatter.Format(progress, 0));
        }

        [Test]
        public void FormatLockedRequirement_WithPreviousTitle_ReferencesIt()
        {
            Assert.AreEqual("Hoàn thành Màn chơi 1 để mở.", LevelStatusFormatter.FormatLockedRequirement("Màn chơi 1"));
        }

        [Test]
        public void FormatLockedRequirement_NoPreviousTitle_ReturnsGenericLockedText()
        {
            Assert.AreEqual("Đã khoá", LevelStatusFormatter.FormatLockedRequirement(null));
        }
    }
}
