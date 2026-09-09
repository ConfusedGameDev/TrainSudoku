using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class LevelTextTests
    {
        [Test]
        public void ParsesTheReferenceLevel()
        {
            var level = LevelText.Parse(TestLevels.PlanExampleText);
            Assert.AreEqual("Plan Example", level.Name);
            Assert.AreEqual(6, level.Width);
            Assert.AreEqual(6, level.Height);
            CollectionAssert.AreEqual(new[] { 2, 3, 1, 1, 3, 1 }, level.ColumnClues);
            CollectionAssert.AreEqual(new[] { 0, 0, 4, 3, 4, 0 }, level.RowClues);
            CollectionAssert.AreEquivalent(
                new[] { new FixedPiece(1, 2, PieceKey.SW), new FixedPiece(4, 4, PieceKey.NW) },
                level.FixedPieces);
            Assert.AreEqual(new Tunnel(Direction.West, 3), level.Entrance);
            Assert.AreEqual(new Tunnel(Direction.East, 2), level.Exit);
        }

        [Test]
        public void SerializesTheReferenceLevelToTheCanonicalText()
        {
            var level = LevelText.Parse(TestLevels.PlanExampleText);
            Assert.AreEqual(TestLevels.PlanExampleText, LevelText.Serialize(level));
        }

        [Test]
        public void NameIsOptional()
        {
            var level = LevelText.Parse(TestLevels.TwoSolutionsText);
            Assert.AreEqual("", level.Name);
            Assert.AreEqual(4, level.Width);
            Assert.AreEqual(4, level.Height);
            Assert.AreEqual(new Tunnel(Direction.West, 0), level.Entrance);
            Assert.AreEqual(new Tunnel(Direction.East, 0), level.Exit);
        }

        [Test]
        public void TunnelsOnTopAndRightEdges()
        {
            var level = LevelText.Parse("  . S .\n  1 1 1\n. . . 1 E\n. . . 0\n. . . 0\n");
            Assert.AreEqual(3, level.Width);
            Assert.AreEqual(3, level.Height);
            Assert.AreEqual(new Tunnel(Direction.North, 1), level.Entrance);
            Assert.AreEqual(new Tunnel(Direction.East, 0), level.Exit);
        }

        [Test]
        public void TunnelsOnBottomAndLeftEdges()
        {
            var level = LevelText.Parse("name: Sides\n1 1 1 1\nE . . . . 1\n. . . . 0\n. . . S\n");
            Assert.AreEqual(4, level.Width);
            Assert.AreEqual(2, level.Height);
            Assert.AreEqual("Sides", level.Name);
            Assert.AreEqual(new Tunnel(Direction.South, 3), level.Entrance);
            Assert.AreEqual(new Tunnel(Direction.West, 0), level.Exit);
        }

        [Test]
        public void NonSquareBoardsInferSizeFromTokens()
        {
            var level = LevelText.Parse("1 0\nS NS . 1 E\n. . 0\n. . 0\n");
            Assert.AreEqual(2, level.Width);
            Assert.AreEqual(3, level.Height);
            Assert.AreEqual(1, level.FixedPieces.Count);
            Assert.AreEqual(new FixedPiece(0, 0, PieceKey.NS), level.FixedPieces[0]);
        }

        [TestCase(TestLevels.PlanExampleText)]
        [TestCase(TestLevels.TwoSolutionsText)]
        [TestCase(TestLevels.LoopText)]
        [TestCase("  . S .\n  1 1 1\n. . . 1 E\n. . . 0\n. . . 0\n")]
        [TestCase("name: Sides\n1 1 1 1\nE . . . . 1\n. . . . 0\n. . . S\n")]
        [TestCase("  E . .\n  1 1 1\n. NE . 1\n. . . 0\n  . S .\n")]
        public void SerializeThenParseRoundTrips(string text)
        {
            var original = LevelText.Parse(text);
            var serialized = LevelText.Serialize(original);
            var reparsed = LevelText.Parse(serialized);
            TestLevels.AssertEqual(original, reparsed);
            Assert.AreEqual(serialized, LevelText.Serialize(reparsed));
        }

        [Test]
        public void WindowsLineEndingsAndBlankLinesAreAccepted()
        {
            var level = LevelText.Parse("\r\n  1 1\r\n\r\nS . . 2 E\r\n. . 0\r\n\r\n");
            Assert.AreEqual(2, level.Width);
            Assert.AreEqual(2, level.Height);
        }

        [TestCase("", 0)]
        [TestCase("  1 1\n", 1)]
        [TestCase("  1 1\nS . 1 E\n. . 0\n", 2)]
        [TestCase("  1 1\nS . X 1 E\n. . 0\n", 2)]
        [TestCase("  1 1\nS . . 1 E\n. . 0 Q\n", 3)]
        [TestCase("  1 1\nS . . 1\n. . 0\n", 0)]
        [TestCase("  1 1\nS . . 1 S\n. . 0\n", 2)]
        [TestCase("  . S .\n  1 1\n. . 1 E\n. . 0\n", 1)]
        [TestCase("  1 1\n. . 1 E\n. . 0\n  . S\n  . .\n", 5)]
        [TestCase("  a 1\n. . 1 E\n. . 0\n", 1)]
        public void MalformedTextThrowsWithTheLineNumber(string text, int expectedLine)
        {
            var ex = Assert.Throws<LevelTextException>(() => LevelText.Parse(text));
            Assert.AreEqual(expectedLine, ex.Line, ex.Message);
        }
    }
}
