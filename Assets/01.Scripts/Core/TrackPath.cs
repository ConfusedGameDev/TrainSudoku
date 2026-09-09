using System;
using System.Collections.Generic;

namespace TrainSudoku.Core
{
    /// <summary>A point on a <see cref="TrackPath"/> in world coordinates on the ground plane, with the direction of travel.</summary>
    public readonly struct PathSample
    {
        public double X { get; }
        public double Z { get; }
        public double TangentX { get; }
        public double TangentZ { get; }

        public PathSample(double x, double z, double tangentX, double tangentZ)
        {
            X = x;
            Z = z;
            TangentX = tangentX;
            TangentZ = tangentZ;
        }
    }

    /// <summary>
    /// The train's route (PRD section 8): the per-cell <see cref="TrackCurve"/>s from the entrance to the exit joined
    /// end to end, extended one cell into each tunnel so the train can start and finish out of sight. Parameterised
    /// by arc length in world units; sampling beyond either end continues straight along the end tangent.
    /// </summary>
    public sealed class TrackPath
    {
        public const double TunnelExtension = BoardLayout.CellSize;

        private readonly List<Segment> _segments = new List<Segment>();
        private readonly List<double> _starts = new List<double>();
        private readonly List<(int X, int Y)> _cells = new List<(int X, int Y)>();

        private TrackPath()
        {
        }

        public double Length { get; private set; }

        /// <summary>Board cells in travel order.</summary>
        public IReadOnlyList<(int X, int Y)> Cells => _cells;

        /// <summary>Builds the route of a board whose track connects the entrance to the exit. False when it does not.</summary>
        public static bool TryBuild(Board board, out TrackPath path)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            path = null;
            if (!PathFinder.TryFindPath(board, out var cells)) return false;

            var level = board.Level;
            var result = new TrackPath();

            var entrance = level.Entrance;
            var (ex, ez) = BoardLayout.CellCenter(entrance.CellX(level.Width), entrance.CellY(level.Height), level.Width, level.Height);
            var (inDx, inDz) = BoardLayout.Step(entrance.Side);
            result.Add(Segment.Straight(
                ex + inDx * (TrackCurve.Radius + TunnelExtension), ez + inDz * (TrackCurve.Radius + TunnelExtension),
                -inDx, -inDz, TunnelExtension));

            var from = entrance.Side;
            foreach (var (x, y) in cells)
            {
                var key = board[x, y].Value.Key;
                var curve = TrackCurve.For(key, from);
                var (cx, cz) = BoardLayout.CellCenter(x, y, level.Width, level.Height);
                result.Add(Segment.Cell(cx, cz, curve));
                result._cells.Add((x, y));
                from = curve.To.Opposite();
            }

            var exitSide = from.Opposite();
            var (lx, ly) = cells[cells.Count - 1];
            var (lcx, lcz) = BoardLayout.CellCenter(lx, ly, level.Width, level.Height);
            var (outDx, outDz) = BoardLayout.Step(exitSide);
            result.Add(Segment.Straight(lcx + outDx * TrackCurve.Radius, lcz + outDz * TrackCurve.Radius, outDx, outDz, TunnelExtension));

            path = result;
            return true;
        }

        private void Add(Segment segment)
        {
            _starts.Add(Length);
            _segments.Add(segment);
            Length += segment.Length;
        }

        public PathSample Sample(double distance)
        {
            if (distance <= 0)
            {
                var first = _segments[0].At(0);
                return new PathSample(first.X + first.TangentX * distance, first.Z + first.TangentZ * distance, first.TangentX, first.TangentZ);
            }

            if (distance >= Length)
            {
                var last = _segments[_segments.Count - 1].At(1);
                var over = distance - Length;
                return new PathSample(last.X + last.TangentX * over, last.Z + last.TangentZ * over, last.TangentX, last.TangentZ);
            }

            // Binary search for the segment containing the distance.
            var lo = 0;
            var hi = _segments.Count - 1;
            while (lo < hi)
            {
                var mid = (lo + hi + 1) / 2;
                if (_starts[mid] <= distance) lo = mid;
                else hi = mid - 1;
            }

            var segment = _segments[lo];
            return segment.At((distance - _starts[lo]) / segment.Length);
        }

        private readonly struct Segment
        {
            private readonly double _originX;
            private readonly double _originZ;
            private readonly bool _isCurve;
            private readonly TrackCurve _curve;
            private readonly double _dirX;
            private readonly double _dirZ;

            public double Length { get; }

            private Segment(double originX, double originZ, bool isCurve, TrackCurve curve, double dirX, double dirZ, double length)
            {
                _originX = originX;
                _originZ = originZ;
                _isCurve = isCurve;
                _curve = curve;
                _dirX = dirX;
                _dirZ = dirZ;
                Length = length;
            }

            public static Segment Cell(double centerX, double centerZ, TrackCurve curve) =>
                new Segment(centerX, centerZ, true, curve, 0, 0, curve.Length);

            public static Segment Straight(double startX, double startZ, double dirX, double dirZ, double length) =>
                new Segment(startX, startZ, false, default, dirX, dirZ, length);

            public PathSample At(double t)
            {
                if (_isCurve)
                {
                    var (px, pz) = _curve.Position(t);
                    var (tx, tz) = _curve.Tangent(t);
                    return new PathSample(_originX + px, _originZ + pz, tx, tz);
                }

                return new PathSample(_originX + _dirX * Length * t, _originZ + _dirZ * Length * t, _dirX, _dirZ);
            }
        }
    }
}
