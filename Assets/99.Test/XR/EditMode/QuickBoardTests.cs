using System.Linq;
using NUnit.Framework;
using TrainSudoku.Core;
using TrainSudoku.XR.Rules;

namespace TrainSudoku.XR.Tests
{
    /// <summary>The quick-test board: the same station with only its last few rails left to lay.</summary>
    public class QuickBoardTests
    {
        [Test]
        public void LeavesExactlyTheAskedRailsToLay()
        {
            var level = XRTestBoards.PlanExampleLevel();
            Assert.IsTrue(Solver.TrySolve(level, out var before));
            var rails = Rails(before);

            Assert.IsTrue(QuickBoard.TryLeave(level, 3, out var laid));
            Assert.AreEqual(rails - 3, laid);

            Assert.IsTrue(Solver.TrySolve(level, out var after));
            var board = new Board(level);
            var open = 0;
            for (var y = 0; y < after.Height; y++)
            for (var x = 0; x < after.Width; x++)
            {
                if (!(after[x, y] is Piece piece)) continue;
                Assert.AreEqual(before[x, y].Value.Key, piece.Key, $"({x},{y}) keeps its solution key");
                if (!board[x, y].HasValue) open++;
            }

            Assert.AreEqual(3, open);
        }

        [Test]
        public void TheRailsLeftAreTheLastAlongTheRoute()
        {
            var level = XRTestBoards.PlanExampleLevel();
            Assert.IsTrue(Solver.TrySolve(level, out var solution));
            Assert.IsTrue(PathFinder.TryFindPath(solution, out var route));

            Assert.IsTrue(QuickBoard.TryLeave(level, 3, out _));
            var board = new Board(level);
            var firstOpen = route.FindIndex(cell => !board[cell.X, cell.Y].HasValue);
            Assert.GreaterOrEqual(firstOpen, 0);
            Assert.IsTrue(route.Skip(firstOpen).All(cell => !board[cell.X, cell.Y].HasValue || board[cell.X, cell.Y].Value.IsFixed));
            Assert.AreEqual(3, route.Skip(firstOpen).Count(cell => !board[cell.X, cell.Y].HasValue));
        }

        [Test]
        public void TheShortenedLevelIsStillWellFormed()
        {
            var level = XRTestBoards.PlanExampleLevel();
            Assert.IsTrue(QuickBoard.TryLeave(level, 3, out _));
            CollectionAssert.IsEmpty(level.Validate());
        }

        [Test]
        public void ABoardWithNoMoreThanThatLeftIsUntouched()
        {
            var level = XRTestBoards.PlanExampleLevel();
            var fixedBefore = level.FixedPieces.Count;
            Assert.IsFalse(QuickBoard.TryLeave(level, 1000, out var laid));
            Assert.AreEqual(0, laid);
            Assert.AreEqual(fixedBefore, level.FixedPieces.Count);
        }

        private static int Rails(Board solution)
        {
            var rails = 0;
            for (var y = 0; y < solution.Height; y++)
            for (var x = 0; x < solution.Width; x++)
                if (solution[x, y] is Piece piece && !piece.IsFixed)
                    rails++;
            return rails;
        }
    }
}
