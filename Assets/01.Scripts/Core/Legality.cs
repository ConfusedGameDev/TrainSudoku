using System.Collections.Generic;

namespace TrainSudoku.Core
{
    public enum DirectionClass
    {
        Open,
        Forced,
        Forbidden,
    }

    /// <summary>Placement legality, PRD section 3.3.</summary>
    public static class Legality
    {
        private static readonly PieceKey[] NoKeys = new PieceKey[0];

        /// <summary>Classifies the four sides of a cell. Index the result with <c>(int)Direction</c>.</summary>
        public static DirectionClass[] Classify(Board board, int x, int y)
        {
            var result = new DirectionClass[4];
            foreach (var direction in DirectionExtensions.All)
            {
                var nx = x + direction.Dx();
                var ny = y + direction.Dy();
                DirectionClass cls;
                if (!board.InBounds(nx, ny))
                    cls = board.HasTunnel(x, y, direction) ? DirectionClass.Forced : DirectionClass.Forbidden;
                else if (board[nx, ny] is Piece neighbour)
                    cls = neighbour.Has(direction.Opposite()) ? DirectionClass.Forced : DirectionClass.Forbidden;
                else
                    cls = DirectionClass.Open;
                result[(int)direction] = cls;
            }

            return result;
        }

        public static int ForcedCount(DirectionClass[] classes)
        {
            var count = 0;
            foreach (var cls in classes)
                if (cls == DirectionClass.Forced) count++;
            return count;
        }

        /// <summary>Keys that may be placed in the cell. Empty for occupied cells, cells outside the board, or three or more forced sides.</summary>
        public static IReadOnlyList<PieceKey> LegalKeys(Board board, int x, int y)
        {
            if (!board.IsEmpty(x, y)) return NoKeys;
            var classes = Classify(board, x, y);
            if (ForcedCount(classes) > 2) return NoKeys;

            var keys = new List<PieceKey>();
            foreach (var key in PieceKeys.All)
                if (IsLegal(classes, key)) keys.Add(key);
            return keys;
        }

        public static bool IsLegal(Board board, int x, int y, PieceKey key) =>
            board.IsEmpty(x, y) && IsLegal(Classify(board, x, y), key);

        /// <summary>A key is legal when neither connection is forbidden and every forced side is included.</summary>
        public static bool IsLegal(DirectionClass[] classes, PieceKey key)
        {
            var (a, b) = PieceKeys.Connections(key);
            if (classes[(int)a] == DirectionClass.Forbidden || classes[(int)b] == DirectionClass.Forbidden) return false;
            foreach (var direction in DirectionExtensions.All)
                if (classes[(int)direction] == DirectionClass.Forced && direction != a && direction != b) return false;
            return true;
        }
    }
}
