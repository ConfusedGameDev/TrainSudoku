using NUnit.Framework;
using TrainSudoku.XR.Rules;

namespace TrainSudoku.XR.Tests
{
    /// <summary>XR-PRD 7: the lesson in moving the board, from the ghost hands to the note about placing it again.</summary>
    public class XRBoardLessonTests
    {
        private static XRBoardLesson Begun()
        {
            var lesson = new XRBoardLesson();
            lesson.Begin();
            return lesson;
        }

        [Test]
        public void ItIsIdleUntilBegun()
        {
            var lesson = new XRBoardLesson();
            Assert.IsFalse(lesson.Active);
            Assert.IsFalse(lesson.HandsShown);
            Assert.IsNull(lesson.Key);
            Assert.IsFalse(lesson.Observe(true, 2, true), "an idle lesson watches nothing");
            Assert.AreEqual(XRBoardLessonStep.Idle, lesson.Step);
        }

        [Test]
        public void ItOpensWithTheGhostHandsWaitingOpen()
        {
            var lesson = Begun();
            Assert.AreEqual(XRBoardLessonStep.Approach, lesson.Step);
            Assert.IsTrue(lesson.Active);
            Assert.IsTrue(lesson.HandsShown);
            Assert.IsFalse(lesson.HandsPinching);
            Assert.AreEqual(XRTutorialKeys.MoveApproach, lesson.Key);
        }

        [Test]
        public void AHandComingNearAsksForThePinch()
        {
            var lesson = Begun();
            Assert.IsFalse(lesson.Observe(false, 0, false));
            Assert.IsTrue(lesson.Observe(true, 0, false));
            Assert.AreEqual(XRBoardLessonStep.Pinch, lesson.Step);
            Assert.IsTrue(lesson.HandsPinching);
            Assert.AreEqual(XRTutorialKeys.MovePinch, lesson.Key);
        }

        [Test]
        public void TheHandGoingAwayPutsTheHandsBackToWaiting()
        {
            var lesson = Begun();
            lesson.Observe(true, 0, false);
            Assert.IsTrue(lesson.Observe(false, 0, false));
            Assert.AreEqual(XRBoardLessonStep.Approach, lesson.Step);
        }

        [Test]
        public void OneHandOnTheRailStillAsksForBoth()
        {
            var lesson = Begun();
            lesson.Observe(false, 1, false);
            Assert.AreEqual(XRBoardLessonStep.Pinch, lesson.Step);
            lesson.Observe(false, 1, false);
            Assert.AreEqual(XRBoardLessonStep.Pinch, lesson.Step, "a hand on the rail is near it, whatever the distance check says");
        }

        [Test]
        public void BothHandsOnTheRailExplainCarryingAndResizing()
        {
            var lesson = Begun();
            lesson.Observe(true, 0, false);
            Assert.IsTrue(lesson.Observe(true, 2, false));
            Assert.AreEqual(XRBoardLessonStep.Carry, lesson.Step);
            Assert.IsFalse(lesson.HandsShown);
            Assert.AreEqual(XRTutorialKeys.MoveCarry, lesson.Key);
        }

        [Test]
        public void LettingGoWithoutMovingAsksForThePinchAgain()
        {
            var lesson = Begun();
            lesson.Observe(true, 2, false);
            Assert.IsTrue(lesson.Observe(true, 1, false));
            Assert.AreEqual(XRBoardLessonStep.Pinch, lesson.Step);
        }

        [Test]
        public void LettingGoAfterMovingReachesTheClosingCard()
        {
            var lesson = Begun();
            lesson.Observe(true, 2, false);
            lesson.Observe(true, 2, true);
            // The release ends the move in the same frame, so the board is no longer moving when it is seen.
            Assert.IsTrue(lesson.Observe(true, 1, false));
            Assert.AreEqual(XRBoardLessonStep.Closing, lesson.Step);
            Assert.IsNull(lesson.Key, "the signboard alone carries the closing card");
            Assert.IsFalse(lesson.HandsShown);
        }

        [Test]
        public void ABoardAlreadyMovingWhenBothHandsAreSeenCounts()
        {
            var lesson = Begun();
            lesson.Observe(false, 2, true);
            lesson.Observe(false, 0, false);
            Assert.AreEqual(XRBoardLessonStep.Closing, lesson.Step);
        }

        [Test]
        public void SkipGoesToTheClosingCardNotOut()
        {
            var lesson = Begun();
            Assert.IsTrue(lesson.Skip());
            Assert.AreEqual(XRBoardLessonStep.Closing, lesson.Step);
            Assert.IsTrue(lesson.Active);
            Assert.IsFalse(lesson.Skip(), "already there");
        }

        [Test]
        public void TheClosingCardIgnoresTheHands()
        {
            var lesson = Begun();
            lesson.Skip();
            Assert.IsFalse(lesson.Observe(true, 2, true));
            Assert.AreEqual(XRBoardLessonStep.Closing, lesson.Step);
        }

        [Test]
        public void OnlyTheClosingCardsButtonEndsIt()
        {
            var lesson = Begun();
            Assert.IsFalse(lesson.Acknowledge(), "there is no such button before the closing card");
            Assert.IsTrue(lesson.Active);

            lesson.Skip();
            Assert.IsTrue(lesson.Acknowledge());
            Assert.IsFalse(lesson.Active);
            Assert.IsFalse(lesson.Acknowledge());
            Assert.IsFalse(lesson.Skip());
        }

        [Test]
        public void EndDropsItAndItCanBeginAgain()
        {
            var lesson = Begun();
            lesson.Observe(true, 2, true);
            lesson.End();
            Assert.IsFalse(lesson.Active);

            lesson.Begin();
            lesson.Observe(true, 2, false);
            lesson.Observe(true, 0, false);
            Assert.AreEqual(XRBoardLessonStep.Pinch, lesson.Step, "the earlier carry does not count for the new run");
        }
    }
}
