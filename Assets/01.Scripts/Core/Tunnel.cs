using System;

namespace TrainSudoku.Core
{
    /// <summary>
    /// A tunnel mouth outside the grid. <see cref="Side"/> is the board edge it sits on and <see cref="Index"/> is the
    /// column (North/South) or row (East/West). The tunnel points into its adjacent perimeter cell from that side.
    /// </summary>
    public readonly struct Tunnel : IEquatable<Tunnel>
    {
        public Direction Side { get; }
        public int Index { get; }

        public Tunnel(Direction side, int index)
        {
            Side = side;
            Index = index;
        }

        /// <summary>Column of the cell this tunnel opens into.</summary>
        public int CellX(int width)
        {
            switch (Side)
            {
                case Direction.East: return width - 1;
                case Direction.West: return 0;
                default: return Index;
            }
        }

        /// <summary>Row of the cell this tunnel opens into.</summary>
        public int CellY(int height)
        {
            switch (Side)
            {
                case Direction.South: return height - 1;
                case Direction.North: return 0;
                default: return Index;
            }
        }

        public bool IsOnPerimeter(int width, int height)
        {
            var limit = Side.IsVertical() ? width : height;
            return Index >= 0 && Index < limit;
        }

        public bool Equals(Tunnel other) => Side == other.Side && Index == other.Index;
        public override bool Equals(object obj) => obj is Tunnel other && Equals(other);
        public override int GetHashCode() => ((int)Side * 397) ^ Index;
        public override string ToString() => $"{Side}[{Index}]";

        public static bool operator ==(Tunnel left, Tunnel right) => left.Equals(right);
        public static bool operator !=(Tunnel left, Tunnel right) => !left.Equals(right);
    }
}
