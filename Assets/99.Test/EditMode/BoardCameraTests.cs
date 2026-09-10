using NUnit.Framework;
using TrainSudoku.Core;
using TrainSudoku.Game;
using UnityEngine;

namespace TrainSudoku.Tests
{
    /// <summary>
    /// The HUD-inset half of the camera framing (work order 5.3, D9). <see cref="CameraFitTests"/> covers the fit
    /// maths itself; this covers the strip the fit is given and where that strip's centre lands.
    /// </summary>
    public class BoardCameraTests
    {
        private const float Eps = 1e-5f;

        /// <summary>The Play screen's two bars at the 1080x1920 reference resolution.</summary>
        private const float SignBar = 250f / 1920f;
        private const float LedStrip = 150f / 1920f;

        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
        }

        private BoardCamera Rig(bool orthographic = true)
        {
            _go = new GameObject("Board Camera Test", typeof(Camera), typeof(BoardCamera));
            var camera = _go.GetComponent<Camera>();
            camera.orthographic = orthographic;
            var rig = _go.GetComponent<BoardCamera>();
            rig.SetTarget(BoardLayout.HalfWidth(6), BoardLayout.HalfDepth(6), BoardLayout.Height);
            return rig;
        }

        [TestCase(0f)]
        [TestCase(0.1f)]
        [TestCase(0.13020833f)]
        [TestCase(0.4f)]
        public void OneEdgeReproducesTheOldHudFraction(float hud)
        {
            // The scalar it replaces reserved space along the top edge only: strip = 1 - hud, shift = -hud.
            var (strip, centre) = BoardCamera.HudStrip(hud, 0f);
            Assert.AreEqual(1f - hud, strip, Eps, "The free strip is what the one bar leaves");
            Assert.AreEqual(-hud, centre, Eps, "The image moves down by the whole of the one bar");
        }

        [Test]
        public void TwoBarsLeaveTheirRemainderAndCentreBetweenThem()
        {
            var (strip, centre) = BoardCamera.HudStrip(SignBar, LedStrip);
            // 1920 - 250 - 150 = 1520 px free, and its centre is 50 px below the screen's.
            Assert.AreEqual(1520f / 1920f, strip, Eps);
            Assert.AreEqual(-100f / 1920f, centre, Eps);
            Assert.AreEqual(-50f, centre * 1920f / 2f, 1e-3f, "Clip y spans 2, so the shift is 50 px down");
        }

        [Test]
        public void EqualBarsLeaveTheCentreAlone()
        {
            var (strip, centre) = BoardCamera.HudStrip(0.2f, 0.2f);
            Assert.AreEqual(0.6f, strip, Eps);
            Assert.AreEqual(0f, centre, Eps, "Symmetric bars need no shift at all");
        }

        [Test]
        public void InsetsAreClampedSoTheStripCannotVanish()
        {
            Assert.AreEqual(1f, BoardCamera.HudStrip(-1f, -1f).Strip, Eps, "A negative bar is no bar");
            Assert.AreEqual(0f, BoardCamera.HudStrip(-1f, -1f).Centre, Eps);

            var (strip, centre) = BoardCamera.HudStrip(5f, 0f);
            Assert.AreEqual(1f - BoardCamera.MaxInset, strip, Eps, "A runaway measurement is capped");
            Assert.AreEqual(-BoardCamera.MaxInset, centre, Eps);
            Assert.GreaterOrEqual(BoardCamera.HudStrip(5f, 5f).Strip, 1f - BoardCamera.MaxInsetSum, "Both bars together still leave a strip");
        }

        [Test]
        public void TheProjectionCarriesTheShiftAndTheStripSizesTheFit()
        {
            const float aspect = 9f / 16f;
            var rig = Rig();

            rig.SetHudInsets(0f, 0f);
            rig.Fit(aspect);
            var whole = rig.Camera.orthographicSize;
            Assert.AreEqual(0f, rig.Camera.projectionMatrix.m13, Eps, "No bars, no shift");

            rig.SetHudInsets(SignBar, 0f);
            rig.Fit(aspect);
            var top = rig.Camera.orthographicSize;
            Assert.AreEqual(-SignBar, rig.Camera.projectionMatrix.m13, Eps);

            rig.SetHudInsets(0f, SignBar);
            rig.Fit(aspect);
            Assert.AreEqual(SignBar, rig.Camera.projectionMatrix.m13, Eps, "The same bar at the bottom shifts the other way");
            Assert.AreEqual(top, rig.Camera.orthographicSize, Eps, "...and takes exactly as much room");

            rig.SetHudInsets(SignBar, LedStrip);
            rig.Fit(aspect);
            Assert.AreEqual(LedStrip - SignBar, rig.Camera.projectionMatrix.m13, Eps);
            Assert.GreaterOrEqual(rig.Camera.orthographicSize, top, "Two bars leave no more room than one");
            Assert.GreaterOrEqual(top, whole, "Any bar at all costs room, or the fit ignored it");
        }

        [Test]
        public void PerspectiveShiftsToo()
        {
            // A perspective projection has m33 = 0, so the same clip-space shift lands in m12 rather than m13.
            var rig = Rig(orthographic: false);
            rig.SetHudInsets(SignBar, LedStrip);
            rig.Fit(9f / 16f);
            Assert.AreEqual(-(LedStrip - SignBar), rig.Camera.projectionMatrix.m12, Eps);
        }

        [Test]
        public void TheBoardStaysInsideTheStripAtEveryShippedAspect()
        {
            // 9:16, 9:19.5 and 9:21 -- the three the work order names -- and the widest shipped board.
            foreach (var aspect in new[] { 9f / 16f, 9f / 19.5f, 9f / 21f })
            {
                var rig = Rig();
                rig.SetTarget(BoardLayout.HalfWidth(8) + 1f, BoardLayout.HalfDepth(8) + 1f, BoardLayout.Height);
                rig.SetHudInsets(SignBar, LedStrip);
                rig.Fit(aspect);

                var size = rig.Camera.orthographicSize;
                var (strip, centre) = BoardCamera.HudStrip(SignBar, LedStrip);
                var pitch = rig.PitchDegrees * Mathf.Deg2Rad;
                var halfWidth = BoardLayout.HalfWidth(8) + 1f;
                var halfDepth = BoardLayout.HalfDepth(8) + 1f;

                foreach (var y in new[] { 0.0, BoardLayout.Height })
                foreach (var x in new[] { -halfWidth, halfWidth })
                foreach (var z in new[] { -halfDepth, halfDepth })
                {
                    var (_, cy, _) = CameraFit.ToCameraSpace(x, y, z, pitch);
                    var clip = (float)(cy / size) + centre;   // normalised device y, after the shift
                    Assert.LessOrEqual(clip, 1f - 2f * SignBar + Eps, $"Corner ({x},{y},{z}) at {aspect:F3} runs under the sign bar");
                    Assert.GreaterOrEqual(clip, -1f + 2f * LedStrip - Eps, $"Corner ({x},{y},{z}) at {aspect:F3} runs under the LED strip");
                }

                Assert.Greater(strip, 0f);
                TearDown();
            }
        }
    }
}
