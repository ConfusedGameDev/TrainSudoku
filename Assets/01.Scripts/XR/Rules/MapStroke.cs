using System;
using System.Collections.Generic;

namespace TrainSudoku.XR.Rules
{
    /// <summary>How a line's map is stroked. Mirrors the phone's <c>MapShape</c>, which Rules cannot see.</summary>
    public enum StrokeShape
    {
        /// <summary>An open run from one terminus to the other.</summary>
        Route,

        /// <summary>A closed loop through the nodes; the last joins back to the first.</summary>
        Loop,

        /// <summary>
        /// A stadium fitted to the nodes' bounding box: its corners rounded by half the shorter side, so the two short
        /// ends are semicircles. The nodes only place the stations on it.
        /// </summary>
        Stadium,
    }

    /// <summary>The polyline a line's colour is laid along on the platform, in map space.</summary>
    public static class MapStroke
    {
        private const double Epsilon = 1e-6;

        /// <param name="arcSegments">Straight segments per quarter circle of a stadium's rounded corners.</param>
        public static List<(double X, double Y)> Points(StrokeShape shape, IReadOnlyList<(double X, double Y)> nodes, int arcSegments = 8)
        {
            var points = new List<(double X, double Y)>();
            if (nodes == null || nodes.Count == 0) return points;

            if (shape == StrokeShape.Stadium && TryStadium(nodes, Math.Max(1, arcSegments), points)) return points;

            // A stadium with no area is stroked open through its nodes, as the phone's network map strokes it.
            points.AddRange(nodes);
            if (shape == StrokeShape.Loop && nodes.Count > 2) points.Add(nodes[0]);
            return points;
        }

        /// <summary>
        /// The phone's stadium: from the middle of the top edge, clockwise on screen (right, then down), each corner of
        /// the box rounded with a quarter circle of half the shorter side. Closed: it ends where it began. A box with no
        /// area has no stadium, and the caller strokes the nodes instead.
        /// </summary>
        private static bool TryStadium(IReadOnlyList<(double X, double Y)> nodes, int arcSegments, List<(double X, double Y)> points)
        {
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var (x, y) in nodes)
            {
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }

            var radius = Math.Min(maxX - minX, maxY - minY) / 2;
            if (radius <= Epsilon) return false;

            // Corner centres in drawing order, and the angle each arc starts at. Y is down, so +90 degrees points down the screen.
            var corners = new[]
            {
                (X: maxX - radius, Y: minY + radius, From: -90.0),
                (X: maxX - radius, Y: maxY - radius, From: 0.0),
                (X: minX + radius, Y: maxY - radius, From: 90.0),
                (X: minX + radius, Y: minY + radius, From: 180.0),
            };

            Add(points, ((minX + maxX) / 2, minY));
            foreach (var corner in corners)
                for (var i = 0; i <= arcSegments; i++)
                {
                    var angle = (corner.From + 90.0 * i / arcSegments) * Math.PI / 180.0;
                    Add(points, (corner.X + radius * Math.Cos(angle), corner.Y + radius * Math.Sin(angle)));
                }

            // On a box as wide as its radius allows, the last arc already ends at the start, and the stroke is closed.
            Add(points, points[0]);
            return true;
        }

        /// <summary>Skips a point on top of the last one: where the radius is half the width, the straights have no length.</summary>
        private static void Add(List<(double X, double Y)> points, (double X, double Y) point)
        {
            if (points.Count > 0)
            {
                var last = points[points.Count - 1];
                if (Math.Abs(last.X - point.X) < Epsilon && Math.Abs(last.Y - point.Y) < Epsilon) return;
            }

            points.Add(point);
        }
    }
}
