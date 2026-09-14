using System;
using NUnit.Framework;
using TrainSudoku.XR.Rules;

namespace TrainSudoku.XR.Tests
{
    /// <summary>XR-PRD 5.4 and 6.2: a map fitted onto the platform card, the top of the phone's screen at the far side.</summary>
    public class MapFitTests
    {
        private const double Tolerance = 1e-9;

        private static MapFrame Fit((double X, double Y)[] points, double width = 10, double depth = 10,
            double marginX = 1, double marginZ = 1, double originX = 0, double originZ = 5, double maxScale = 1)
        {
            Assert.IsTrue(MapFit.TryFit(points, width, depth, marginX, marginZ, originX, originZ, maxScale, out var frame));
            return frame;
        }

        [Test]
        public void TheLongerSpanFillsItsRoom()
        {
            var frame = Fit(new[] { (0.0, 0.0), (200.0, 100.0) });
            Assert.AreEqual(8.0 / 200.0, frame.Scale, Tolerance);

            var tall = Fit(new[] { (0.0, 0.0), (100.0, 400.0) });
            Assert.AreEqual(8.0 / 400.0, tall.Scale, Tolerance);
        }

        [Test]
        public void TheMiddleOfTheMapLandsOnTheOrigin()
        {
            var frame = Fit(new[] { (100.0, 300.0), (500.0, 900.0) }, originX: 0.5, originZ: 5);
            var (x, z) = frame.ToPlatform(300, 600);
            Assert.AreEqual(0.5, x, Tolerance);
            Assert.AreEqual(5.0, z, Tolerance);
        }

        [Test]
        public void TheTopOfTheScreenIsTheFarSide()
        {
            var frame = Fit(new[] { (0.0, 0.0), (100.0, 100.0) });
            Assert.Greater(frame.ToPlatform(50, 0).Z, frame.ToPlatform(50, 100).Z);
            Assert.Greater(frame.ToPlatform(100, 50).X, frame.ToPlatform(0, 50).X, "x keeps its direction");
        }

        [Test]
        public void EveryPointStaysInsideTheMarginsAndTheAspectHolds()
        {
            var points = new[] { (-1350.0, -1050.0), (2550.0, 2850.0), (124.0, 256.0), (800.0, 1230.0) };
            var frame = Fit(points, width: 9.6, depth: 9.6, marginX: 0.8, marginZ: 0.6, originX: 0, originZ: 5);
            foreach (var (px, py) in points)
            {
                var (x, z) = frame.ToPlatform(px, py);
                Assert.LessOrEqual(Math.Abs(x), 4.8 - 0.8 + Tolerance, $"({px},{py}) across");
                Assert.LessOrEqual(Math.Abs(z - 5), 4.8 - 0.6 + Tolerance, $"({px},{py}) deep");
            }

            var (ax, az) = frame.ToPlatform(0, 0);
            var (bx, _) = frame.ToPlatform(100, 0);
            var (_, cz) = frame.ToPlatform(0, 100);
            Assert.AreEqual(bx - ax, az - cz, Tolerance);
        }

        [Test]
        public void ASmallMapIsNotBlownUpPastTheCap()
        {
            var frame = Fit(new[] { (0.0, 0.0), (10.0, 10.0) }, maxScale: 0.05);
            Assert.AreEqual(0.05, frame.Scale, Tolerance);
        }

        [Test]
        public void ASinglePointTakesTheCapAndSitsOnTheOrigin()
        {
            var frame = Fit(new[] { (40.0, 40.0) }, maxScale: 0.02, originX: 1, originZ: 4);
            Assert.AreEqual(0.02, frame.Scale, Tolerance);
            Assert.AreEqual((1.0, 4.0), frame.ToPlatform(40, 40));
        }

        [Test]
        public void AStraightLineFitsItsOnlySpan()
        {
            var frame = Fit(new[] { (0.0, 50.0), (400.0, 50.0) });
            Assert.AreEqual(8.0 / 400.0, frame.Scale, Tolerance);
        }

        [Test]
        public void NoPointsOrNoRoomDoesNotFit()
        {
            Assert.IsFalse(MapFit.TryFit(Array.Empty<(double, double)>(), 10, 10, 1, 1, 0, 0, 1, out _));
            Assert.IsFalse(MapFit.TryFit(null, 10, 10, 1, 1, 0, 0, 1, out _));
            Assert.IsFalse(MapFit.TryFit(new[] { (0.0, 0.0), (1.0, 1.0) }, 10, 10, 5, 1, 0, 0, 1, out _));
        }
    }
}
