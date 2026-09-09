using System;
using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class TrackCurveTests
    {
        private const double Eps = 1e-9;

        private static void AssertPoint((double X, double Z) expected, (double X, double Z) actual, string message)
        {
            Assert.AreEqual(expected.X, actual.X, Eps, message + " X");
            Assert.AreEqual(expected.Z, actual.Z, Eps, message + " Z");
        }

        private static (double X, double Z) Negate((double X, double Z) v) => (-v.X, -v.Z);

        [Test]
        public void EveryCurveRunsFromOneSideMidpointToTheOther()
        {
            foreach (var key in PieceKeys.All)
            foreach (var from in DirectionExtensions.All)
            {
                if (!PieceKeys.Has(key, from)) continue;
                var curve = TrackCurve.For(key, from);
                Assert.AreEqual(PieceKeys.Other(key, from), curve.To);
                AssertPoint(TrackCurve.SideMidpoint(from), curve.Position(0), $"{curve} start");
                AssertPoint(TrackCurve.SideMidpoint(curve.To), curve.Position(1), $"{curve} end");
            }
        }

        [Test]
        public void TangentsPointIntoTheCellAtTheStartAndOutAtTheEnd()
        {
            foreach (var key in PieceKeys.All)
            foreach (var from in DirectionExtensions.All)
            {
                if (!PieceKeys.Has(key, from)) continue;
                var curve = TrackCurve.For(key, from);
                AssertPoint(Negate(BoardLayout.Step(from)), curve.Tangent(0), $"{curve} entry tangent");
                AssertPoint(BoardLayout.Step(curve.To), curve.Tangent(1), $"{curve} exit tangent");
            }
        }

        [Test]
        public void StraightsPassThroughTheCentre()
        {
            var ns = TrackCurve.For(PieceKey.NS);
            Assert.IsTrue(ns.IsStraight);
            Assert.AreEqual(1, ns.Length, Eps);
            AssertPoint((0, 0), ns.Position(0.5), "NS middle");
            AssertPoint((0, -1), ns.Tangent(0.3), "NS travels south");

            var we = TrackCurve.For(PieceKey.EW, Direction.West);
            AssertPoint((-0.5, 0), we.Position(0), "from west");
            AssertPoint((1, 0), we.Tangent(0.9), "travels east");
        }

        [Test]
        public void CurvesAreQuarterCirclesAroundTheSharedCorner()
        {
            foreach (var key in new[] { PieceKey.NE, PieceKey.NW, PieceKey.SE, PieceKey.SW })
            {
                var curve = TrackCurve.For(key);
                Assert.IsFalse(curve.IsStraight);
                Assert.AreEqual(Math.PI / 4, curve.Length, Eps);

                var (ax, az) = TrackCurve.SideMidpoint(curve.From);
                var (bx, bz) = TrackCurve.SideMidpoint(curve.To);
                var corner = (X: ax + bx, Z: az + bz);
                Assert.AreEqual(0.5, Math.Abs(corner.X), Eps);
                Assert.AreEqual(0.5, Math.Abs(corner.Z), Eps);

                for (var i = 0; i <= 10; i++)
                {
                    var t = i / 10.0;
                    var (px, pz) = curve.Position(t);
                    var radius = Math.Sqrt((px - corner.X) * (px - corner.X) + (pz - corner.Z) * (pz - corner.Z));
                    Assert.AreEqual(TrackCurve.Radius, radius, Eps, $"{curve} radius at t={t}");
                    Assert.LessOrEqual(Math.Abs(px), 0.5 + Eps, "stays inside the cell");
                    Assert.LessOrEqual(Math.Abs(pz), 0.5 + Eps, "stays inside the cell");

                    var (tx, tz) = curve.Tangent(t);
                    Assert.AreEqual(1, Math.Sqrt(tx * tx + tz * tz), Eps, "unit tangent");
                    var dot = (px - corner.X) * tx + (pz - corner.Z) * tz;
                    Assert.AreEqual(0, dot, Eps, "tangent is perpendicular to the radius");
                }
            }
        }

        [Test]
        public void CurveBulgesTowardsTheCellCentre()
        {
            var (x, z) = TrackCurve.For(PieceKey.NE).Position(0.5);
            Assert.Less(x, 0.5 - 0.3, "the midpoint is well inside from the east side");
            Assert.Less(z, 0.5 - 0.3, "and from the north side");
            Assert.Greater(x, 0);
            Assert.Greater(z, 0);
        }

        [Test]
        public void ReversedCurveRetracesTheSamePath()
        {
            var forward = TrackCurve.For(PieceKey.SW, Direction.South);
            var back = forward.Reversed();
            Assert.AreEqual(Direction.West, back.From);
            Assert.AreEqual(Direction.South, back.To);
            for (var i = 0; i <= 4; i++)
            {
                var t = i / 4.0;
                AssertPoint(forward.Position(t), back.Position(1 - t), $"t={t}");
                AssertPoint(Negate(forward.Tangent(t)), back.Tangent(1 - t), $"tangent t={t}");
            }
        }

        [Test]
        public void RejectsASideThePieceDoesNotConnect()
        {
            Assert.Throws<ArgumentException>(() => TrackCurve.For(PieceKey.NS, Direction.East));
        }
    }
}
