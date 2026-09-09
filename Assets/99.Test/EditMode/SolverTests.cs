using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class SolverTests
    {
        [Test]
        public void ReferenceLevelHasExactlyOneSolution()
        {
            Assert.AreEqual(1, Solver.CountSolutions(TestLevels.PlanExample()));
            Assert.AreEqual(1, Solver.CountSolutions(TestLevels.PlanExample(), 100));
        }

        [Test]
        public void ReferenceLevelSolutionMatchesTheExpectedTrack()
        {
            Assert.IsTrue(Solver.TrySolve(TestLevels.PlanExample(), out var solution));
            Assert.AreEqual(TestLevels.PlanExampleSolution.Length, solution.PieceCount);
            foreach (var (x, y, key) in TestLevels.PlanExampleSolution)
                Assert.AreEqual(key, solution[x, y]?.Key, $"({x},{y})");
            Assert.IsTrue(solution[1, 2].Value.IsFixed);
            Assert.IsTrue(solution[4, 4].Value.IsFixed);
            Assert.IsFalse(solution[0, 3].Value.IsFixed);
            Assert.IsTrue(WinChecker.Evaluate(solution).IsWin);
        }

        [Test]
        public void AmbiguousLevelReportsTwoOrMore()
        {
            var level = LevelText.Parse(TestLevels.TwoSolutionsText);
            Assert.AreEqual(2, Solver.CountSolutions(level));
            Assert.AreEqual(1, Solver.CountSolutions(level, 1));
            Assert.GreaterOrEqual(Solver.CountSolutions(level, 100), 2);
            Assert.IsTrue(Solver.TrySolve(level, out var solution));
            Assert.IsTrue(WinChecker.Evaluate(solution).IsWin);
        }

        [Test]
        public void ContradictoryCluesHaveNoSolution()
        {
            var level = TestLevels.PlanExample();
            level.ColumnClues[5] = 2;
            Assert.AreEqual(0, Solver.CountSolutions(level));
            Assert.IsFalse(Solver.TrySolve(level, out _));
        }

        [Test]
        public void UnreachableExitHasNoSolution()
        {
            var level = TestLevels.PlanExample();
            level.RowClues[2] = 3;
            level.RowClues[1] = 1;
            Assert.AreEqual(0, Solver.CountSolutions(level));
        }

        [Test]
        public void FixedPieceExceedingItsClueHasNoSolution()
        {
            var level = TestLevels.PlanExample();
            level.ColumnClues[1] = 0;
            level.RowClues[2] = 1;
            Assert.AreEqual(0, Solver.CountSolutions(level));
        }

        [Test]
        public void NodeBudgetStopsTheSearchAndReportsExhaustion()
        {
            var level = TestLevels.PlanExample();
            var tiny = Solver.Solve(level, Solver.DefaultLimit, 10);
            Assert.IsTrue(tiny.Exhausted);
            Assert.AreEqual(11, tiny.Nodes, "The search stops on the first node past the budget");
            Assert.AreEqual(0, tiny.Count);
            Assert.IsNull(tiny.First);

            var full = Solver.Solve(level);
            Assert.IsFalse(full.Exhausted);
            Assert.AreEqual(1, full.Count);
            Assert.IsNotNull(full.First);
            Assert.IsTrue(WinChecker.Evaluate(full.First).IsWin);
            Assert.Greater(full.Nodes, 11);

            var enough = Solver.Solve(level, Solver.DefaultLimit, full.Nodes);
            Assert.IsFalse(enough.Exhausted);
            Assert.AreEqual(1, enough.Count);
        }

        [Test]
        public void LoopLevelHasExactlyTwoSolutions()
        {
            // Straight row plus a detached loop, or a snake through the same four cells.
            Assert.AreEqual(2, Solver.CountSolutions(LevelText.Parse(TestLevels.LoopText), 100));
        }
    }
}
