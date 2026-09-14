using System.Linq;
using NUnit.Framework;
using TrainSudoku.XR.Rules;

namespace TrainSudoku.XR.Tests
{
    /// <summary>XR-PRD 6.2: the network map shows the lines the player has earned and the first one they have not.</summary>
    public class MapRevealTests
    {
        [Test]
        public void EveryOpenLineAndTheFirstClosedOne()
        {
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, MapReveal.Revealed(6, null, line => line < 3));
        }

        [Test]
        public void TheFirstLineAloneOpensOntoTheSecond()
        {
            CollectionAssert.AreEqual(new[] { 0, 1 }, MapReveal.Revealed(24, null, line => line == 0));
        }

        [Test]
        public void EverythingOpenRevealsTheWholeNetwork()
        {
            CollectionAssert.AreEqual(Enumerable.Range(0, 24), MapReveal.Revealed(24, null, _ => true));
        }

        [Test]
        public void NoUnlockRuleRevealsEveryDrawableLine()
        {
            CollectionAssert.AreEqual(new[] { 0, 1, 3, 4 }, MapReveal.Revealed(5, line => line != 2, null));
        }

        [Test]
        public void AnUndrawableLineIsSkippedWithoutEndingTheReveal()
        {
            CollectionAssert.AreEqual(new[] { 0, 2, 3 }, MapReveal.Revealed(6, line => line != 1, line => line < 3));
        }

        [Test]
        public void ExactlyOneLockedLineIsOnTheMapUntilTheEnd()
        {
            const int lines = 8;
            for (var open = 1; open <= lines; open++)
            {
                var unlockedCount = open;
                var revealed = MapReveal.Revealed(lines, null, line => line < unlockedCount);
                var locked = revealed.Count(line => line >= unlockedCount);
                Assert.AreEqual(open < lines ? 1 : 0, locked, $"{open} open");
                Assert.AreEqual(revealed.Count - 1, revealed.Last() - revealed.First(), "the open lines are a prefix");
            }
        }

        [Test]
        public void NoLinesNothingRevealed()
        {
            Assert.IsEmpty(MapReveal.Revealed(0, null, null));
        }
    }
}
