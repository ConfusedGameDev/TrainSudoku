using System;
using System.Collections.Generic;
using System.Text;
using TrainSudoku.Core;
using UnityEngine;

namespace TrainSudoku.Editor
{
    /// <summary>
    /// Searches for a uniquely solvable board. Written for M14 to replace <c>Hard</c> as the line's finale (D17), and
    /// kept because it is the provenance of <c>Ravensmoor.asset</c>: the search is deterministic, so
    /// <c>Search(7, 7, 29, 60, 20260910, 2000000, 300000, null)</c> reproduces that exact board.
    /// </summary>
    /// <remarks>
    /// Author by solution: walk a random self-avoiding route from the entrance to the exit, read the row and column
    /// clues off that route, then throw the route away and ask the solver how many boards those clues admit. Keep the
    /// first that admits exactly one. A long route is preferred because it fills more of the grid, which is what makes
    /// a finale feel like one.
    ///
    /// It is not wired to a menu item. Nothing in the shipped game calls it; it is run from an editor command when a
    /// board is needed. If more levels are ever authored this way, that is the moment to give it a window.
    /// </remarks>
    public static class FinaleGenerator
    {
        public sealed class Candidate
        {
            public LevelData Level;
            public List<FixedPiece> Solution;
            public long Nodes;
            public int Attempt;

            /// <summary>How many of the route's cells are pre-placed for the player.</summary>
            public int FixedCount;
        }

        /// <summary>
        /// The first uniquely solvable board found, or null.
        /// </summary>
        /// <remarks>
        /// Clues alone are not enough at this size: a 7x7 with a thirty-cell route leaves a search tree this
        /// backtracker cannot finish, which is exactly the complaint against <c>Hard</c>. Pre-placed pieces are the
        /// lever — they prune the tree until uniqueness is provable, and they are also what gives the player a
        /// foothold. So each route is tried with progressively more of its own cells revealed, and the sparsest board
        /// that admits exactly one solution wins.
        /// </remarks>
        public static Candidate Search(int width, int height, int minRouteLength, int attempts, int seed,
            long solverBudget, long walkBudget, StringBuilder log, int[] fixedCounts = null)
        {
            fixedCounts = fixedCounts ?? new[] { 0, 3, 5, 7, 9, 11 };
            var rng = new System.Random(seed);
            var solution = new List<FixedPiece>();

            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var entrance = new Tunnel(Direction.West, rng.Next(height));
                var exit = new Tunnel(Direction.East, rng.Next(height));

                var walk = new Walker(width, height, walkBudget, rng);
                if (!walk.Run(0, entrance.Index, width - 1, exit.Index, minRouteLength))
                {
                    log?.AppendLine($"attempt {attempt}: no route of {minRouteLength}+ cells");
                    continue;
                }

                if (!BuildPieces(walk.Path, width, entrance, exit, solution))
                {
                    log?.AppendLine($"attempt {attempt}: route could not be tracked");
                    continue;
                }

                var level = new LevelData(width, height) { Name = "Finale", Entrance = entrance, Exit = exit };
                foreach (var piece in solution) level.FixedPieces.Add(piece);
                LevelAuthoring.DeriveClues(level);   // clues come from the whole route
                level.FixedPieces.Clear();           // then the answer is thrown away

                var issues = level.Validate();
                if (issues.Count > 0)
                {
                    log?.AppendLine($"attempt {attempt}: invalid - {string.Join("; ", issues)}");
                    continue;
                }

                // Reveal cells in a fixed shuffled order so each larger count is a superset of the smaller one.
                var reveal = new List<int>();
                for (var i = 0; i < solution.Count; i++) reveal.Add(i);
                for (var i = reveal.Count - 1; i > 0; i--)
                {
                    var j = rng.Next(i + 1);
                    var swap = reveal[i];
                    reveal[i] = reveal[j];
                    reveal[j] = swap;
                }

                foreach (var fixedCount in fixedCounts)
                {
                    if (fixedCount > solution.Count) break;
                    level.FixedPieces.Clear();
                    for (var i = 0; i < fixedCount; i++) level.FixedPieces.Add(solution[reveal[i]]);

                    var solve = Solver.Solve(level, 2, solverBudget);
                    log?.AppendLine($"attempt {attempt}: route {solution.Count} cells, W{entrance.Index}->E{exit.Index}, " +
                                    $"fixed {fixedCount} -> solutions {solve.Count}, exhausted {solve.Exhausted}, nodes {solve.Nodes:N0}");

                    if (solve.Exhausted || solve.Count != 1) continue;

                    return new Candidate
                    {
                        Level = level,
                        Solution = new List<FixedPiece>(solution),
                        Nodes = solve.Nodes,
                        Attempt = attempt,
                        FixedCount = fixedCount,
                    };
                }
            }

