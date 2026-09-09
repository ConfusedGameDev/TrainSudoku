using System;
using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class CameraFitTests
    {
        private const double Eps = 1e-9;

        /// <summary>Projects the rectangle corners with the placement and checks they fall inside the allowed viewport share.</summary>
        private static void AssertCornersFit(CameraPlacement placement, double hw, double hd, double pitchDeg, double fovDeg, double aspect, double hFrac, double vFrac, out double slack)
        {
            var pitch = pitchDeg * Math.PI / 180.0;
            var tanV = Math.Tan(fovDeg * Math.PI / 360.0);
            var tanH = tanV * aspect;
            slack = double.MaxValue;
            foreach (var (x, z) in new[] { (-hw, -hd), (hw, -hd), (-hw, hd), (hw, hd) })
            {
                var (cx, cy, cz) = CameraFit.ToCameraSpace(x, z, pitch);
                var depth = cz + placement.Distance;
                Assert.Greater(depth, 0, "Every corner is in front of the camera");
                var nx = cx / (depth * tanH);
                var ny = cy / (depth * tanV);
                Assert.LessOrEqual(Math.Abs(nx), hFrac + Eps, $"Corner ({x},{z}) horizontal");
                Assert.LessOrEqual(Math.Abs(ny), vFrac + Eps, $"Corner ({x},{z}) vertical");
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
            var placement = CameraFit.Solve(hw, hd, pitch, fov, aspect, hFrac, vFrac);
            AssertCornersFit(placement, hw, hd, pitch, fov, aspect, hFrac, vFrac, out var slack);
            Assert.AreEqual(0, slack, 1e-7, "The distance is minimal: some corner is exactly on the limit");
        }

        [Test]
        public void PlacementDecomposesAlongThePitch()
        {
            var placement = CameraFit.Solve(3, 3, 60, 60, 0.5625);
            Assert.AreEqual(placement.Distance * Math.Sin(60 * Math.PI / 180), placement.Height, Eps);
            Assert.AreEqual(placement.Distance * Math.Cos(60 * Math.PI / 180), placement.Back, Eps);
            Assert.Greater(placement.Height, placement.Back, "At 60 degrees the camera is more above than behind");
        }

        [Test]
        public void PortraitFitsTheWidthAndWiderBoardsNeedMoreDistance()
        {
            var narrow = CameraFit.Solve(BoardLayout.HalfWidth(6), BoardLayout.HalfDepth(6), 60, 60, 9.0 / 16.0);
            var wide = CameraFit.Solve(BoardLayout.HalfWidth(8), BoardLayout.HalfDepth(6), 60, 60, 9.0 / 16.0);
            var tall = CameraFit.Solve(BoardLayout.HalfWidth(6), BoardLayout.HalfDepth(8), 60, 60, 9.0 / 16.0);
            Assert.Greater(wide.Distance, narrow.Distance);
            Assert.GreaterOrEqual(tall.Distance, narrow.Distance);
            Assert.Greater(wide.Distance - narrow.Distance, tall.Distance - narrow.Distance, "In portrait the width is the binding constraint");
        }

        [Test]
        public void TopDownViewReducesToPlaneGeometry()
        {
            // Looking straight down with a square viewport, a square of half-size 1 at 90 degrees fov needs distance 1.
            var placement = CameraFit.Solve(1, 1, 90, 90, 1);
            Assert.AreEqual(1, placement.Distance, 1e-9);
            Assert.AreEqual(1, placement.Height, 1e-9);
            Assert.AreEqual(0, placement.Back, 1e-9);
        }

        [Test]
        public void RejectsDegenerateInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFit.Solve(1, 1, 0, 60, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFit.Solve(1, 1, 91, 60, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFit.Solve(1, 1, 60, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFit.Solve(1, 1, 60, 60, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFit.Solve(-1, 1, 60, 60, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFit.Solve(1, 1, 60, 60, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFit.Solve(1, 1, 60, 60, 1, 1, 1.5));
        }
    }
}
