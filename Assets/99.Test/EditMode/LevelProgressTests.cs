using System;
using System.Collections.Generic;
using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class LevelProgressTests
    {
        [Test]
        public void CaptureRecordsOnlyPlayerPiecesInRasterOrder()
        {
            var board = new Board(TestLevels.PlanExample());
            TestLevels.Place(board, 0, 3, PieceKey.NW);
            TestLevels.Place(board, 0, 2, PieceKey.SE);
            TestLevels.Place(board, 4, 3, PieceKey.NS);

            var progress = LevelProgress.Capture(board, 12.5);

            Assert.AreEqual(12.5, progress.Elapsed);
            CollectionAssert.AreEqual(
                new[] { new PlacedPiece(0, 2, PieceKey.SE), new PlacedPiece(0, 3, PieceKey.NW), new PlacedPiece(4, 3, PieceKey.NS) },
                progress.Pieces,
                "fixed pieces at (1,2) and (4,4) are not recorded");
        }

        [Test]
        public void ApplyRestoresTheBoardStateWithoutTouchingFixedPieces()
        {
            var original = TestLevels.SolvedPlanExample((5, 2));
            var progress = LevelProgress.Capture(original, 3);

            var restored = new Board(TestLevels.PlanExample());
            Assert.AreEqual(progress.Pieces.Count, progress.ApplyTo(restored));

            for (var y = 0; y < original.Height; y++)
            for (var x = 0; x < original.Width; x++)
                Assert.AreEqual(original[x, y], restored[x, y], $"({x},{y})");
            Assert.IsTrue(restored[1, 2].Value.IsFixed);
            Assert.IsFalse(restored[0, 3].Value.IsFixed);
        }

        [Test]
        public void ApplySkipsCellsOutsideTheBoardOrAlreadyOccupied()
        {
            var progress = new LevelProgress(0, new[]
            {
                new PlacedPiece(1, 2, PieceKey.NS),   // fixed SW sits here
                new PlacedPiece(6, 0, PieceKey.EW),   // outside
                new PlacedPiece(-1, 0, PieceKey.EW),  // outside
                new PlacedPiece(0, 0, PieceKey.EW),   // fine, even though not legal on its own
            });

            var board = new Board(TestLevels.PlanExample());
            Assert.AreEqual(1, progress.ApplyTo(board));
            Assert.AreEqual(PieceKey.SW, board[1, 2].Value.Key);
            Assert.AreEqual(new Piece(PieceKey.EW, false), board[0, 0]);
        }

        [Test]
        public void ApplyDoesNotRunTheLegalityCheck()
        {
            // SW at (0,0) points west into the board edge, which TryPlace refuses; a saved board is trusted as it was.
            var progress = new LevelProgress(0, new[] { new PlacedPiece(0, 0, PieceKey.SW), new PlacedPiece(0, 1, PieceKey.NE) });
            var board = TestLevels.Corridor();
            Assert.AreEqual(2, progress.ApplyTo(board));
            Assert.AreEqual(PieceKey.SW, board[0, 0].Value.Key);
            Assert.AreEqual(PieceKey.NE, board[0, 1].Value.Key);
        }

        [Test]
        public void RejectsInvalidElapsedTimes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LevelProgress(-1, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LevelProgress(double.NaN, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LevelProgress(double.PositiveInfinity, null));
            Assert.IsEmpty(new LevelProgress(0, null).Pieces);
        }
    }
}
