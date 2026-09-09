using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class LevelAuthoringTests
    {
        [Test]
        public void ResizeKeepsContentInsideTheNewBounds()
        {
            var level = TestLevels.PlanExample();
            var resized = LevelAuthoring.Resize(level, 4, 3);

            Assert.AreEqual("Plan Example", resized.Name);
            Assert.AreEqual(4, resized.Width);
            Assert.AreEqual(3, resized.Height);
            CollectionAssert.AreEqual(new[] { 2, 3, 1, 1 }, resized.ColumnClues);
            CollectionAssert.AreEqual(new[] { 0, 0, 4 }, resized.RowClues);
            CollectionAssert.AreEquivalent(new[] { new FixedPiece(1, 2, PieceKey.SW) }, resized.FixedPieces);
            Assert.AreEqual(new Tunnel(Direction.West, 2), resized.Entrance, "West index 3 is clamped to the new last row");
            Assert.AreEqual(new Tunnel(Direction.East, 2), resized.Exit);
        }

        [Test]
        public void ResizeGrowingPadsCluesWithZeroAndKeepsEverything()
        {
            var level = TestLevels.PlanExample();
            var resized = LevelAuthoring.Resize(level, 8, 7);

            CollectionAssert.AreEqual(new[] { 2, 3, 1, 1, 3, 1, 0, 0 }, resized.ColumnClues);
            CollectionAssert.AreEqual(new[] { 0, 0, 4, 3, 4, 0, 0 }, resized.RowClues);
            CollectionAssert.AreEquivalent(level.FixedPieces, resized.FixedPieces);
            Assert.AreEqual(level.Entrance, resized.Entrance);
            Assert.AreEqual(level.Exit, resized.Exit);
            Assert.AreEqual(7, resized.Exit.CellX(resized.Width), "An east tunnel follows the new right edge");
        }

        [Test]
        public void ResizeDoesNotTouchTheOriginal()
        {
            var level = TestLevels.PlanExample();
            LevelAuthoring.Resize(level, 2, 2);
            TestLevels.AssertEqual(TestLevels.PlanExample(), level);
        }

        [Test]
        public void DeriveCluesCountsFixedPiecesPerLine()
        {
            var level = TestLevels.PlanExample();
            LevelAuthoring.SetFixedPiecesFrom(level, TestLevels.SolvedPlanExample());
            LevelAuthoring.DeriveClues(level);

            CollectionAssert.AreEqual(new[] { 2, 3, 1, 1, 3, 1 }, level.ColumnClues);
            CollectionAssert.AreEqual(new[] { 0, 0, 4, 3, 4, 0 }, level.RowClues);
            Assert.AreEqual(1, Solver.CountSolutions(level));
        }

        [Test]
        public void DeriveCluesOnAnEmptyBoardZeroesEverything()
        {
            var level = TestLevels.PlanExample();
            level.FixedPieces.Clear();
            LevelAuthoring.DeriveClues(level);
            CollectionAssert.AreEqual(new int[6], level.ColumnClues);
            CollectionAssert.AreEqual(new int[6], level.RowClues);
        }

        [Test]
        public void SetFixedPieceReplacesAndRemoves()
        {
            var level = TestLevels.PlanExample();

            LevelAuthoring.SetFixedPiece(level, 1, 2, PieceKey.NS);
            Assert.IsTrue(level.TryGetFixedPiece(1, 2, out var replaced));
            Assert.AreEqual(PieceKey.NS, replaced.Key);
            Assert.AreEqual(2, level.FixedPieces.Count);

            LevelAuthoring.SetFixedPiece(level, 0, 0, PieceKey.EW);
            Assert.AreEqual(3, level.FixedPieces.Count);

            LevelAuthoring.SetFixedPiece(level, 1, 2, null);
            Assert.IsFalse(level.TryGetFixedPiece(1, 2, out _));
            Assert.AreEqual(2, level.FixedPieces.Count);

            LevelAuthoring.SetFixedPiece(level, 5, 5, null);
            Assert.AreEqual(2, level.FixedPieces.Count, "Removing from an empty cell is a no-op");
        }

        [Test]
        public void SetFixedPieceRejectsCellsOutsideTheBoard()
        {
            var level = TestLevels.PlanExample();
            Assert.Throws<System.ArgumentOutOfRangeException>(() => LevelAuthoring.SetFixedPiece(level, 6, 0, PieceKey.NS));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => LevelAuthoring.SetFixedPiece(level, 0, -1, null));
        }

        [Test]
        public void SetFixedPiecesFromCopiesPlayerAndFixedPieces()
        {
            var level = TestLevels.PlanExample();
            LevelAuthoring.SetFixedPiecesFrom(level, TestLevels.SolvedPlanExample());

            Assert.AreEqual(TestLevels.PlanExampleSolution.Length, level.FixedPieces.Count);
            foreach (var (x, y, key) in TestLevels.PlanExampleSolution)
            {
                Assert.IsTrue(level.TryGetFixedPiece(x, y, out var piece), $"({x},{y})");
                Assert.AreEqual(key, piece.Key);
            }

            Assert.IsTrue(WinChecker.Evaluate(new Board(level)).IsWin, "The fixed pieces alone form the finished track");
        }

        [TestCase("First Steps", "first-steps")]
        [TestCase("  Loop de Loop!  ", "loop-de-loop")]
        [TestCase("Level_07 (hard)", "level-07-hard")]
        [TestCase("", "")]
        [TestCase("***", "")]
        [TestCase(null, "")]
        public void SuggestIdMakesALowerCaseSlug(string name, string expected)
        {
            Assert.AreEqual(expected, LevelAuthoring.SuggestId(name));
        }
    }
}
