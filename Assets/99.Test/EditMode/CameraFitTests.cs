using System;
using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class CameraFitTests
    {
        private const double Eps = 1e-9;

        private static (double X, double Y, double Z)[] BoxCorners(double hw, double hd, double h) => new[]
        {
            (-hw, 0.0, -hd), (hw, 0.0, -hd), (-hw, 0.0, hd), (hw, 0.0, hd),
            (-hw, h, -hd), (hw, h, -hd), (-hw, h, hd), (hw, h, hd),
        };

        /// <summary>Projects the box corners with the placement and checks they fall inside the allowed viewport share.</summary>
        private static void AssertCornersFit(CameraPlacement placement, double hw, double hd, double h, double pitchDeg, double fovDeg, double aspect, double hFrac, double vFrac, out double slack)
        {
            var pitch = pitchDeg * Math.PI / 180.0;
            var tanV = Math.Tan(fovDeg * Math.PI / 360.0);
            var tanH = tanV * aspect;
            slack = double.MaxValue;
            foreach (var (x, y, z) in BoxCorners(hw, hd, h))
            {
                var (cx, cy, cz) = CameraFit.ToCameraSpace(x, y, z, pitch);
                var depth = cz + placement.Distance;
                Assert.Greater(depth, 0, "Every corner is in front of the camera");
                var nx = cx / (depth * tanH);
                var ny = cy / (depth * tanV);
                Assert.LessOrEqual(Math.Abs(nx), hFrac + Eps, $"Corner ({x},{y},{z}) horizontal");
                Assert.LessOrEqual(Math.Abs(ny), vFrac + Eps, $"Corner ({x},{y},{z}) vertical");
                slack = Math.Min(slack, Math.Min(hFrac - Math.Abs(nx), vFrac - Math.Abs(ny)));
            }
        }

        [TestCase(4.9, 4.9, 60, 60, 9.0 / 16.0, 1.0, 1.0)]
        [TestCase(4.9, 4.9, 60, 60, 9.0 / 16.0, 1.0, 0.8)]
        [TestCase(4.9, 4.9, 60, 60, 16.0 / 9.0, 1.0, 1.0)]
        [TestCase(9.9, 3.4, 55, 60, 9.0 / 16.0, 0.9, 0.9)]
        [TestCase(2.9, 9.9, 65, 45, 1.0, 1.0, 1.0)]
        [TestCase(4.9, 4.9, 90, 60, 9.0 / 16.0, 1.0, 1.0)]
        public void CornersFitAndOneEdgeTouchesTheLimit(double hw, double hd, double pitch, double fov, double aspect, double hFrac, double vFrac)
        {
            var placement = CameraFit.Solve(hw, hd, 0, pitch, fov, aspect, hFrac, vFrac);
            AssertCornersFit(placement, hw, hd, 0, pitch, fov, aspect, hFrac, vFrac, out var slack);
            Assert.AreEqual(0, slack, 1e-7, "The distance is minimal: some corner is exactly on the limit");
        }

        [TestCase(4.9, 4.9, 1.5, 60, 60, 9.0 / 16.0, 0.94, 0.81)]
        [TestCase(7.9, 7.4, 1.5, 60, 60, 9.0 / 16.0, 0.94, 0.81)]
        [TestCase(4.9, 4.9, 1.5, 60, 60, 16.0 / 9.0, 0.94, 0.81)]
        [TestCase(4.9, 4.9, 3.0, 45, 60, 1.0, 1.0, 1.0)]
        [TestCase(4.9, 4.9, 1.5, 90, 60, 1.0, 1.0, 1.0)]
        public void BoxCornersFitAndOneTouchesTheLimit(double hw, double hd, double h, double pitch, double fov, double aspect, double hFrac, double vFrac)
        {
            var placement = CameraFit.Solve(hw, hd, h, pitch, fov, aspect, hFrac, vFrac);
            AssertCornersFit(placement, hw, hd, h, pitch, fov, aspect, hFrac, vFrac, out var slack);
            Assert.AreEqual(0, slack, 1e-7, "The distance is minimal: some corner is exactly on the limit");
        }

        [Test]
        public void HeightNeedsMoreDistanceThanTheFlatRectangle()
        {
            var flat = CameraFit.Solve(4.9, 4.9, 0, 60, 60, 9.0 / 16.0);
            var box = CameraFit.Solve(4.9, 4.9, 1.5, 60, 60, 9.0 / 16.0);
            Assert.Greater(box.Distance, flat.Distance);
        }

        [TestCase(4.9, 4.9, 1.5, 60, 9.0 / 16.0, 0.94, 0.81)]
        [TestCase(7.9, 7.4, 1.5, 60, 9.0 / 16.0, 0.94, 0.81)]
        [TestCase(4.9, 4.9, 1.5, 60, 16.0 / 9.0, 1.0, 1.0)]
        [TestCase(4.9, 4.9, 0.0, 90, 1.0, 1.0, 1.0)]
        public void OrthographicSizeShowsEveryCornerAndOneTouchesTheLimit(double hw, double hd, double h, double pitch, double aspect, double hFrac, double vFrac)
        {
            var size = CameraFit.SolveOrthographicSize(hw, hd, h, pitch, aspect, hFrac, vFrac);
            var pitchRad = pitch * Math.PI / 180.0;
            var slack = double.MaxValue;
            foreach (var (x, y, z) in BoxCorners(hw, hd, h))
            {
                var (cx, cy, _) = CameraFit.ToCameraSpace(x, y, z, pitchRad);
                var nx = cx / (size * aspect);
                var ny = cy / size;
                Assert.LessOrEqual(Math.Abs(nx), hFrac + Eps, $"Corner ({x},{y},{z}) horizontal");
                Assert.LessOrEqual(Math.Abs(ny), vFrac + Eps, $"Corner ({x},{y},{z}) vertical");
                slack = Math.Min(slack, Math.Min(hFrac - Math.Abs(nx), vFrac - Math.Abs(ny)));
            }

            Assert.AreEqual(0, slack, 1e-7, "The size is minimal");
        }

        [Test]
        public void OrthographicPortraitIsBoundByTheWidth()
        {
            // A 6x6 board with its row clue labels spans about 4.9 units either side of centre once built. On a 9:16
            // screen that half-width must fit in size * aspect, so the size is at least 4.9 / 0.5625.
            const double contentHalfWidth = 4.9;
            var size = CameraFit.SolveOrthographicSize(contentHalfWidth, BoardLayout.HalfDepth(6), BoardLayout.Height, 60, 9.0 / 16.0);
            Assert.AreEqual(contentHalfWidth / (9.0 / 16.0), size, Eps);
            Assert.Greater(size, 8.29, "The old hand-set orthographic size cut the sides off in portrait");
            Assert.Greater(size, CameraFit.SolveOrthographicSize(BoardLayout.HalfWidth(6), BoardLayout.HalfDepth(6), BoardLayout.Height, 60, 9.0 / 16.0), "The minimum box alone is smaller than the built content");
        }

        [Test]
        public void ElevatedPointsProjectHigherAndCloser()
        {
            var pitch = 60 * Math.PI / 180.0;
            var (gx, gy, gz) = CameraFit.ToCameraSpace(1, 0, 2, pitch);
            var (ex, ey, ez) = CameraFit.ToCameraSpace(1, 1, 2, pitch);
            Assert.AreEqual(gx, ex, Eps);
            Assert.Greater(ey, gy);
            Assert.Less(ez, gz);
            var (_, fy, fz) = CameraFit.ToCameraSpace(1, 2, pitch);
            Assert.AreEqual(gy, fy, Eps, "The ground overload is the y = 0 case");
            Assert.AreEqual(gz, fz, Eps);
        }

        [Test]
        public void PlacementDecomposesAlongThePitch()
        {
            var placement = CameraFit.Solve(3, 3, 0, 60, 60, 0.5625);
            Assert.AreEqual(placement.Distance * Math.Sin(60 * Math.PI / 180), placement.Height, Eps);
            Assert.AreEqual(placement.Distance * Math.Cos(60 * Math.PI / 180), placement.Back, Eps);
            Assert.Greater(placement.Height, placement.Back, "At 60 degrees the camera is more above than behind");
        }

        [Test]
        public void PortraitFitsTheWidthAndWiderBoardsNeedMoreDistance()
        {
            var narrow = CameraFit.Solve(BoardLayout.HalfWidth(6), BoardLayout.HalfDepth(6), 0, 60, 60, 9.0 / 16.0);
            var wide = CameraFit.Solve(BoardLayout.HalfWidth(8), BoardLayout.HalfDepth(6), 0, 60, 60, 9.0 / 16.0);
            var tall = CameraFit.Solve(BoardLayout.HalfWidth(6), BoardLayout.HalfDepth(8), 0, 60, 60, 9.0 / 16.0);
            Assert.Greater(wide.Distance, narrow.Distance);
            Assert.GreaterOrEqual(tall.Distance, narrow.Distance);
            Assert.Greater(wide.Distance - narrow.Distance, tall.Distance - narrow.Distance, "In portrait the width is the binding constraint");
        }

        [Test]
        public void TopDownViewReducesToPlaneGeometry()
        {
            // Looking straight down with a square viewport, a square of half-size 1 at 90 degrees fov needs distance 1.
            var placement = CameraFit.Solve(1, 1, 0, 90, 90, 1);
            Assert.AreEqual(1, placement.Distance, 1e-9);
            Assert.AreEqual(1, placement.Height, 1e-9);
            Assert.AreEqual(0, placement.Back, 1e-9);
        }

        [Test]
        public void RejectsDegenerateInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFit.Solve(1, 1, 0, 0, 60, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFit.Solve(1, 1, 0, 91, 60, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFit.Solve(1, 1, 0, 60, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFit.Solve(1, 1, 0, 60, 60, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFit.Solve(-1, 1, 0, 60, 60, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFit.Solve(1, 1, 0, 60, 60, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFit.Solve(1, 1, 0, 60, 60, 1, 1, 1.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFit.Solve(1, 1, -0.5, 60, 60, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFit.SolveOrthographicSize(1, 1, 1, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFit.SolveOrthographicSize(1, 1, 1, 60, 1, 1, 0));
        }
    }
}
