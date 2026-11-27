using NUnit.Framework;
using TrainSudoku.XR.Rules;

namespace TrainSudoku.XR.Tests
{
    /// <summary>Pip's reactions in play: the web edition's timings, without its hint and idle nudge (X15).</summary>
    public class XRPipReactionsTests
    {
        [Test]
        public void WavesAtTheStartOfAStationAndSettles()
        {
            var pip = new XRPipReactions();
            var changes = 0;
            pip.Changed += () => changes++;

            pip.Begin();
            Assert.AreEqual(XRPipMood.Wave, pip.Mood);
            Assert.AreEqual(XRPipKeys.Start, pip.BubbleKey);

            pip.Update(2.4);
            Assert.AreEqual(XRPipMood.Wave, pip.Mood);
            pip.Update(0.2);
            Assert.AreEqual(XRPipMood.Idle, pip.Mood);
            Assert.IsNull(pip.BubbleKey);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void CheersForASatisfiedLineAndWorriesForAnExceededOne()
        {
            var pip = new XRPipReactions();
            pip.Begin();

            pip.LineCleared(true, 2);
            Assert.AreEqual(XRPipMood.Cheer, pip.Mood);
            Assert.AreEqual(XRPipKeys.RowClear, pip.BubbleKey);
            Assert.AreEqual(3, pip.BubbleNumber, "the bubble counts from one");

            pip.Update(1.7);
            Assert.AreEqual(XRPipMood.Cheer, pip.Mood);
            pip.Update(0.2);
            Assert.AreEqual(XRPipMood.Idle, pip.Mood);

            pip.LineExceeded(false, 0);
            Assert.AreEqual(XRPipMood.Worry, pip.Mood);
            Assert.AreEqual(XRPipKeys.ColumnOver, pip.BubbleKey);
            Assert.AreEqual(1, pip.BubbleNumber);
        }

        [Test]
        public void TheLaterEventWinsAndRestartsTheWindow()
        {
            var pip = new XRPipReactions();
            pip.Begin();
            pip.LineCleared(true, 0);
            pip.Update(1.5);
            pip.LineExceeded(true, 1);
            // The first reaction would have ended by now.
            pip.Update(1.5);
            Assert.AreEqual(XRPipMood.Worry, pip.Mood);
            pip.Update(0.5);
            Assert.AreEqual(XRPipMood.Idle, pip.Mood);
        }

        [Test]
        public void StopGoesQuietAndStaysQuiet()
        {
            var pip = new XRPipReactions();
            pip.Begin();
            pip.LineCleared(false, 4);
            pip.Stop();
            Assert.AreEqual(XRPipMood.Idle, pip.Mood);
            Assert.IsNull(pip.BubbleKey);

            // Nothing reacts until the next station begins.
            pip.LineExceeded(true, 0);
            pip.Update(100);
            Assert.AreEqual(XRPipMood.Idle, pip.Mood);
            Assert.IsNull(pip.BubbleKey);
        }

        [Test]
        public void TheSameViewTwiceRaisesNothing()
        {
            var pip = new XRPipReactions();
            pip.Begin();
            var changes = 0;
            pip.Changed += () => changes++;
            pip.LineCleared(true, 1);
            pip.LineCleared(true, 1);
            Assert.AreEqual(1, changes);
        }
    }
}