            return null;
        }

        /// <summary>Each cell connects the side it was entered by to the side it leaves by.</summary>
        private static bool BuildPieces(List<int> path, int width, Tunnel entrance, Tunnel exit, List<FixedPiece> into)
        {
            into.Clear();
            for (var i = 0; i < path.Count; i++)
            {
                var inSide = i == 0 ? entrance.Side : Towards(path[i], path[i - 1], width);
                var outSide = i == path.Count - 1 ? exit.Side : Towards(path[i], path[i + 1], width);
                if (inSide == outSide) return false;
                if (!PieceKeys.TryFromDirections(inSide, outSide, out var key)) return false;
                into.Add(new FixedPiece(path[i] % width, path[i] / width, key));
            }

            return true;
        }

        private static Direction Towards(int from, int to, int width)
        {
            var fx = from % width;
            var tx = to % width;
            if (tx > fx) return Direction.East;
            if (tx < fx) return Direction.West;
            return to / width > from / width ? Direction.South : Direction.North;
        }

        public static string Render(LevelData level, IReadOnlyList<FixedPiece> solution)
        {
            var grid = new string[level.Width, level.Height];
            if (solution != null)
                foreach (var piece in solution) grid[piece.X, piece.Y] = piece.Key.ToString();

            var sb = new StringBuilder();
            sb.Append("       ");
            for (var x = 0; x < level.Width; x++) sb.Append(level.ColumnClues[x].ToString().PadLeft(3));
            sb.AppendLine("   (column clues)");
            for (var y = 0; y < level.Height; y++)
            {
                sb.Append("    ");
                for (var x = 0; x < level.Width; x++) sb.Append((grid[x, y] ?? ".").PadLeft(3));
                sb.Append("   | ").Append(level.RowClues[y]);
                sb.AppendLine();
            }

            sb.AppendLine($"    entrance {level.Entrance.Side}[{level.Entrance.Index}]  " +
                          $"exit {level.Exit.Side}[{level.Exit.Index}]  " +
                          $"track pieces {(solution != null ? solution.Count : 0)}");
            return sb.ToString();
        }

        /// <summary>Randomised depth-first self-avoiding walk with a node budget.</summary>
        private sealed class Walker
        {
            private readonly int _width;
            private readonly int _height;
            private readonly bool[] _visited;
            private readonly System.Random _rng;
            private long _budget;

            public readonly List<int> Path = new List<int>();

            public Walker(int width, int height, long budget, System.Random rng)
            {
                _width = width;
                _height = height;
                _budget = budget;
                _rng = rng;
                _visited = new bool[width * height];
            }

            public bool Run(int startX, int startY, int goalX, int goalY, int minLength) =>
                Step(startY * _width + startX, goalY * _width + goalX, minLength);

            private bool Step(int cell, int goal, int minLength)
            {
                if (_budget-- <= 0) return false;

                _visited[cell] = true;
                Path.Add(cell);

                if (cell == goal)
                {
                    if (Path.Count >= minLength) return true;
                    Retreat(cell);
                    return false;
                }

                var order = new[] { 0, 1, 2, 3 };
                for (var i = 3; i > 0; i--)
                {
                    var j = _rng.Next(i + 1);
                    var swap = order[i];
                    order[i] = order[j];
                    order[j] = swap;
                }

                foreach (var index in order)
                {
                    var direction = DirectionExtensions.All[index];
                    var nx = cell % _width + direction.Dx();
                    var ny = cell / _width + direction.Dy();
                    if (nx < 0 || ny < 0 || nx >= _width || ny >= _height) continue;
                    var next = ny * _width + nx;
                    if (_visited[next]) continue;
                    if (Step(next, goal, minLength)) return true;
                }

                Retreat(cell);
                return false;
            }

            private void Retreat(int cell)
            {
                _visited[cell] = false;
                Path.RemoveAt(Path.Count - 1);
            }
        }
    }
}
