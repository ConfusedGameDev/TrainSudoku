using System;

namespace TrainSudoku.Core
{
    /// <summary>Where to put a pitched camera so that a ground rectangle centred on the origin fills the view.</summary>
    public readonly struct CameraPlacement
    {
        /// <summary>Distance from the rectangle centre along the view direction.</summary>
        public double Distance { get; }

        /// <summary>Camera height above the ground plane.</summary>
        public double Height { get; }

        /// <summary>How far the camera sits south (negative Z) of the rectangle centre.</summary>
        public double Back { get; }

        public CameraPlacement(double distance, double height, double back)
        {
            Distance = distance;
            Height = height;
            Back = back;
        }
    }

    /// <summary>
    /// Fit-to-board camera maths (PRD section 7). The camera looks north (+Z) and down at <c>pitch</c> degrees below the
    /// horizontal, aimed at the origin. Moving it back along its view direction only adds to the depth of every point, so
    /// the smallest distance that keeps all four corners inside the frustum has a closed form.
    /// </summary>
    public static class CameraFit
    {
        /// <param name="halfWidth">Half the rectangle's extent along X.</param>
        /// <param name="halfDepth">Half the rectangle's extent along Z.</param>
        /// <param name="pitchDegrees">Camera tilt below the horizontal, 0 exclusive to 90 inclusive.</param>
        /// <param name="verticalFovDegrees">Camera vertical field of view.</param>
        /// <param name="aspect">Viewport width divided by height.</param>
        /// <param name="horizontalFraction">Share of the viewport width the rectangle may use, 0 exclusive to 1.</param>
        /// <param name="verticalFraction">Share of the viewport height the rectangle may use, 0 exclusive to 1.</param>
        public static CameraPlacement Solve(
            double halfWidth, double halfDepth, double pitchDegrees, double verticalFovDegrees, double aspect,
            double horizontalFraction = 1.0, double verticalFraction = 1.0)
        {
            if (halfWidth < 0) throw new ArgumentOutOfRangeException(nameof(halfWidth));
            if (halfDepth < 0) throw new ArgumentOutOfRangeException(nameof(halfDepth));
            if (pitchDegrees <= 0 || pitchDegrees > 90) throw new ArgumentOutOfRangeException(nameof(pitchDegrees), pitchDegrees, "Pitch must be in (0, 90].");
            if (verticalFovDegrees <= 0 || verticalFovDegrees >= 180) throw new ArgumentOutOfRangeException(nameof(verticalFovDegrees));
            if (aspect <= 0) throw new ArgumentOutOfRangeException(nameof(aspect));
            if (horizontalFraction <= 0 || horizontalFraction > 1) throw new ArgumentOutOfRangeException(nameof(horizontalFraction));
            if (verticalFraction <= 0 || verticalFraction > 1) throw new ArgumentOutOfRangeException(nameof(verticalFraction));

            var pitch = pitchDegrees * Math.PI / 180.0;
            var tanV = Math.Tan(verticalFovDegrees * Math.PI / 360.0) * verticalFraction;
            var tanH = Math.Tan(verticalFovDegrees * Math.PI / 360.0) * aspect * horizontalFraction;

            var distance = 0.0;
            foreach (var (x, z) in Corners(halfWidth, halfDepth))
            {
                var (cx, cy, cz) = ToCameraSpace(x, z, pitch);
                distance = Math.Max(distance, Math.Abs(cx) / tanH - cz);
                distance = Math.Max(distance, Math.Abs(cy) / tanV - cz);
            }

            return new CameraPlacement(distance, distance * Math.Sin(pitch), distance * Math.Cos(pitch));
        }

        /// <summary>Camera-space coordinates of a ground point relative to a camera at the origin with the given pitch, looking north.</summary>
        public static (double X, double Y, double Z) ToCameraSpace(double x, double z, double pitchRadians)
        {
            // right = (1, 0, 0); up = (0, cos p, sin p); forward = (0, -sin p, cos p). Ground points have y = 0.
            return (x, z * Math.Sin(pitchRadians), z * Math.Cos(pitchRadians));
        }

        private static (double X, double Z)[] Corners(double halfWidth, double halfDepth) => new[]
        {
            (-halfWidth, -halfDepth), (halfWidth, -halfDepth), (-halfWidth, halfDepth), (halfWidth, halfDepth),
        };
    }
}
