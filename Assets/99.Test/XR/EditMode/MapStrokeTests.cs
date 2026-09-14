using System;
using System.Linq;
using NUnit.Framework;
using TrainSudoku.XR.Rules;

namespace TrainSudoku.XR.Tests
{
    /// <summary>XR-PRD 6.2: the polyline each line's colour is laid along, the phone's three map shapes.</summary>
    public class MapStrokeTests
    {
        private const double Tolerance = 1e-6;

        private static readonly (double X, double Y)[] Zigzag = { (0, 0), (150, 0), (300, 150), (300, 300) };

        /// <summary>Thornemoor's stadium box: 520 across, 900 tall.</summary>
        private static readonly (double X, double Y)[] TallBox = { (540, 330), (800, 600), (540, 1230), (280, 900) };

        [Test]
        public void ARouteIsItsNodes()
        {
            CollectionAssert.AreEqual(Zigzag, MapStroke.Points(StrokeShape.Route, Zigzag));
        }

        [Test]
        public void ALoopClosesOnItsFirstNode()
        {
            var points = MapStroke.Points(StrokeShape.Loop, Zigzag);
            Assert.AreEqual(Zigzag.Length + 1, points.Count);
            Assert.AreEqual(Zigzag[0], points[points.Count - 1]);
        }

        [Test]
        public void AStadiumIsClosedAndStartsAtTheMiddleOfItsTop()
        {
            var points = MapStroke.Points(StrokeShape.Stadium, TallBox);
            Assert.AreEqual((540.0, 330.0), points[0]);
            Assert.AreEqual(points[0], points[points.Count - 1]);
        }

        [Test]
        public void AStadiumTouchesEverySideOfItsBoxAndNeverLeavesIt()
        {
            var points = MapStroke.Points(StrokeShape.Stadium, TallBox);
            foreach (var (x, y) in points)
            {
                Assert.That(x, Is.InRange(280 - Tolerance, 800 + Tolerance));
                Assert.That(y, Is.InRange(330 - Tolerance, 1230 + Tolerance));
            }

            Assert.AreEqual(280, points.Min(p => p.X), Tolerance);
            Assert.AreEqual(800, points.Max(p => p.X), Tolerance);
            Assert.AreEqual(330, points.Min(p => p.Y), Tolerance);
            Assert.AreEqual(1230, points.Max(p => p.Y), Tolerance);
        }

        [Test]
        public void AStadiumsShortEndsAreSemicircles()
        {
            // 520 across, so the radius is 260 and both top corners share the centre (540, 590).
            var points = MapStroke.Points(StrokeShape.Stadium, TallBox);
            var cap = points.Where(p => p.Y < 590 - Tolerance).ToList();
            Assert.IsNotEmpty(cap);
            foreach (var (x, y) in cap)
                Assert.AreEqual(260, Math.Sqrt((x - 540) * (x - 540) + (y - 590) * (y - 590)), Tolerance);
        }

        [Test]
        public void AStadiumNeverRepeatsAPoint()
        {
            var points = MapStroke.Points(StrokeShape.Stadium, TallBox);
            for (var i = 1; i < points.Count; i++)
                Assert.Greater(Math.Abs(points[i].X - points[i - 1].X) + Math.Abs(points[i].Y - points[i - 1].Y), Tolerance, $"point {i}");
        }

        [Test]
        public void AFlatStadiumIsStrokedOpenThroughItsNodes()
        {
            var flat = new[] { (0.0, 50.0), (150.0, 50.0), (300.0, 50.0) };
            CollectionAssert.AreEqual(flat, MapStroke.Points(StrokeShape.Stadium, flat));
        }

        [Test]
        public void NoNodesNoStroke()
        {
            Assert.IsEmpty(MapStroke.Points(StrokeShape.Route, Array.Empty<(double, double)>()));
            Assert.IsEmpty(MapStroke.Points(StrokeShape.Stadium, null));
        }
    }
}
