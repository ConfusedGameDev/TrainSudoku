using System;
using System.Collections.Generic;

namespace TrainSudoku.XR.Rules
{
    /// <summary>
    /// Where a map lands on the platform: the scale from map units to cells, the map-space point that sits in the
    /// middle of the fitted area, and the platform point that middle lands on.
    /// </summary>
    /// <remarks>
    /// Map space is the phone's: x grows right and <b>y grows down the screen</b>. On the platform the top of the
    /// screen is the far side of the table, so y is flipped into -Z: what the phone draws at the top, the platform
    /// prints furthest from the player.
    /// </remarks>
    public readonly struct MapFrame
    {
        public double Scale { get; }
        public double CentreX { get; }
        public double CentreY { get; }
        public double OriginX { get; }
        public double OriginZ { get; }

        public MapFrame(double scale, double centreX, double centreY, double originX, double originZ)
        {
            Scale = scale;
            CentreX = centreX;
            CentreY = centreY;
            OriginX = originX;
            OriginZ = originZ;
        }

        /// <summary>A map-space point on the platform, in cells.</summary>
        public (double X, double Z) ToPlatform(double x, double y) =>
            (OriginX + (x - CentreX) * Scale, OriginZ - (y - CentreY) * Scale);
    }

    /// <summary>Fits map-space points onto a rectangle of the platform, preserving their aspect.</summary>
    public static class MapFit
    {
        private const double Epsilon = 1e-6;

        /// <summary>
        /// Fits <paramref name="points"/> into a <paramref name="width"/> by <paramref name="depth"/> rectangle centred
        /// on (<paramref name="originX"/>, <paramref name="originZ"/>), keeping <paramref name="marginX"/> and
        /// <paramref name="marginZ"/> clear on each side for whatever hangs off the nodes (roundels, names).
        /// </summary>
        /// <param name="maxScale">
        /// The most cells one map unit may take. It keeps a small map from being blown up to fill the card, and is the
        /// scale used when the points have no extent at all.
        /// </param>
        /// <returns>False when there are no points or no room left inside the margins.</returns>
        public static bool TryFit(IEnumerable<(double X, double Y)> points, double width, double depth,
            double marginX, double marginZ, double originX, double originZ, double maxScale, out MapFrame frame)
        {
            frame = default;
            if (points == null) return false;

            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            var any = false;
            foreach (var (x, y) in points)
            {
                any = true;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }

            var roomX = width - 2 * marginX;
            var roomZ = depth - 2 * marginZ;
            if (!any || roomX <= 0 || roomZ <= 0) return false;

            var scale = maxScale;
            if (maxX - minX > Epsilon) scale = Math.Min(scale, roomX / (maxX - minX));
            if (maxY - minY > Epsilon) scale = Math.Min(scale, roomZ / (maxY - minY));

            frame = new MapFrame(scale, (minX + maxX) / 2, (minY + maxY) / 2, originX, originZ);
            return true;
        }
    }
}
