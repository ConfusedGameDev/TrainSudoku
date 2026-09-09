using System;

namespace TrainSudoku.Core
{
    /// <summary>
    /// The centre line of a track piece inside its cell (PRD section 7): a straight through the centre, or a quarter
    /// circle of radius 0.5 centred on the cell corner shared by the two sides. Coordinates are cell-local with the
    /// origin at the cell centre, X east and Z north, so neighbouring pieces meet at side midpoints with matching
    /// tangents. <c>t</c> runs from 0 at the <see cref="From"/> side to 1 at the <see cref="To"/> side.
    /// </summary>
    public readonly struct TrackCurve
    {
        public const double Radius = BoardLayout.CellSize / 2.0;

        public PieceKey Key { get; }
        public Direction From { get; }
        public Direction To { get; }

        private TrackCurve(PieceKey key, Direction from, Direction to)
        {
            Key = key;
            From = from;
            To = to;
        }

        /// <summary>Curve of a piece entered from <paramref name="from"/>.</summary>
        public static TrackCurve For(PieceKey key, Direction from)
        {
            if (!PieceKeys.Has(key, from)) throw new ArgumentException($"{key} has no connection towards {from}.", nameof(from));
            return new TrackCurve(key, from, PieceKeys.Other(key, from));
        }

        /// <summary>Curve of a piece in its canonical orientation, entered from the first connection of the key.</summary>
        public static TrackCurve For(PieceKey key) => For(key, PieceKeys.Connections(key).A);

        public TrackCurve Reversed() => new TrackCurve(Key, To, From);

        public bool IsStraight => From == To.Opposite();

        public double Length => IsStraight ? BoardLayout.CellSize : Math.PI * Radius / 2.0;

        /// <summary>Midpoint of a cell side, where pieces connect.</summary>
        public static (double X, double Z) SideMidpoint(Direction side)
        {
            var (dx, dz) = BoardLayout.Step(side);
            return (dx * Radius, dz * Radius);
        }

        public (double X, double Z) Position(double t)
        {
            var (fx, fz) = SideMidpoint(From);
            var (tx, tz) = SideMidpoint(To);
            if (IsStraight) return (fx + (tx - fx) * t, fz + (tz - fz) * t);

            var (cx, cz, phi0, delta) = Arc();
            var phi = phi0 + delta * t;
            return (cx + Radius * Math.Cos(phi), cz + Radius * Math.Sin(phi));
        }

        /// <summary>Unit direction of travel at <paramref name="t"/>.</summary>
        public (double X, double Z) Tangent(double t)
        {
            if (IsStraight)
            {
                var (dx, dz) = BoardLayout.Step(To);
                return (dx, dz);
            }

            var (_, _, phi0, delta) = Arc();
            var phi = phi0 + delta * t;
            var sign = Math.Sign(delta);
            return (-sign * Math.Sin(phi), sign * Math.Cos(phi));
        }

        /// <summary>Arc centre (the shared corner), start angle and signed sweep of a curved piece.</summary>
        private (double CX, double CZ, double Phi0, double Delta) Arc()
        {
            var (fx, fz) = SideMidpoint(From);
            var (tx, tz) = SideMidpoint(To);
            var cx = fx + tx;
            var cz = fz + tz;
            var phi0 = Math.Atan2(fz - cz, fx - cx);
            var phi1 = Math.Atan2(tz - cz, tx - cx);
            var delta = phi1 - phi0;
            while (delta > Math.PI) delta -= 2 * Math.PI;
            while (delta <= -Math.PI) delta += 2 * Math.PI;
            return (cx, cz, phi0, delta);
        }

        public override string ToString() => $"{Key} {From}->{To}";
    }
}
