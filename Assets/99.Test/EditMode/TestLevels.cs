using System;
using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    /// <summary>Shared fixtures. The Plan example is the Plan.md map with concrete clues and a hand-verified unique solution.</summary>
    public static class TestLevels
    {
        public const string PlanExampleText =
            "name: Plan Example\n" +
            "  2 3 1 1 3 1\n" +
            ". . . . . . 0\n" +
            ". . . . . . 0\n" +
            ". SW . . . . 4 E\n" +
            "S . . . . . . 3\n" +
            ". . . . NW . 4\n" +
            ". . . . . . 0\n";

        public static readonly (int X, int Y, PieceKey Key)[] PlanExampleSolution =
        {
            (0, 3, PieceKey.NW), (0, 2, PieceKey.SE), (1, 2, PieceKey.SW), (1, 3, PieceKey.NS), (1, 4, PieceKey.NE),
            (2, 4, PieceKey.EW), (3, 4, PieceKey.EW), (4, 4, PieceKey.NW), (4, 3, PieceKey.NS), (4, 2, PieceKey.SE),
            (5, 2, PieceKey.EW),
        };

        /// <summary>Row 0 straight across plus either a detached 2x2 loop or a detour through the same cells.</summary>
        public const string TwoSolutionsText =
            "  1 3 3 1\n" +
            "S . . . . 4 E\n" +
            ". . . . 2\n" +
            ". . . . 2\n" +
            ". . . . 0\n";

        /// <summary>Straight track on row 0 plus a closed loop below it; clues match, so it is a win.</summary>
        public const string LoopText =
            "  3 3 1\n" +
            "S . . . 3 E\n" +
            ". . . 2\n" +
            ". . . 2\n";

        public static LevelData PlanExample() => LevelText.Parse(PlanExampleText);

        /// <summary>Places every solution piece except the fixed ones and those listed in <paramref name="skip"/>.</summary>
        public static Board SolvedPlanExample(params (int X, int Y)[] skip)
        {
            var board = new Board(PlanExample());
            foreach (var (x, y, key) in PlanExampleSolution)
            {
                if (board[x, y].HasValue) continue;
                if (Array.IndexOf(skip, (x, y)) >= 0) continue;
                Assert.IsTrue(board.TryPlace(x, y, key), $"Could not place {key} at ({x},{y}).");
            }

            return board;
        }

        public static LevelData Empty(int width, int height, Tunnel entrance, Tunnel exit)
        {
            return new LevelData(width, height) { Entrance = entrance, Exit = exit };
        }

        /// <summary>3x3 board, entrance west of (0,1), exit east of (2,1), all clues zero.</summary>
        public static Board Corridor() =>
            new Board(Empty(3, 3, new Tunnel(Direction.West, 1), new Tunnel(Direction.East, 1)));

        public static void Place(Board board, int x, int y, PieceKey key)
        {
            Assert.IsTrue(board.TryPlace(x, y, key), $"Could not place {key} at ({x},{y}).");
        }

        public static void AssertEqual(LevelData expected, LevelData actual)
        {
            Assert.AreEqual(expected.Name, actual.Name, "Name");
            Assert.AreEqual(expected.Width, actual.Width, "Width");
            Assert.AreEqual(expected.Height, actual.Height, "Height");
            CollectionAssert.AreEqual(expected.ColumnClues, actual.ColumnClues, "ColumnClues");
            CollectionAssert.AreEqual(expected.RowClues, actual.RowClues, "RowClues");
            CollectionAssert.AreEquivalent(expected.FixedPieces, actual.FixedPieces, "FixedPieces");
            Assert.AreEqual(expected.Entrance, actual.Entrance, "Entrance");
            Assert.AreEqual(expected.Exit, actual.Exit, "Exit");
        }
    }
}
