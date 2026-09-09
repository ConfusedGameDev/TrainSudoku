using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class BoardTests
    {
        [Test]
        public void FixedPiecesArePresentAndLocked()
        {
            var board = new Board(TestLevels.PlanExample());
            Assert.AreEqual(new Piece(PieceKey.SW, true), board[1, 2]);
            Assert.AreEqual(new Piece(PieceKey.NW, true), board[4, 4]);
            Assert.AreEqual(2, board.PieceCount);
            Assert.IsFalse(board.TryErase(1, 2));
            Assert.IsFalse(board.TryPlace(1, 2, PieceKey.NS));
            Assert.AreEqual(new Piece(PieceKey.SW, true), board[1, 2]);
        }

        [Test]
        public void PlaceEraseAndReset()
        {
            var board = new Board(TestLevels.PlanExample());
            TestLevels.Place(board, 0, 3, PieceKey.NW);
            Assert.AreEqual(new Piece(PieceKey.NW, false), board[0, 3]);
            Assert.AreEqual(1, board.ColumnCount(0));
            Assert.AreEqual(1, board.RowCount(3));

            Assert.IsTrue(board.TryErase(0, 3));
            Assert.IsNull(board[0, 3]);
            Assert.IsFalse(board.TryErase(0, 3));

            TestLevels.Place(board, 0, 3, PieceKey.NW);
            TestLevels.Place(board, 5, 2, PieceKey.EW);
            board.Reset();
            Assert.AreEqual(2, board.PieceCount);
            Assert.IsTrue(board[1, 2].Value.IsFixed);
            Assert.IsTrue(board[4, 4].Value.IsFixed);
        }

        [Test]
        public void TunnelQueries()
        {
            var board = new Board(TestLevels.PlanExample());
            Assert.IsTrue(board.IsEntrance(0, 3, Direction.West));
            Assert.IsTrue(board.HasTunnel(0, 3, Direction.West));
            Assert.IsFalse(board.IsExit(0, 3, Direction.West));
            Assert.IsTrue(board.IsExit(5, 2, Direction.East));
            Assert.IsFalse(board.HasTunnel(0, 2, Direction.West));
            Assert.IsFalse(board.HasTunnel(0, 3, Direction.East));
        }

        [Test]
        public void CloneIsIndependent()
        {
            var board = TestLevels.SolvedPlanExample();
            var clone = board.Clone();
            Assert.IsTrue(clone.TryErase(0, 3));
            Assert.IsNotNull(board[0, 3]);
            Assert.IsNull(clone[0, 3]);
            Assert.AreSame(board.Level, clone.Level);
        }

        [Test]
        public void OutOfBoundsReadsAreNull()
        {
            var board = TestLevels.Corridor();
            Assert.IsNull(board[-1, 0]);
            Assert.IsNull(board[0, 3]);
            Assert.IsFalse(board.IsEmpty(3, 0));
        }

        [Test]
        public void TunnelAdjacentCells()
        {
            Assert.AreEqual(0, new Tunnel(Direction.West, 3).CellX(6));
            Assert.AreEqual(3, new Tunnel(Direction.West, 3).CellY(6));
            Assert.AreEqual(5, new Tunnel(Direction.East, 2).CellX(6));
            Assert.AreEqual(2, new Tunnel(Direction.East, 2).CellY(6));
            Assert.AreEqual(4, new Tunnel(Direction.North, 4).CellX(6));
            Assert.AreEqual(0, new Tunnel(Direction.North, 4).CellY(6));
            Assert.AreEqual(1, new Tunnel(Direction.South, 1).CellX(6));
            Assert.AreEqual(5, new Tunnel(Direction.South, 1).CellY(6));
            Assert.IsTrue(new Tunnel(Direction.South, 5).IsOnPerimeter(6, 4));
            Assert.IsFalse(new Tunnel(Direction.South, 6).IsOnPerimeter(6, 4));
            Assert.IsFalse(new Tunnel(Direction.East, 4).IsOnPerimeter(6, 4));
        }

        [Test]
        public void LevelValidationReportsProblems()
        {
            var level = TestLevels.PlanExample();
            Assert.IsEmpty(level.Validate());

            level.ColumnClues[0] = 7;
            level.FixedPieces.Add(new FixedPiece(9, 9, PieceKey.NS));
            level.FixedPieces.Add(new FixedPiece(0, 0, PieceKey.NS));
            level.Exit = level.Entrance;
            var problems = level.Validate();
            Assert.That(problems, Has.Some.Contains("Column 0 clue 7"));
            Assert.That(problems, Has.Some.Contains("outside the board"));
            Assert.That(problems, Has.Some.Contains("off the board"));
            Assert.That(problems, Has.Some.Contains("same tunnel"));
            Assert.That(problems, Has.Some.Contains("Row 0 has 1 fixed pieces"));
        }
    }
}
