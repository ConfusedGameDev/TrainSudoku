using System;
using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class TrackPathTests
    {
        private const double Eps = 1e-9;

        private static TrackPath SolvedPath()
        {
            Assert.IsTrue(TrackPath.TryBuild(TestLevels.SolvedPlanExample(), out var path));
            return path;
        }

        [Test]
        public void UnconnectedBoardHasNoPath()
        {
            Assert.IsFalse(TrackPath.TryBuild(new Board(TestLevels.PlanExample()), out var path));
            Assert.IsNull(path);
            Assert.IsFalse(TrackPath.TryBuild(TestLevels.SolvedPlanExample((2, 4)), out _), "one missing piece breaks the route");
        }

        [Test]
        public void LengthIsTheSumOfTheCellCurvesPlusBothTunnelExtensions()
        {
            var path = SolvedPath();
            // Solution: 5 straights (NS, EW, EW, NS, EW) and 6 quarter circles.
            var expected = 2 * TrackPath.TunnelExtension + 5 * 1.0 + 6 * Math.PI / 4;
            Assert.AreEqual(expected, path.Length, Eps);
            Assert.AreEqual(TestLevels.PlanExampleSolution.Length, path.Cells.Count);
            Assert.AreEqual((0, 3), path.Cells[0], "starts at the cell inside the entrance");
            Assert.AreEqual((5, 2), path.Cells[path.Cells.Count - 1], "ends at the cell inside the exit");
        }

        [Test]
        public void StartsOneCellOutsideTheEntranceHeadingIn()
        {
            var path = SolvedPath();
            var start = path.Sample(0);
            // Entrance is west of cell (0,3): centre (-2.5, -0.5), so the tunnel end is 1.5 further west.
            Assert.AreEqual(-4.0, start.X, Eps);
            Assert.AreEqual(-0.5, start.Z, Eps);
            Assert.AreEqual(1, start.TangentX, Eps);
            Assert.AreEqual(0, start.TangentZ, Eps);

            var mouth = path.Sample(TrackPath.TunnelExtension);
            Assert.AreEqual(-3.0, mouth.X, Eps, "the extension ends at the west side midpoint of (0,3)");
            Assert.AreEqual(-0.5, mouth.Z, Eps);
        }

        [Test]
        public void EndsOneCellOutsideTheExitHeadingOut()
        {
            var path = SolvedPath();
            var end = path.Sample(path.Length);
            // Exit is east of cell (5,2): centre (2.5, 0.5).
            Assert.AreEqual(4.0, end.X, Eps);
            Assert.AreEqual(0.5, end.Z, Eps);
            Assert.AreEqual(1, end.TangentX, Eps);
            Assert.AreEqual(0, end.TangentZ, Eps);
        }

        [Test]
        public void SamplesAreContinuousAndMoveAtUnitSpeed()
        {
            var path = SolvedPath();
            const double step = 0.01;
            var previous = path.Sample(0);
            for (var s = step; s <= path.Length + Eps; s += step)
            {
                var current = path.Sample(Math.Min(s, path.Length));
                var dx = current.X - previous.X;
                var dz = current.Z - previous.Z;
                var moved = Math.Sqrt(dx * dx + dz * dz);
                Assert.AreEqual(Math.Min(s, path.Length) - (s - step), moved, 1e-4, $"arc length step at s={s}");
                var tangentLength = Math.Sqrt(current.TangentX * current.TangentX + current.TangentZ * current.TangentZ);
                Assert.AreEqual(1, tangentLength, Eps, "unit tangent");
                previous = current;
            }
        }

        [Test]
        public void PassesThroughEveryCellCentreOfAStraightAndTheMidpointsBetweenCells()
        {
            var path = SolvedPath();
            // The first cell (0,3) holds NW entered from the west: after the extension (1.0) and the quarter circle
            // (pi/4) the route is at the north side midpoint of (0,3), which is the south midpoint of (0,2).
            var join = path.Sample(TrackPath.TunnelExtension + Math.PI / 4);
            Assert.AreEqual(-2.5, join.X, Eps);
            Assert.AreEqual(0.0, join.Z, Eps);
            Assert.AreEqual(0, join.TangentX, Eps);
            Assert.AreEqual(1, join.TangentZ, Eps, "heading north into (0,2)");
        }

        [Test]
        public void ExtrapolatesStraightBeyondBothEnds()
        {
            var path = SolvedPath();
            var before = path.Sample(-2);
            Assert.AreEqual(-6.0, before.X, Eps);
            Assert.AreEqual(-0.5, before.Z, Eps);
            var after = path.Sample(path.Length + 3);
            Assert.AreEqual(7.0, after.X, Eps);
            Assert.AreEqual(0.5, after.Z, Eps);
        }
    }
}
