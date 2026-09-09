using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class PlacementSessionTests
    {
        private Board _board;
        private PlacementSession _session;

        [SetUp]
        public void CreateSession()
        {
            _board = TestLevels.Corridor(); // 3x3, entrance west of (0,1), exit east of (2,1)
            _session = new PlacementSession(_board);
        }

        [Test]
        public void SelectingAnOpenCellShowsEveryLegalSideWithForcedOnesFlagged()
        {
            Assert.AreEqual(SelectOutcome.Selected, _session.Select(0, 1));
            Assert.IsTrue(_session.IsActive);
            Assert.AreEqual((0, 1), (_session.X, _session.Y));
            Assert.IsNull(_session.First);
            CollectionAssert.AreEquivalent(DirectionExtensions.All, _session.Available);
            Assert.IsTrue(_session.IsForced(Direction.West), "The tunnel forces the west side");
            Assert.IsFalse(_session.IsForced(Direction.North));
        }

        [Test]
        public void CornerCellWithOneLegalKeyIsPlacedImmediately()
        {
            Assert.AreEqual(SelectOutcome.AutoPlaced, _session.Select(0, 0));
            Assert.IsFalse(_session.IsActive);
            Assert.AreEqual(PieceKey.SE, _board[0, 0]?.Key);
            Assert.AreEqual(PieceKey.SE, _session.LastPlaced);
            Assert.AreEqual((0, 0), _session.LastPlacedCell);
        }

        [Test]
        public void ChoosingTwoOpenSidesPlacesThatPiece()
        {
            _session.Select(0, 1);
            Assert.AreEqual(ChooseOutcome.Narrowed, _session.Choose(Direction.West));
            Assert.AreEqual(Direction.West, _session.First);
            CollectionAssert.AreEquivalent(new[] { Direction.North, Direction.East, Direction.South }, _session.Available);

            Assert.AreEqual(ChooseOutcome.Placed, _session.Choose(Direction.East));
            Assert.AreEqual(PieceKey.EW, _board[0, 1]?.Key);
            Assert.IsFalse(_board[0, 1].Value.IsFixed);
            Assert.IsFalse(_session.IsActive);
            Assert.IsEmpty(_session.Available);
        }

        [Test]
        public void ChoosingANonForcedSideFirstLeavesOnlyTheForcedOneAndPlaces()
        {
            _session.Select(0, 1);
            // North first: the only legal partner is the forced west side, so the piece is placed at once.
            Assert.AreEqual(ChooseOutcome.Placed, _session.Choose(Direction.North));
            Assert.AreEqual(PieceKey.NW, _board[0, 1]?.Key);
        }

        [Test]
        public void SidesAreIgnoredWhenNothingIsSelectedOrNotOffered()
        {
            Assert.AreEqual(ChooseOutcome.Ignored, _session.Choose(Direction.North));

            _board.SetUnchecked(1, 0, new Piece(PieceKey.NS, false)); // not adjacent to (0,1): changes nothing there
            _session.Select(0, 1);
            CollectionAssert.Contains(_session.Available, Direction.North, "north neighbour (0,0) is empty so north is open");
            CollectionAssert.Contains(_session.Available, Direction.East);
            _session.Cancel();

            _board.SetUnchecked(1, 1, new Piece(PieceKey.NS, false)); // does not point west: forbids the east side of (0,1)
            Assert.AreEqual(SelectOutcome.Selected, _session.Select(0, 1));
            CollectionAssert.DoesNotContain(_session.Available, Direction.East);
            Assert.AreEqual(ChooseOutcome.Ignored, _session.Choose(Direction.East));
            Assert.IsTrue(_session.IsActive, "an ignored tap keeps the selection");
        }

        [Test]
        public void TappingTheSelectedCellAgainCancels()
        {
            _session.Select(0, 1);
            Assert.AreEqual(SelectOutcome.Cancelled, _session.Select(0, 1));
            Assert.IsFalse(_session.IsActive);
            Assert.IsEmpty(_session.Available);
            Assert.IsNull(_board[0, 1]);
        }

        [Test]
        public void SelectingAnotherCellMovesTheSelection()
        {
            _session.Select(0, 1);
            _session.Choose(Direction.West);
            Assert.AreEqual(SelectOutcome.Selected, _session.Select(1, 1));
            Assert.AreEqual((1, 1), (_session.X, _session.Y));
            Assert.IsNull(_session.First, "the half-made choice is dropped");
        }

        [Test]
        public void OccupiedAndUnfillableCellsAreRejected()
        {
            _board.SetUnchecked(1, 1, new Piece(PieceKey.NS, false));
            Assert.AreEqual(SelectOutcome.Rejected, _session.Select(1, 1), "occupied");

            // (0,0): edges forbid north and west; NS at (1,0) forbids east; EW at (0,1) forbids south.
            _board.SetUnchecked(1, 0, new Piece(PieceKey.NS, false));
            _board.SetUnchecked(0, 1, new Piece(PieceKey.EW, false));
            Assert.AreEqual(SelectOutcome.Rejected, _session.Select(0, 0), "no legal key");
            Assert.IsFalse(_session.IsActive);
        }

        [Test]
        public void SelectingAfterAPlacementUsesTheUpdatedBoard()
        {
            _session.Select(0, 1);
            _session.Choose(Direction.West);
            _session.Choose(Direction.East);
            Assert.AreEqual(SelectOutcome.Selected, _session.Select(1, 1));
            Assert.IsTrue(_session.IsForced(Direction.West), "the new EW piece points at (1,1)");
            Assert.AreEqual(ChooseOutcome.Placed, _session.Choose(Direction.East), "west is forced, so east alone completes EW");
            Assert.AreEqual(PieceKey.EW, _board[1, 1]?.Key);
        }
    }
}
