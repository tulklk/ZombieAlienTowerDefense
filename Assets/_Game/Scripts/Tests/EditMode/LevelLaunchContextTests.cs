using AlienDefense.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.EditMode
{
    public class LevelLaunchContextTests
    {
        [Test]
        public void InitialState_HasNoSelection()
        {
            var context = new LevelLaunchContext();

            Assert.IsFalse(context.HasSelection);
            Assert.IsNull(context.SelectedLevelId);
        }

        [Test]
        public void SetSelectedLevel_ThenRead_ReturnsSameId()
        {
            var context = new LevelLaunchContext();

            context.SetSelectedLevel("level_02");

            Assert.IsTrue(context.HasSelection);
            Assert.AreEqual("level_02", context.SelectedLevelId);
        }

        [Test]
        public void SetSelectedLevel_ReadAgain_DoesNotClear()
        {
            var context = new LevelLaunchContext();
            context.SetSelectedLevel("level_01");

            string firstRead = context.SelectedLevelId;
            string secondRead = context.SelectedLevelId;

            Assert.AreEqual("level_01", firstRead);
            Assert.AreEqual("level_01", secondRead);
            Assert.IsTrue(context.HasSelection, "Reading SelectedLevelId must not consume/clear it (Retry needs it to survive a re-read).");
        }

        [Test]
        public void Clear_RemovesSelection()
        {
            var context = new LevelLaunchContext();
            context.SetSelectedLevel("level_01");

            context.Clear();

            Assert.IsFalse(context.HasSelection);
            Assert.IsNull(context.SelectedLevelId);
        }

        [Test]
        public void SetSelectedLevel_EmptyId_IsRejected_AndLogsError()
        {
            var context = new LevelLaunchContext();

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("empty"));
            context.SetSelectedLevel("");

            Assert.IsFalse(context.HasSelection);
        }

        [Test]
        public void SetSelectedLevel_Twice_OverwritesPreviousSelection()
        {
            var context = new LevelLaunchContext();
            context.SetSelectedLevel("level_01");

            context.SetSelectedLevel("level_02");

            Assert.AreEqual("level_02", context.SelectedLevelId);
        }
    }
}
