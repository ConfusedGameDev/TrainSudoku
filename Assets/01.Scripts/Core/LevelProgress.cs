using System;
using System.Collections.Generic;

namespace TrainSudoku.Core
{
    /// <summary>A piece the player placed, as stored in the save file. Fixed pieces are never recorded.</summary>
    public readonly struct PlacedPiece : IEquatable<PlacedPiece>
    {
        public int X { get; }
        public int Y { get; }
        public PieceKey Key { get; }

        public PlacedPiece(int x, int y, PieceKey key)
        {
            X = x;
            Y = y;
            Key = key;
        }

        public bool Equals(PlacedPiece other) => X == other.X && Y == other.Y && Key == other.Key;
        public override bool Equals(object obj) => obj is PlacedPiece other && Equals(other);
        public override int GetHashCode() => (X * 397 ^ Y) * 7 + (int)Key;
        public override string ToString() => $"{Key} at ({X},{Y})";
    }

    /// <summary>
    /// A snapshot of a level mid-play (PRD section 6): the player's pieces and the clock. Saved whenever the board
    /// changes or the player leaves the level, and applied back when the level is selected again.
    /// </summary>
    public sealed class LevelProgress
    {
        private static readonly PlacedPiece[] NoPieces = new PlacedPiece[0];

        public double Elapsed { get; }
        public IReadOnlyList<PlacedPiece> Pieces { get; }

        public LevelProgress(double elapsed, IReadOnlyList<PlacedPiece> pieces)
        {
            if (elapsed < 0 || double.IsNaN(elapsed) || double.IsInfinity(elapsed))
                throw new ArgumentOutOfRangeException(nameof(elapsed), elapsed, "Elapsed time must be a finite, non-negative number.");
            Elapsed = elapsed;
            Pieces = pieces ?? NoPieces;
        }

        /// <summary>Records every player piece on the board in raster order.</summary>
        public static LevelProgress Capture(Board board, double elapsed)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            var pieces = new List<PlacedPiece>();
            for (var y = 0; y < board.Height; y++)
            for (var x = 0; x < board.Width; x++)
                if (board[x, y] is Piece piece && !piece.IsFixed) pieces.Add(new PlacedPiece(x, y, piece.Key));
            return new LevelProgress(elapsed, pieces);
        }

        /// <summary>
        /// Puts the pieces back without legality checks, because replaying them one by one could refuse an order the
        /// player was allowed. Cells outside the board or already occupied (a fixed piece the level gained since the
        /// save) are skipped. Returns how many pieces were placed.
        /// </summary>
        public int ApplyTo(Board board)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            var applied = 0;
            foreach (var piece in Pieces)
            {
                if (!board.IsEmpty(piece.X, piece.Y)) continue;
                board.SetUnchecked(piece.X, piece.Y, new Piece(piece.Key, false));
                applied++;
            }

            return applied;
        }
    }
}
