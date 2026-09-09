using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class WinCheckerTests
    {
        [Test]
        public void SolvedReferenceLevelWins()
        {
            var result = WinChecker.Evaluate(TestLevels.SolvedPlanExample());
            Assert.IsTrue(result.PathConnected);
            Assert.IsTrue(result.CluesSatisfied);
            Assert.IsTrue(result.NoOpenEnds);
            Assert.IsTrue(result.IsWin);
            Assert.AreEqual(11, result.Path.Count);
            Assert.AreEqual((0, 3), result.Path[0]);
            Assert.AreEqual((5, 2), result.Path[10]);
            Assert.That(result.RowSatisfied, Is.All.True);
            Assert.That(result.ColumnSatisfied, Is.All.True);
        }

        [Test]
        public void MissingPieceBreaksEveryCondition()
        {
            var result = WinChecker.Evaluate(TestLevels.SolvedPlanExample((2, 4)));
            Assert.IsFalse(result.PathConnected);
            Assert.IsFalse(result.CluesSatisfied);
            Assert.IsFalse(result.NoOpenEnds);
            Assert.IsFalse(result.IsWin);
            Assert.IsFalse(result.RowSatisfied[4]);
            Assert.IsFalse(result.ColumnSatisfied[2]);
            Assert.IsTrue(result.RowSatisfied[3]);
            Assert.IsTrue(result.ColumnSatisfied[0]);
            CollectionAssert.AreEqual(new[] { (0, 3), (0, 2), (1, 2), (1, 3), (1, 4) }, result.Path);
        }

        [Test]
        public void EmptyBoardIsNotAWin()
        {
            var result = WinChecker.Evaluate(new Board(TestLevels.PlanExample()));
            Assert.IsFalse(result.PathConnected);
            Assert.IsEmpty(result.Path);
            Assert.IsFalse(result.CluesSatisfied);
            Assert.IsFalse(result.NoOpenEnds);
        }

        [Test]
        public void OpenEndFailsConditionThree()
        {
            var board = TestLevels.Corridor();
            TestLevels.Place(board, 0, 1, PieceKey.EW);
            var result = WinChecker.Evaluate(board);
            Assert.IsFalse(result.NoOpenEnds);
            Assert.IsFalse(result.PathConnected);
            Assert.AreEqual(1, result.Path.Count);
        }

        [Test]
        public void StraightCorridorWins()
        {
            var level = TestLevels.Empty(3, 3, new Tunnel(Direction.West, 1), new Tunnel(Direction.East, 1));
            level.RowClues[1] = 3;
            level.ColumnClues[0] = level.ColumnClues[1] = level.ColumnClues[2] = 1;
            var board = new Board(level);
            TestLevels.Place(board, 0, 1, PieceKey.EW);
            TestLevels.Place(board, 1, 1, PieceKey.EW);
            TestLevels.Place(board, 2, 1, PieceKey.EW);
            Assert.IsTrue(WinChecker.Evaluate(board).IsWin);
        }

        [Test]
        public void ClosedLoopOffThePathIsAllowedWhenCluesMatch()
        {
            var board = new Board(LevelText.Parse(TestLevels.LoopText));
            TestLevels.Place(board, 0, 0, PieceKey.EW);
            TestLevels.Place(board, 1, 0, PieceKey.EW);
            TestLevels.Place(board, 2, 0, PieceKey.EW);
            PlaceLoop(board);
            var result = WinChecker.Evaluate(board);
            Assert.IsTrue(result.NoOpenEnds);
            Assert.IsTrue(result.PathConnected);
            Assert.IsTrue(result.CluesSatisfied);
            Assert.IsTrue(result.IsWin);
            Assert.AreEqual(3, result.Path.Count);
        }

        [Test]
        public void ClosedLoopAloneHasNoOpenEndsButNoPath()
        {
            var board = new Board(LevelText.Parse(TestLevels.LoopText));
            PlaceLoop(board);
            var result = WinChecker.Evaluate(board);
            Assert.IsTrue(result.NoOpenEnds);
            Assert.IsFalse(result.PathConnected);
            Assert.IsFalse(result.CluesSatisfied);
            Assert.IsFalse(result.IsWin);
        }

        private static void PlaceLoop(Board board)
        {
            TestLevels.Place(board, 0, 1, PieceKey.SE);
            TestLevels.Place(board, 1, 1, PieceKey.SW);
            TestLevels.Place(board, 0, 2, PieceKey.NE);
            TestLevels.Place(board, 1, 2, PieceKey.NW);
        }
    }
}
