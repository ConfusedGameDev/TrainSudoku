using System;

namespace TrainSudoku.Core
{
    /// <summary>The six track shapes. Each key names the two sides a piece connects.</summary>
    public enum PieceKey
    {
        NS,
        EW,
        NW,
        NE,
        SW,
        SE,
    }

    public static class PieceKeys
    {
        public static readonly PieceKey[] All = { PieceKey.NS, PieceKey.EW, PieceKey.NW, PieceKey.NE, PieceKey.SW, PieceKey.SE };

        public static (Direction A, Direction B) Connections(PieceKey key)
        {
            switch (key)
            {
                case PieceKey.NS: return (Direction.North, Direction.South);
                case PieceKey.EW: return (Direction.East, Direction.West);
                case PieceKey.NW: return (Direction.North, Direction.West);
                case PieceKey.NE: return (Direction.North, Direction.East);
                case PieceKey.SW: return (Direction.South, Direction.West);
                case PieceKey.SE: return (Direction.South, Direction.East);
                default: throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown piece key.");
            }
        }

        public static bool Has(PieceKey key, Direction direction)
        {
            var (a, b) = Connections(key);
            return a == direction || b == direction;
        }

        /// <summary>Given one connection of the piece, returns the other one.</summary>
        public static Direction Other(PieceKey key, Direction direction)
        {
            var (a, b) = Connections(key);
            if (a == direction) return b;
            if (b == direction) return a;
            throw new ArgumentException($"{key} has no connection towards {direction}.", nameof(direction));
        }

        public static bool TryFromDirections(Direction a, Direction b, out PieceKey key)
        {
            foreach (var candidate in All)
            {
                var (ca, cb) = Connections(candidate);
                if ((ca == a && cb == b) || (ca == b && cb == a))
                {
                    key = candidate;
                    return true;
                }
            }

            key = default;
            return false;
        }

        public static bool TryParse(string text, out PieceKey key)
        {
            if (text != null)
            {
                var trimmed = text.Trim();
                foreach (var candidate in All)
                {
                    if (string.Equals(candidate.ToString(), trimmed, StringComparison.OrdinalIgnoreCase))
                    {
                        key = candidate;
                        return true;
                    }
                }
            }

            key = default;
            return false;
        }
    }
}
