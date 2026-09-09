using System;

namespace TrainSudoku.Core
{
    /// <summary>A track piece sitting in a cell. Fixed pieces come from the level and cannot be erased.</summary>
    public readonly struct Piece : IEquatable<Piece>
    {
        public PieceKey Key { get; }
        public bool IsFixed { get; }

        public Piece(PieceKey key, bool isFixed)
        {
            Key = key;
            IsFixed = isFixed;
        }

        public bool Has(Direction direction) => PieceKeys.Has(Key, direction);

        public bool Equals(Piece other) => Key == other.Key && IsFixed == other.IsFixed;
        public override bool Equals(object obj) => obj is Piece other && Equals(other);
        public override int GetHashCode() => ((int)Key << 1) | (IsFixed ? 1 : 0);
        public override string ToString() => IsFixed ? $"{Key} (fixed)" : Key.ToString();
    }
}
