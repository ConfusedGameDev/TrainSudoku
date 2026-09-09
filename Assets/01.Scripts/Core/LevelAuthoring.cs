using System;
using System.Text;

namespace TrainSudoku.Core
{
    /// <summary>Editing operations on <see cref="LevelData"/> used by the level editor (PRD section 10).</summary>
    public static class LevelAuthoring
    {
        /// <summary>
        /// Returns a copy with the new size. Clues and fixed pieces inside the new bounds are kept; tunnel indices
        /// are clamped so both tunnels stay on the perimeter.
        /// </summary>
        public static LevelData Resize(LevelData level, int width, int height)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));

            var copy = new LevelData(width, height) { Name = level.Name };
            Array.Copy(level.ColumnClues, copy.ColumnClues, Math.Min(level.Width, width));
            Array.Copy(level.RowClues, copy.RowClues, Math.Min(level.Height, height));
            foreach (var piece in level.FixedPieces)
                if (copy.InBounds(piece.X, piece.Y)) copy.FixedPieces.Add(piece);
            copy.Entrance = Clamp(level.Entrance, width, height);
            copy.Exit = Clamp(level.Exit, width, height);
            return copy;
        }

        private static Tunnel Clamp(Tunnel tunnel, int width, int height)
        {
            var limit = tunnel.Side.IsVertical() ? width : height;
            var index = Math.Max(0, Math.Min(limit - 1, tunnel.Index));
            return new Tunnel(tunnel.Side, index);
        }

        /// <summary>Sets the clue of every row and column to the number of fixed pieces it holds ("author by solution").</summary>
        public static void DeriveClues(LevelData level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));

            Array.Clear(level.ColumnClues, 0, level.Width);
            Array.Clear(level.RowClues, 0, level.Height);
            foreach (var piece in level.FixedPieces)
            {
                if (!level.InBounds(piece.X, piece.Y)) continue;
                level.ColumnClues[piece.X]++;
                level.RowClues[piece.Y]++;
            }
        }

        /// <summary>Places, replaces or (with a null key) removes the fixed piece in a cell.</summary>
        public static void SetFixedPiece(LevelData level, int x, int y, PieceKey? key)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (!level.InBounds(x, y))
                throw new ArgumentOutOfRangeException($"({x},{y}) is outside the {level.Width}x{level.Height} board.");

            level.FixedPieces.RemoveAll(p => p.X == x && p.Y == y);
            if (key.HasValue) level.FixedPieces.Add(new FixedPiece(x, y, key.Value));
        }

        /// <summary>Replaces the fixed pieces with every piece on a board, fixed or not.</summary>
        public static void SetFixedPiecesFrom(LevelData level, Board board)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (board == null) throw new ArgumentNullException(nameof(board));

            level.FixedPieces.Clear();
            for (var y = 0; y < level.Height; y++)
            for (var x = 0; x < level.Width; x++)
                if (board[x, y] is Piece piece) level.FixedPieces.Add(new FixedPiece(x, y, piece.Key));
        }

        /// <summary>Lower-case, hyphen-separated identifier derived from a display name. Empty when the name has no letters or digits.</summary>
        public static string SuggestId(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            var sb = new StringBuilder(name.Length);
            var pendingHyphen = false;
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (pendingHyphen && sb.Length > 0) sb.Append('-');
                    pendingHyphen = false;
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    pendingHyphen = true;
                }
            }

            return sb.ToString();
        }
    }
}
