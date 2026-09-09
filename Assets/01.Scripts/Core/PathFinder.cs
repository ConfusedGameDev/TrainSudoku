using System.Collections.Generic;

namespace TrainSudoku.Core
{
    public static class PathFinder
    {
        /// <summary>
        /// Walks the track from the entrance tunnel. Returns true when the walk reaches the exit tunnel.
        /// <paramref name="path"/> always holds the cells visited, in order, so callers can show partial progress.
        /// </summary>
        public static bool TryFindPath(Board board, out List<(int X, int Y)> path)
        {
            path = new List<(int X, int Y)>();
            var visited = new HashSet<(int, int)>();

            var entrance = board.Level.Entrance;
            var x = entrance.CellX(board.Width);
            var y = entrance.CellY(board.Height);
            var from = entrance.Side;

            while (true)
            {
                if (!(board[x, y] is Piece piece)) return false;
                if (!piece.Has(from)) return false;
                if (!visited.Add((x, y))) return false;
                path.Add((x, y));

                var next = PieceKeys.Other(piece.Key, from);
                if (board.IsExit(x, y, next)) return true;

                x += next.Dx();
                y += next.Dy();
                from = next.Opposite();
                if (!board.InBounds(x, y)) return false;
            }
        }
    }
}
