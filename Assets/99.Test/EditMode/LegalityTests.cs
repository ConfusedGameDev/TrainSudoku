using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class LegalityTests
    {
        private static DirectionClass Class(DirectionClass[] classes, Direction direction) => classes[(int)direction];

        [Test]
        public void TunnelSideIsForced()
        {
            var classes = Legality.Classify(TestLevels.Corridor(), 0, 1);
            Assert.AreEqual(DirectionClass.Forced, Class(classes, Direction.West));
        }

        [Test]
        public void BoardEdgeWithoutTunnelIsForbidden()
        {
            var classes = Legality.Classify(TestLevels.Corridor(), 0, 0);
            Assert.AreEqual(DirectionClass.Forbidden, Class(classes, Direction.West));
            Assert.AreEqual(DirectionClass.Forbidden, Class(classes, Direction.North));
            Assert.AreEqual(DirectionClass.Open, Class(classes, Direction.East));
            Assert.AreEqual(DirectionClass.Open, Class(classes, Direction.South));
        }

        [Test]
        public void NeighbourPointingAtTheCellIsForced()
        {
            var board = TestLevels.Corridor();
            TestLevels.Place(board, 1, 1, PieceKey.EW);
            var classes = Legality.Classify(board, 0, 1);
            Assert.AreEqual(DirectionClass.Forced, Class(classes, Direction.East));
        }

        [Test]
        public void NeighbourNotPointingAtTheCellIsForbidden()
        {
            var board = TestLevels.Corridor();
            TestLevels.Place(board, 1, 1, PieceKey.NS);
            var classes = Legality.Classify(board, 0, 1);
            Assert.AreEqual(DirectionClass.Forbidden, Class(classes, Direction.East));
            CollectionAssert.AreEquivalent(new[] { PieceKey.NW, PieceKey.SW }, Legality.LegalKeys(board, 0, 1));
        }

        [Test]
        public void EmptyNeighbourIsOpen()
        {
            var classes = Legality.Classify(TestLevels.Corridor(), 1, 1);
            foreach (var direction in DirectionExtensions.All)
                Assert.AreEqual(DirectionClass.Open, Class(classes, direction), direction.ToString());
            Assert.AreEqual(6, Legality.LegalKeys(TestLevels.Corridor(), 1, 1).Count);
        }

        [Test]
        public void TwoForcedSidesLeaveExactlyOneKey()
        {
            var board = TestLevels.Corridor();
            TestLevels.Place(board, 1, 1, PieceKey.EW);
            CollectionAssert.AreEqual(new[] { PieceKey.EW }, Legality.LegalKeys(board, 0, 1));
        }

        [Test]
        public void ThreeForcedSidesLeaveNoKeys()
        {
            var board = TestLevels.Corridor();
            board.SetUnchecked(1, 0, new Piece(PieceKey.NS, false));
            board.SetUnchecked(0, 1, new Piece(PieceKey.EW, false));
            board.SetUnchecked(2, 1, new Piece(PieceKey.EW, false));
            Assert.AreEqual(3, Legality.ForcedCount(Legality.Classify(board, 1, 1)));
            Assert.IsEmpty(Legality.LegalKeys(board, 1, 1));
            Assert.IsFalse(board.TryPlace(1, 1, PieceKey.NS));
        }

        [Test]
        public void OccupiedAndOutOfBoundsCellsHaveNoKeys()
        {
            var board = TestLevels.Corridor();
            TestLevels.Place(board, 1, 1, PieceKey.EW);
            Assert.IsEmpty(Legality.LegalKeys(board, 1, 1));
            Assert.IsEmpty(Legality.LegalKeys(board, -1, 0));
            Assert.IsEmpty(Legality.LegalKeys(board, 3, 3));
            Assert.IsFalse(board.TryPlace(1, 1, PieceKey.NS));
            Assert.IsFalse(board.TryPlace(3, 3, PieceKey.NS));
        }

        [Test]
        public void TryPlaceRejectsKeysThatMissAForcedSide()
        {
            var board = TestLevels.Corridor();
            Assert.IsFalse(board.TryPlace(0, 1, PieceKey.NS));
            Assert.IsTrue(board.IsEmpty(0, 1));
            Assert.IsTrue(board.TryPlace(0, 1, PieceKey.NW));
            Assert.AreEqual(PieceKey.NW, board[0, 1].Value.Key);
        }

        [Test]
        public void TryPlaceRejectsKeysThatUseAForbiddenSide()
        {
            var board = TestLevels.Corridor();
            Assert.IsFalse(board.TryPlace(0, 0, PieceKey.NS));
            Assert.IsFalse(board.TryPlace(0, 0, PieceKey.EW));
            Assert.IsTrue(board.TryPlace(0, 0, PieceKey.SE));
        }
    }
}
