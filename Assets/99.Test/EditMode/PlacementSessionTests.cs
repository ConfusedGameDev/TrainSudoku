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

        // ---- how each side reads to the player: the view marks the neighbours from these ----

        [Test]
        public void EachSideOfASelectionReadsAsForcedOpenOrBlocked()
        {
            // (1,0) sits on the top edge: north is a wall, the other three are empty neighbours.
            Assert.AreEqual(SelectOutcome.Selected, _session.Select(1, 0));
            Assert.AreEqual(SideMark.Blocked, _session.MarkOf(Direction.North), "the board edge is a wall");
            Assert.AreEqual(SideMark.Open, _session.MarkOf(Direction.East));
            Assert.AreEqual(SideMark.Open, _session.MarkOf(Direction.South));
            Assert.AreEqual(SideMark.Open, _session.MarkOf(Direction.West));

            // (0,1) faces the entrance tunnel, which the piece has to connect to.
            Assert.AreEqual(SelectOutcome.Selected, _session.Select(0, 1));
            Assert.AreEqual(SideMark.Forced, _session.MarkOf(Direction.West), "the tunnel forces the west side");
            Assert.AreEqual(SideMark.Open, _session.MarkOf(Direction.North));
        }

        [Test]
        public void TheFirstChoiceReadsAsChosenAndTheSidesItRulesOutAsBlocked()
        {
            _session.Select(0, 1);
            Assert.AreEqual(ChooseOutcome.Narrowed, _session.Choose(Direction.West));

            Assert.AreEqual(SideMark.Chosen, _session.MarkOf(Direction.West), "the connection already made");
            Assert.AreEqual(SideMark.Open, _session.MarkOf(Direction.East), "EW is still on offer");

            // A wall stays blocked whatever has been chosen.
            _session.Select(1, 0);
            _session.Choose(Direction.South);
            Assert.AreEqual(SideMark.Chosen, _session.MarkOf(Direction.South));
            Assert.AreEqual(SideMark.Blocked, _session.MarkOf(Direction.North));
        }

        [Test]
        public void TappingTheChosenSideAgainReleasesIt()
        {
            _session.Select(0, 1);
            Assert.AreEqual(ChooseOutcome.Narrowed, _session.Choose(Direction.West));
            Assert.AreEqual(SideMark.Chosen, _session.MarkOf(Direction.West));

            Assert.AreEqual(ChooseOutcome.Reverted, _session.Choose(Direction.West), "the same side again takes it back");
            Assert.IsNull(_session.First);
            Assert.IsTrue(_session.IsActive, "the cell stays selected");
            CollectionAssert.AreEquivalent(DirectionExtensions.All, _session.Available, "every side is on offer again");
            Assert.AreEqual(SideMark.Forced, _session.MarkOf(Direction.West), "the tunnel reads as forced once more");
            Assert.IsNull(_board[0, 1], "nothing was placed on the way there and back");
        }

        [Test]
        public void TheSelectedCellStillCancelsAfterAFirstChoice()
        {
            _session.Select(0, 1);
            _session.Choose(Direction.West);
            Assert.AreEqual(SelectOutcome.Cancelled, _session.Select(0, 1));
            Assert.IsFalse(_session.IsActive);
            Assert.IsNull(_session.First);
            Assert.IsEmpty(_session.Available);
            Assert.IsNull(_board[0, 1]);
        }

        [Test]
        public void EverySideIsBlockedWithNothingSelected()
        {
            foreach (var side in DirectionExtensions.All)
                Assert.AreEqual(SideMark.Blocked, _session.MarkOf(side), $"{side} with no selection");

            _session.Select(0, 1);
            _session.Cancel();
            foreach (var side in DirectionExtensions.All)
                Assert.AreEqual(SideMark.Blocked, _session.MarkOf(side), $"{side} after cancelling");
        }
    }
}
