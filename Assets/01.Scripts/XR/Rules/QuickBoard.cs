using System.Collections.Generic;
using TrainSudoku.Core;

namespace TrainSudoku.XR.Rules
{
    /// <summary>
    /// A testing aid (asked for at the XR7 headset check): a station with all but its last few rails already laid, so it
    /// plays in seconds. The rest of the solution goes in as fixed pieces, which keeps the board's one answer — fixed
    /// pieces taken from the only solution cannot add another — and the clues, tunnels and flow stay the real station's.
    /// It changes a <see cref="LevelData"/> in memory only: XR never writes a level asset (XR-PRD 10.6).
    /// </summary>
    public static class QuickBoard
    {
        /// <summary>
        /// Fixes every rail of the solution but the last <paramref name="rails"/> along the route, the ones nearest the
        /// exit, so the finish is what is played. False, with <paramref name="level"/> untouched, when the solver cannot
        /// finish the board or it has no more than <paramref name="rails"/> left to lay already.
        /// </summary>
        /// <param name="laid">How many rails went in as fixed pieces.</param>
        public static bool TryLeave(LevelData level, int rails, out int laid)
        {
            laid = 0;
            if (level == null || rails < 0 || !Solver.TrySolve(level, out var solution)) return false;

            // The route from the entrance gives the order; anything off it (there should be nothing) is laid first.
            PathFinder.TryFindPath(solution, out var route);
            var order = new Dictionary<(int, int), int>();
            for (var i = 0; i < route.Count; i++) order[route[i]] = i;

            var open = new List<(int X, int Y)>();
            for (var y = 0; y < solution.Height; y++)
            for (var x = 0; x < solution.Width; x++)
                if (solution[x, y] is Piece piece && !piece.IsFixed)
                    open.Add((x, y));
            if (open.Count <= rails) return false;

            open.Sort((a, b) => Position(order, a).CompareTo(Position(order, b)));
            for (var i = 0; i < open.Count - rails; i++)
            {
                var (x, y) = open[i];
                level.FixedPieces.Add(new FixedPiece(x, y, solution[x, y].Value.Key));
                laid++;
            }

            return true;
        }

        private static int Position(Dictionary<(int, int), int> order, (int X, int Y) cell) =>
            order.TryGetValue((cell.X, cell.Y), out var index) ? index : -1;
    }
}
