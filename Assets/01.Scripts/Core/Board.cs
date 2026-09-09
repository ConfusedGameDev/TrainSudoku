using System;

namespace TrainSudoku.Core
{
    /// <summary>Mutable play state for one level: the fixed pieces plus whatever the player has placed.</summary>
    public sealed class Board
    {
        private readonly Piece?[] _cells;

        public LevelData Level { get; }
        public int Width => Level.Width;
        public int Height => Level.Height;

        public Board(LevelData level)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            _cells = new Piece?[Width * Height];
            foreach (var fixedPiece in level.FixedPieces)
            {
                if (!InBounds(fixedPiece.X, fixedPiece.Y))
                    throw new ArgumentException($"Fixed piece {fixedPiece} is outside the {Width}x{Height} board.", nameof(level));
                _cells[Index(fixedPiece.X, fixedPiece.Y)] = new Piece(fixedPiece.Key, true);
            }
        }

        private Board(Board other)
        {
            Level = other.Level;
            _cells = (Piece?[])other._cells.Clone();
        }

        public Board Clone() => new Board(this);

        /// <summary>The piece in a cell, or null when the cell is empty or outside the board.</summary>
        public Piece? this[int x, int y] => InBounds(x, y) ? _cells[Index(x, y)] : null;

        public bool InBounds(int x, int y) => Level.InBounds(x, y);
        public bool IsEmpty(int x, int y) => InBounds(x, y) && !_cells[Index(x, y)].HasValue;

        public bool HasTunnel(int x, int y, Direction side) => Level.HasTunnel(x, y, side);
        public bool IsEntrance(int x, int y, Direction side) => Level.IsEntrance(x, y, side);
        public bool IsExit(int x, int y, Direction side) => Level.IsExit(x, y, side);

        /// <summary>Places a player piece if the cell is empty and the key is legal there (PRD 3.3).</summary>
        public bool TryPlace(int x, int y, PieceKey key)
        {
            if (!Legality.IsLegal(this, x, y, key)) return false;
            _cells[Index(x, y)] = new Piece(key, false);
            return true;
        }

        /// <summary>Removes a player piece. Fixed pieces and empty cells are left alone and return false.</summary>
        public bool TryErase(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            var index = Index(x, y);
            var piece = _cells[index];
            if (!piece.HasValue || piece.Value.IsFixed) return false;
            _cells[index] = null;
            return true;
        }

        /// <summary>Clears every player piece; fixed pieces stay.</summary>
        public void Reset()
        {
            for (var i = 0; i < _cells.Length; i++)
                if (_cells[i].HasValue && !_cells[i].Value.IsFixed)
                    _cells[i] = null;
        }

        /// <summary>Writes a cell without any legality check. For the solver and tests.</summary>
        public void SetUnchecked(int x, int y, Piece? piece)
        {
            if (!InBounds(x, y)) throw new ArgumentOutOfRangeException($"({x},{y}) is outside the {Width}x{Height} board.");
            _cells[Index(x, y)] = piece;
        }

        public int RowCount(int y)
        {
            var count = 0;
            for (var x = 0; x < Width; x++)
                if (_cells[Index(x, y)].HasValue) count++;
            return count;
        }

        public int ColumnCount(int x)
        {
            var count = 0;
            for (var y = 0; y < Height; y++)
                if (_cells[Index(x, y)].HasValue) count++;
            return count;
        }

        public int PieceCount
        {
            get
            {
                var count = 0;
                foreach (var cell in _cells)
                    if (cell.HasValue) count++;
                return count;
            }
        }

        private int Index(int x, int y) => y * Width + x;
    }
}
