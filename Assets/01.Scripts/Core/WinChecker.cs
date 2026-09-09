using System.Collections.Generic;

namespace TrainSudoku.Core
{
    public sealed class WinResult
    {
        /// <summary>Condition 1: a continuous track runs from the entrance to the exit.</summary>
        public bool PathConnected { get; internal set; }

        /// <summary>Condition 2: every row and column clue is met exactly.</summary>
        public bool CluesSatisfied { get; internal set; }

        /// <summary>Condition 3: every connection of every piece meets a neighbour piece or a tunnel.</summary>
        public bool NoOpenEnds { get; internal set; }

        public bool IsWin => PathConnected && CluesSatisfied && NoOpenEnds;

        public bool[] RowSatisfied { get; internal set; }
        public bool[] ColumnSatisfied { get; internal set; }

        /// <summary>Cells visited from the entrance, whether or not the exit was reached.</summary>
        public List<(int X, int Y)> Path { get; internal set; }
    }

    /// <summary>Win condition, PRD section 3.4.</summary>
    public static class WinChecker
    {
        public static WinResult Evaluate(Board board)
        {
            var result = new WinResult();

            result.PathConnected = PathFinder.TryFindPath(board, out var path);
            result.Path = path;

            var level = board.Level;
            result.RowSatisfied = new bool[board.Height];
            result.ColumnSatisfied = new bool[board.Width];
            var clues = true;
            for (var y = 0; y < board.Height; y++)
            {
                result.RowSatisfied[y] = board.RowCount(y) == level.RowClues[y];
                clues &= result.RowSatisfied[y];
            }

            for (var x = 0; x < board.Width; x++)
            {
                result.ColumnSatisfied[x] = board.ColumnCount(x) == level.ColumnClues[x];
                clues &= result.ColumnSatisfied[x];
            }

            result.CluesSatisfied = clues;
            result.NoOpenEnds = HasNoOpenEnds(board);
            return result;
        }

        public static bool HasNoOpenEnds(Board board)
        {
            for (var y = 0; y < board.Height; y++)
            for (var x = 0; x < board.Width; x++)
            {
                if (!(board[x, y] is Piece piece)) continue;
                var (a, b) = PieceKeys.Connections(piece.Key);
                if (!IsConnected(board, x, y, a) || !IsConnected(board, x, y, b)) return false;
            }

            return true;
        }

        private static bool IsConnected(Board board, int x, int y, Direction direction)
        {
            var nx = x + direction.Dx();
            var ny = y + direction.Dy();
            if (!board.InBounds(nx, ny)) return board.HasTunnel(x, y, direction);
            return board[nx, ny] is Piece neighbour && neighbour.Has(direction.Opposite());
        }
    }
}
