using System;
using NUnit.Framework;
using TrainSudoku.XR.Rules;

namespace TrainSudoku.XR.Tests
{
    /// <summary>XR-PRD 6.4: the wrist button shows while the back of the wrist is turned to the eyes, like a watch.</summary>
    public class WatchCheckTests
    {
        private static readonly (double, double, double) Back = (0, 1, 0);

        /// <summary>The way to the eyes, <paramref name="degrees"/> off the back of the wrist.</summary>
        private static (double, double, double) At(double degrees)
        {
            var radians = degrees * Math.PI / 180;
            return (2 * Math.Sin(radians), 2 * Math.Cos(radians), 0);
        }

        [Test]
        public void AWristTurnedToTheEyesShows()
        {
            Assert.IsTrue(new WatchCheck().Update(true, Back, At(10)));
        }

        [Test]
        public void APalmTurnedToTheEyesDoesNot()
        {
            Assert.IsFalse(new WatchCheck().Update(true, Back, At(180)));
        }

        [Test]
        public void AnUntrackedWristNeverShows()
        {
            var watch = new WatchCheck();
            watch.Update(true, Back, At(0));
            Assert.IsFalse(watch.Update(false, Back, At(0)));
            Assert.IsFalse(watch.Showing);
        }

        [Test]
        public void BetweenTheLimitsItKeepsWhatItWas()
        {
            var watch = new WatchCheck();
            Assert.IsFalse(watch.Update(true, Back, At(55)), "not shown yet: 55 degrees is outside the show limit");
            Assert.IsTrue(watch.Update(true, Back, At(40)));
            Assert.IsTrue(watch.Update(true, Back, At(55)), "shown: 55 degrees is inside the hide limit");
            Assert.IsFalse(watch.Update(true, Back, At(70)));
        }

        [Test]
        public void ResetHidesIt()
        {
            var watch = new WatchCheck();
            watch.Update(true, Back, At(0));
            watch.Reset();
            Assert.IsFalse(watch.Showing);
            Assert.IsFalse(watch.Update(true, Back, At(55)), "after a reset the show limit applies again");
        }

        [Test]
        public void ADirectionOfNoLengthHides()
        {
            Assert.IsFalse(new WatchCheck().Update(true, (0, 0, 0), At(0)));
            Assert.IsFalse(new WatchCheck().Update(true, Back, (0, 0, 0)));
        }
    }
}
