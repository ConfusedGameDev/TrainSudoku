namespace TrainSudoku.Core
{
    /// <summary>
    /// Backtracking solver. Cells are decided in row-major order; each is left empty or given a key legal against the
    /// neighbours decided so far. Pruning: an empty cell may not have a forced side, and line counts must stay
    /// able to reach their clue. A complete board counts only if <see cref="WinChecker"/> accepts it.
    /// </summary>
    public static class Solver
    {
        public const int DefaultLimit = 2;

        /// <summary>Counts solutions, stopping at <paramref name="limit"/>. The editor shows 0 / 1 / 2+.</summary>
        public static int CountSolutions(LevelData level, int limit = DefaultLimit)
        {
            var run = new Run(level, limit);
            run.Execute();
            return run.Count;
        }

        public static bool TrySolve(LevelData level, out Board solution)
        {
            var run = new Run(level, 1);
            run.Execute();
            solution = run.First;
            return solution != null;
        }

        private sealed class Run
        {
            private readonly LevelData _level;
            private readonly Board _board;
            private readonly int _limit;
            private readonly int[] _rowCounts;
            private readonly int[] _columnCounts;
            private readonly int[,] _rowFreeAfter;    // [y, x]: non-fixed cells in row y with column > x
            private readonly int[,] _columnFreeAfter; // [x, y]: non-fixed cells in column x with row > y

            public int Count { get; private set; }
            public Board First { get; private set; }

            public Run(LevelData level, int limit)
            {
                _level = level;
                _limit = limit;
                _board = new Board(level);

                _rowCounts = new int[level.Height];
                _columnCounts = new int[level.Width];
                for (var y = 0; y < level.Height; y++) _rowCounts[y] = _board.RowCount(y);
                for (var x = 0; x < level.Width; x++) _columnCounts[x] = _board.ColumnCount(x);

                _rowFreeAfter = new int[level.Height, level.Width];
                for (var y = 0; y < level.Height; y++)
                {
                    var free = 0;
                    for (var x = level.Width - 1; x >= 0; x--)
                    {
                        _rowFreeAfter[y, x] = free;
                        if (!_board[x, y].HasValue) free++;
                    }
                }

                _columnFreeAfter = new int[level.Width, level.Height];
                for (var x = 0; x < level.Width; x++)
                {
                    var free = 0;
                    for (var y = level.Height - 1; y >= 0; y--)
                    {
                        _columnFreeAfter[x, y] = free;
                        if (!_board[x, y].HasValue) free++;
                    }
                }
            }

            public void Execute()
            {
                if (_limit <= 0) return;

                var rowTotal = 0;
                var columnTotal = 0;
                for (var y = 0; y < _level.Height; y++)
                {
                    if (_rowCounts[y] > _level.RowClues[y]) return;
                    rowTotal += _level.RowClues[y];
                }

                for (var x = 0; x < _level.Width; x++)
                {
                    if (_columnCounts[x] > _level.ColumnClues[x]) return;
                    columnTotal += _level.ColumnClues[x];
                }

                if (rowTotal != columnTotal) return;

                Step(0);
            }

            /// <summary>Returns true when the limit has been reached and the search should stop.</summary>
            private bool Step(int index)
            {
                if (index == _level.Width * _level.Height)
                {
                    if (!WinChecker.Evaluate(_board).IsWin) return false;
                    Count++;
                    if (First == null) First = _board.Clone();
                    return Count >= _limit;
                }

                var x = index % _level.Width;
                var y = index / _level.Width;
                if (_board[x, y].HasValue) return Step(index + 1);

                var classes = Legality.Classify(_board, x, y);
                var forced = Legality.ForcedCount(classes);

                var rowClue = _level.RowClues[y];
                var columnClue = _level.ColumnClues[x];

                if (forced == 0
                    && _rowCounts[y] + _rowFreeAfter[y, x] >= rowClue
                    && _columnCounts[x] + _columnFreeAfter[x, y] >= columnClue)
                {
                    if (Step(index + 1)) return true;
                }

                if (forced > 2 || _rowCounts[y] >= rowClue || _columnCounts[x] >= columnClue) return false;

                foreach (var key in PieceKeys.All)
                {
                    if (!Legality.IsLegal(classes, key)) continue;

                    _board.SetUnchecked(x, y, new Piece(key, false));
                    _rowCounts[y]++;
                    _columnCounts[x]++;

                    var stop = Step(index + 1);

                    _board.SetUnchecked(x, y, null);
                    _rowCounts[y]--;
                    _columnCounts[x]--;

                    if (stop) return true;
                }

                return false;
            }
        }
    }
}
