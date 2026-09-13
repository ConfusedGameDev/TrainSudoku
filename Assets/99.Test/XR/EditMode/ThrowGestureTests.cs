using NUnit.Framework;
using TrainSudoku.XR.Rules;

namespace TrainSudoku.XR.Tests
{
    /// <summary>XR-PRD 4.4: a release above the throw threshold is a throw.</summary>
    public class ThrowGestureTests
    {
        private const double Frame = 1.0 / 90.0;

        /// <summary>Moves the hand along X at <paramref name="speed"/> m/s for <paramref name="seconds"/>, one sample a frame.</summary>
        private static double Move(ThrowGesture gesture, double start, double seconds, double speed, ref double x)
        {
            var t = start;
            for (var frames = (int)(seconds / Frame); frames > 0; frames--)
            {
                t += Frame;
                x += speed * Frame;
                gesture.Add(t, x, 1.0, 0.5);
            }

            return t;
        }

        [Test]
        public void AStillHandIsNoThrow()
        {
            var gesture = new ThrowGesture();
            Assert.AreEqual(0, gesture.Speed);
            gesture.Add(0, 1, 1, 1);
            Assert.AreEqual(0, gesture.Speed);
            Assert.IsFalse(gesture.IsThrow());
        }

        [Test]
        public void MeasuresTheHandsSpeed()
        {
            var gesture = new ThrowGesture();
            var x = 0.0;
            Move(gesture, 0, 0.5, 2.0, ref x);
            Assert.AreEqual(2.0, gesture.Speed, 1e-6);
            Assert.AreEqual(2.0, gesture.Velocity.X, 1e-6);
            Assert.AreEqual(0, gesture.Velocity.Y, 1e-9);
            Assert.IsTrue(gesture.IsThrow());
        }

        [Test]
        public void ASlowReleaseIsNoThrow()
        {
            var gesture = new ThrowGesture();
            var x = 0.0;
            Move(gesture, 0, 0.5, 0.5, ref x);
            Assert.IsFalse(gesture.IsThrow());
            Assert.IsTrue(gesture.IsThrow(0.4), "the threshold is tunable");
        }

        [Test]
        public void OnlyTheWindowCounts()
        {
            var gesture = new ThrowGesture();
            var x = 0.0;
            var t = Move(gesture, 0, 0.3, 3.0, ref x);
            Move(gesture, t, 0.2, 0.0, ref x);
            Assert.AreEqual(0, gesture.Speed, 1e-9, "a hand that stopped before letting go did not throw");
        }

        [Test]
        public void AFlickAtTheEndIsAThrow()
        {
            var gesture = new ThrowGesture();
            var x = 0.0;
            var t = Move(gesture, 0, 1.0, 0.1, ref x);
            Move(gesture, t, 0.12, 2.5, ref x);
            Assert.IsTrue(gesture.IsThrow());
        }

        [Test]
        public void IgnoresASampleThatIsNotLater()
        {
            var gesture = new ThrowGesture();
            gesture.Add(0.00, 0, 0, 0);
            gesture.Add(0.05, 0.05, 0, 0);
            gesture.Add(0.05, 9, 9, 9);
            gesture.Add(0.01, -9, 0, 0);
            Assert.AreEqual(1.0, gesture.Speed, 1e-9);
        }

        [Test]
        public void ClearForgetsTheHand()
        {
            var gesture = new ThrowGesture();
            var x = 0.0;
            Move(gesture, 0, 0.5, 3.0, ref x);
            gesture.Clear();
            Assert.AreEqual(0, gesture.Speed);
            gesture.Add(0.1, 0, 0, 0);
            Assert.AreEqual(0, gesture.Speed, "a sample earlier than the cleared ones is accepted after a clear");
        }

        [Test]
        public void TheWindowMustBePositive()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ThrowGesture(0));
        }
    }
}
