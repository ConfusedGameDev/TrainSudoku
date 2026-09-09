using System;

namespace TrainSudoku.Core
{
    /// <summary>Where to put a pitched camera so that a box centred on the origin fills the view.</summary>
    public readonly struct CameraPlacement
    {
        /// <summary>Distance from the box centre along the view direction.</summary>
        public double Distance { get; }

        /// <summary>Camera height above the ground plane.</summary>
        public double Height { get; }

        /// <summary>How far the camera sits south (negative Z) of the box centre.</summary>
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
    /// horizontal, aimed at the origin. The thing to frame is an axis-aligned box on the ground plane: a rectangle of
    /// the given half extents plus <c>height</c> units above it, so tunnels, labels and the train stay in view.
    /// For a perspective camera, moving back along the view direction only adds to the depth of every point, so the
    /// smallest distance that keeps all eight corners inside the frustum has a closed form. For an orthographic camera
    /// the distance is irrelevant and the half-height of the view volume is what has to grow.
    /// </summary>
    public static class CameraFit
    {
        /// <param name="halfWidth">Half the box's extent along X.</param>
        /// <param name="halfDepth">Half the box's extent along Z.</param>
        /// <param name="height">The box's extent above the ground plane.</param>
        /// <param name="pitchDegrees">Camera tilt below the horizontal, 0 exclusive to 90 inclusive.</param>
        /// <param name="verticalFovDegrees">Camera vertical field of view.</param>
        /// <param name="aspect">Viewport width divided by height.</param>
        /// <param name="horizontalFraction">Share of the viewport width the box may use, 0 exclusive to 1.</param>
        /// <param name="verticalFraction">Share of the viewport height the box may use, 0 exclusive to 1.</param>
        public static CameraPlacement Solve(
            double halfWidth, double halfDepth, double height, double pitchDegrees, double verticalFovDegrees, double aspect,
            double horizontalFraction = 1.0, double verticalFraction = 1.0)
        {
            Validate(halfWidth, halfDepth, height, pitchDegrees, aspect, horizontalFraction, verticalFraction);
            if (verticalFovDegrees <= 0 || verticalFovDegrees >= 180) throw new ArgumentOutOfRangeException(nameof(verticalFovDegrees));

            var pitch = pitchDegrees * Math.PI / 180.0;
            var tanV = Math.Tan(verticalFovDegrees * Math.PI / 360.0) * verticalFraction;
            var tanH = Math.Tan(verticalFovDegrees * Math.PI / 360.0) * aspect * horizontalFraction;

            var distance = 0.0;
            foreach (var (x, y, z) in Corners(halfWidth, halfDepth, height))
            {
                var (cx, cy, cz) = ToCameraSpace(x, y, z, pitch);
                distance = Math.Max(distance, Math.Abs(cx) / tanH - cz);
                distance = Math.Max(distance, Math.Abs(cy) / tanV - cz);
            }

            return new CameraPlacement(distance, distance * Math.Sin(pitch), distance * Math.Cos(pitch));
        }

        /// <summary>
        /// Orthographic fit: the smallest half-height of the view volume (Unity's orthographic size) that shows the
        /// whole box within the allowed viewport shares. Parameters as for <see cref="Solve"/>.
        /// </summary>
        public static double SolveOrthographicSize(
            double halfWidth, double halfDepth, double height, double pitchDegrees, double aspect,
            double horizontalFraction = 1.0, double verticalFraction = 1.0)
        {
            Validate(halfWidth, halfDepth, height, pitchDegrees, aspect, horizontalFraction, verticalFraction);

            var pitch = pitchDegrees * Math.PI / 180.0;
            var size = 0.0;
            foreach (var (x, y, z) in Corners(halfWidth, halfDepth, height))
            {
                var (cx, cy, _) = ToCameraSpace(x, y, z, pitch);
                size = Math.Max(size, Math.Abs(cy) / verticalFraction);
                size = Math.Max(size, Math.Abs(cx) / (aspect * horizontalFraction));
            }

            return size;
        }

        /// <summary>Camera-space coordinates of a ground point relative to a camera at the origin with the given pitch, looking north.</summary>
        public static (double X, double Y, double Z) ToCameraSpace(double x, double z, double pitchRadians) => ToCameraSpace(x, 0, z, pitchRadians);

        /// <summary>Camera-space coordinates of any point relative to a camera at the origin with the given pitch, looking north.</summary>
        public static (double X, double Y, double Z) ToCameraSpace(double x, double y, double z, double pitchRadians)
        {
            // right = (1, 0, 0); up = (0, cos p, sin p); forward = (0, -sin p, cos p).
            var sin = Math.Sin(pitchRadians);
            var cos = Math.Cos(pitchRadians);
            return (x, y * cos + z * sin, z * cos - y * sin);
        }

        private static void Validate(double halfWidth, double halfDepth, double height, double pitchDegrees, double aspect, double horizontalFraction, double verticalFraction)
        {
            if (halfWidth < 0) throw new ArgumentOutOfRangeException(nameof(halfWidth));
            if (halfDepth < 0) throw new ArgumentOutOfRangeException(nameof(halfDepth));
            if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (pitchDegrees <= 0 || pitchDegrees > 90) throw new ArgumentOutOfRangeException(nameof(pitchDegrees), pitchDegrees, "Pitch must be in (0, 90].");
            if (aspect <= 0) throw new ArgumentOutOfRangeException(nameof(aspect));
            if (horizontalFraction <= 0 || horizontalFraction > 1) throw new ArgumentOutOfRangeException(nameof(horizontalFraction));
            if (verticalFraction <= 0 || verticalFraction > 1) throw new ArgumentOutOfRangeException(nameof(verticalFraction));
        }

        private static (double X, double Y, double Z)[] Corners(double halfWidth, double halfDepth, double height) => new[]
        {
            (-halfWidth, 0.0, -halfDepth), (halfWidth, 0.0, -halfDepth), (-halfWidth, 0.0, halfDepth), (halfWidth, 0.0, halfDepth),
            (-halfWidth, height, -halfDepth), (halfWidth, height, -halfDepth), (-halfWidth, height, halfDepth), (halfWidth, height, halfDepth),
        };
    }
}
