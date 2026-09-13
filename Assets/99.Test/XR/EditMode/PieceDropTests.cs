using NUnit.Framework;
using TrainSudoku.Core;
using TrainSudoku.XR.Rules;

namespace TrainSudoku.XR.Tests
{
    /// <summary>Every row of XR-PRD 4.4, plus lift-equals-erase, fixed-piece refusal, the ghost and two hands at once.</summary>
    public class PieceDropTests
    {
        private const Hand Left = Hand.Left;
        private const Hand Right = Hand.Right;

        private static PieceDrop Corridor() => new PieceDrop(XRTestBoards.Corridor());

        private static PieceDrop CorridorWith(int x, int y, PieceKey key)
        {
            var drop = Corridor();
            Assert.IsTrue(drop.Board.TryPlace(x, y, key), $"Could not place {key} at ({x},{y}).");
            return drop;
        }

        private static void AssertOutcome(DropOutcome expected, DropResult result) =>
            Assert.AreEqual(expected, result.Outcome, result.ToString());

        private static void AssertCell(Board board, int x, int y, PieceKey key, bool isFixed = false) =>
            Assert.AreEqual(new Piece(key, isFixed), board[x, y], $"({x},{y})");

        private static void AssertEmpty(Board board, int x, int y) =>
            Assert.IsTrue(board.IsEmpty(x, y), $"({x},{y}) holds {board[x, y]}");

        private static void Take(PieceDrop drop, Hand hand, PieceKey key) =>
            AssertOutcome(DropOutcome.Taken, drop.GrabFromTray(hand, key));

        private static void Lift(PieceDrop drop, Hand hand, int x, int y) =>
            AssertOutcome(DropOutcome.Lifted, drop.GrabFromCell(hand, x, y));

        // Row 1: an empty cell where the key is legal.

        [Test]
        public void TrayPieceOnLegalEmptyCellIsPlaced()
        {
            var drop = Corridor();
            Take(drop, Right, PieceKey.EW);
            var result = drop.Release(Right, (1, 1), false);

            AssertOutcome(DropOutcome.Placed, result);
            Assert.IsTrue(result.BoardChanged);
            Assert.IsFalse(result.IllegalDrop);
            Assert.IsNull(result.Origin);
            AssertCell(drop.Board, 1, 1, PieceKey.EW);
            Assert.IsFalse(drop.IsHolding(Right));
        }

        // Row 2: an empty cell where the key is illegal.

        [Test]
        public void TrayPieceOnIllegalEmptyCellReturnsToTheTray()
        {
            var drop = Corridor();
            Take(drop, Right, PieceKey.NS);
            var result = drop.Release(Right, (0, 1), false);

            AssertOutcome(DropOutcome.Returned, result);
            Assert.IsTrue(result.IllegalDrop);
            Assert.IsFalse(result.BoardChanged);
            Assert.IsNull(result.Origin, "A tray piece returns to the tray.");
            Assert.AreEqual(0, drop.Board.PieceCount);
            Assert.IsFalse(drop.IsHolding(Right));
        }

        [Test]
        public void LiftedPieceOnIllegalCellIsReseatedInItsCell()
        {
            var drop = CorridorWith(1, 1, PieceKey.EW);
            Lift(drop, Right, 1, 1);
            var result = drop.Release(Right, (0, 0), false);

            AssertOutcome(DropOutcome.Returned, result);
            Assert.IsTrue(result.IllegalDrop);
            Assert.IsTrue(result.BoardChanged, "Re-seating the piece refills its cell.");
            Assert.AreEqual((1, 1), result.Origin);
            AssertCell(drop.Board, 1, 1, PieceKey.EW);
        }

        [Test]
        public void LiftedPieceWhoseCellNoLongerTakesItPuffs()
        {
            var drop = CorridorWith(1, 1, PieceKey.EW);
            Lift(drop, Right, 1, 1);
            // The other hand lays SE above the empty cell, so (1,1) must now connect north and EW no longer fits there.
            Take(drop, Left, PieceKey.SE);
            AssertOutcome(DropOutcome.Placed, drop.Release(Left, (1, 0), false));

            var result = drop.Release(Right, (0, 0), false);

            AssertOutcome(DropOutcome.Puffed, result);
            Assert.IsTrue(result.IllegalDrop);
            Assert.IsFalse(result.BoardChanged);
            AssertEmpty(drop.Board, 1, 1);
            AssertCell(drop.Board, 1, 0, PieceKey.SE);
        }

        // Row 3: a player piece, and the key is legal once that piece is gone.

        [Test]
        public void PieceOnReplaceablePlayerPieceReplacesIt()
        {
            var drop = CorridorWith(1, 1, PieceKey.NS);
            Take(drop, Right, PieceKey.EW);
            var result = drop.Release(Right, (1, 1), false);

            AssertOutcome(DropOutcome.Replaced, result);
            Assert.AreEqual(PieceKey.NS, result.ReplacedKey);
            Assert.IsTrue(result.BoardChanged);
            Assert.IsFalse(result.IllegalDrop);
            AssertCell(drop.Board, 1, 1, PieceKey.EW);
            Assert.AreEqual(1, drop.Board.PieceCount);
        }

        // Row 4: a player piece, and the key is still illegal once that piece is gone.

        [Test]
        public void PieceOnPlayerPieceThatStaysIllegalReturnsAndTheOldPieceStays()
        {
            var drop = CorridorWith(0, 1, PieceKey.EW);
            Take(drop, Right, PieceKey.NS);
            var result = drop.Release(Right, (0, 1), false);

            AssertOutcome(DropOutcome.Returned, result);
            Assert.IsTrue(result.IllegalDrop);
            Assert.IsNull(result.ReplacedKey);
            AssertCell(drop.Board, 0, 1, PieceKey.EW);
        }

        // Row 5: a fixed piece.

        [Test]
        public void PieceOnFixedPieceReturns()
        {
            var drop = new PieceDrop(XRTestBoards.PlanExample());
            Take(drop, Right, PieceKey.EW);
            var result = drop.Release(Right, (1, 2), false);

            AssertOutcome(DropOutcome.Returned, result);
            Assert.IsTrue(result.IllegalDrop);
            AssertCell(drop.Board, 1, 2, PieceKey.SW, isFixed: true);
        }

        // Row 6: off the platform, below the throw threshold.

        [Test]
        public void ReleasedOffThePlatformPuffs()
        {
            var drop = Corridor();
            Take(drop, Right, PieceKey.EW);
            var result = drop.Release(Right, null, false);

            AssertOutcome(DropOutcome.Puffed, result);
            Assert.IsFalse(result.IllegalDrop, "Letting go off the platform is a deliberate removal, not a mistake.");
            Assert.IsFalse(result.BoardChanged);
            Assert.AreEqual(0, drop.Board.PieceCount);

            Take(drop, Right, PieceKey.EW);
            AssertOutcome(DropOutcome.Puffed, drop.Release(Right, (3, 1), false));
        }

        [Test]
        public void LiftedPieceReleasedOffThePlatformLeavesItsCellEmpty()
        {
            var drop = CorridorWith(1, 1, PieceKey.EW);
            Lift(drop, Right, 1, 1);
            AssertOutcome(DropOutcome.Puffed, drop.Release(Right, null, false));
            Assert.AreEqual(0, drop.Board.PieceCount);
        }

        // Row 7: anywhere, above the throw threshold.

        [Test]
        public void ThrownPieceIsGoneEvenOverALegalCell()
        {
            var drop = Corridor();
            Take(drop, Right, PieceKey.EW);
            var result = drop.Release(Right, (1, 1), true);

            AssertOutcome(DropOutcome.Thrown, result);
            Assert.IsFalse(result.BoardChanged);
            Assert.IsFalse(result.IllegalDrop);
            Assert.AreEqual(0, drop.Board.PieceCount);
            Assert.IsFalse(drop.IsHolding(Right));
        }

        [Test]
        public void ThrowingALiftedPieceLeavesItsCellEmpty()
        {
            var drop = CorridorWith(1, 1, PieceKey.EW);
            Lift(drop, Right, 1, 1);
            AssertOutcome(DropOutcome.Thrown, drop.Release(Right, (1, 1), true));
            Assert.AreEqual(0, drop.Board.PieceCount);
        }

        // Row 8: a board piece on another empty cell where its key is legal.

        [Test]
        public void LiftedPieceOnAnotherLegalCellIsMoved()
        {
            var drop = CorridorWith(1, 1, PieceKey.EW);
            Lift(drop, Right, 1, 1);
            var result = drop.Release(Right, (0, 1), false);

            AssertOutcome(DropOutcome.Moved, result);
            Assert.AreEqual((1, 1), result.Origin);
            Assert.AreEqual((0, 1), result.Cell);
            AssertCell(drop.Board, 0, 1, PieceKey.EW);
            AssertEmpty(drop.Board, 1, 1);
        }

        [Test]
        public void LiftedPieceDroppedBackOnItsOwnCellIsPlaced()
        {
            var drop = CorridorWith(1, 1, PieceKey.EW);
            Lift(drop, Right, 1, 1);
            AssertOutcome(DropOutcome.Placed, drop.Release(Right, (1, 1), false));
            AssertCell(drop.Board, 1, 1, PieceKey.EW);
        }

        // Grabbing (4.2).

        [Test]
        public void LiftingAPieceErasesItAtOnce()
        {
            var drop = CorridorWith(1, 1, PieceKey.EW);
            var result = drop.GrabFromCell(Right, 1, 1);

            AssertOutcome(DropOutcome.Lifted, result);
            Assert.IsTrue(result.BoardChanged);
            Assert.AreEqual(PieceKey.EW, result.Key);
            Assert.AreEqual((1, 1), result.Cell);
            AssertEmpty(drop.Board, 1, 1);
            var held = drop.Held(Right);
            Assert.IsTrue(held.HasValue);
            Assert.AreEqual(PieceKey.EW, held.Value.Key);
            Assert.AreEqual((1, 1), held.Value.Origin);
            Assert.IsFalse(held.Value.FromTray);
        }

        [Test]
        public void FixedPieceRefusesTheGrab()
        {
            var drop = new PieceDrop(XRTestBoards.PlanExample());
            var result = drop.GrabFromCell(Right, 1, 2);

            AssertOutcome(DropOutcome.Refused, result);
            Assert.AreEqual(RefuseReason.FixedPiece, result.RefuseReason);
            Assert.AreEqual(PieceKey.SW, result.Key);
            Assert.IsFalse(result.BoardChanged);
            AssertCell(drop.Board, 1, 2, PieceKey.SW, isFixed: true);
            Assert.IsFalse(drop.IsHolding(Right));
            Take(drop, Right, PieceKey.EW);
        }

        [Test]
        public void EmptyAndOutsideCellsRefuseTheGrab()
        {
            var drop = Corridor();
            Assert.AreEqual(RefuseReason.EmptyCell, drop.GrabFromCell(Right, 1, 1).RefuseReason);
            Assert.AreEqual(RefuseReason.OutsideBoard, drop.GrabFromCell(Right, 3, 0).RefuseReason);
            Assert.AreEqual(RefuseReason.OutsideBoard, drop.GrabFromCell(Right, -1, 1).RefuseReason);
            Assert.IsFalse(drop.IsHolding(Right));
        }

        [Test]
        public void AHandHoldsOnePieceAtATime()
        {
            var drop = CorridorWith(1, 1, PieceKey.NS);
            Take(drop, Right, PieceKey.EW);

            Assert.AreEqual(RefuseReason.HandBusy, drop.GrabFromTray(Right, PieceKey.SE).RefuseReason);
            Assert.AreEqual(RefuseReason.HandBusy, drop.GrabFromCell(Right, 1, 1).RefuseReason);
            AssertCell(drop.Board, 1, 1, PieceKey.NS);
            Assert.AreEqual(PieceKey.EW, drop.Held(Right).Value.Key);
        }

        [Test]
        public void ReleasingAnEmptyHandIsRefused()
        {
            var drop = CorridorWith(1, 1, PieceKey.NS);
            var result = drop.Release(Left, (1, 1), false);

            AssertOutcome(DropOutcome.Refused, result);
            Assert.AreEqual(RefuseReason.NothingHeld, result.RefuseReason);
            AssertCell(drop.Board, 1, 1, PieceKey.NS);
        }

        // Two hands at once.

        [Test]
        public void BothHandsHoldAndPlaceIndependently()
        {
            var drop = Corridor();
            Take(drop, Left, PieceKey.EW);
            Take(drop, Right, PieceKey.EW);
            Assert.IsTrue(drop.IsHolding(Left));
            Assert.IsTrue(drop.IsHolding(Right));

            AssertOutcome(DropOutcome.Placed, drop.Release(Left, (0, 1), false));
            Assert.IsTrue(drop.IsHolding(Right), "Releasing one hand leaves the other holding.");
            AssertOutcome(DropOutcome.Placed, drop.Release(Right, (1, 1), false));

            AssertCell(drop.Board, 0, 1, PieceKey.EW);
            AssertCell(drop.Board, 1, 1, PieceKey.EW);
        }

        [Test]
        public void APieceWhoseCellTheOtherHandFilledPuffsOnAnIllegalDrop()
        {
            var drop = CorridorWith(1, 1, PieceKey.EW);
            Lift(drop, Right, 1, 1);
            Take(drop, Left, PieceKey.NS);
            AssertOutcome(DropOutcome.Placed, drop.Release(Left, (1, 1), false));

            var result = drop.Release(Right, (0, 0), false);

            AssertOutcome(DropOutcome.Puffed, result);
            Assert.IsTrue(result.IllegalDrop);
            AssertCell(drop.Board, 1, 1, PieceKey.NS);
        }

        // The ghost (4.3).

        [Test]
        public void GhostIsGreenWhereTheReleaseWouldLandAndRedWhereItWouldNot()
        {
            var drop = Corridor();
            Assert.AreEqual(GhostTint.None, drop.Hover(Right, (1, 1)), "An empty hand shows no ghost.");

            Take(drop, Right, PieceKey.NS);
            Assert.AreEqual(GhostTint.Legal, drop.Hover(Right, (1, 1)));
            Assert.AreEqual(GhostTint.Illegal, drop.Hover(Right, (0, 1)));
            Assert.AreEqual(GhostTint.None, drop.Hover(Right, null));
            Assert.AreEqual(GhostTint.None, drop.Hover(Right, (7, 7)));
            Assert.AreEqual(GhostTint.None, drop.Hover(Left, (1, 1)));
        }

        [Test]
        public void GhostIsGreenOverAReplaceablePieceAndRedOverAFixedOne()
        {
            var corridor = CorridorWith(1, 1, PieceKey.NS);
            Take(corridor, Right, PieceKey.EW);
            Assert.AreEqual(GhostTint.Legal, corridor.Hover(Right, (1, 1)));

            var plan = new PieceDrop(XRTestBoards.PlanExample());
            Take(plan, Right, PieceKey.SW);
            Assert.AreEqual(GhostTint.Illegal, plan.Hover(Right, (1, 2)));
        }

        [Test]
        public void HoveringNeverChangesTheBoard()
        {
            var drop = CorridorWith(1, 1, PieceKey.NS);
            Take(drop, Right, PieceKey.EW);
            foreach (var cell in new[] { (1, 1), (0, 1), (0, 0), (2, 2) })
                drop.Hover(Right, cell);

            AssertCell(drop.Board, 1, 1, PieceKey.NS);
            Assert.AreEqual(1, drop.Board.PieceCount);
            Assert.AreEqual(1, drop.Board.RowCount(1));
            Assert.IsTrue(drop.IsHolding(Right));
        }

        [Test]
        public void GhostAgreesWithTheReleaseOnEveryCellAndKey()
        {
            PieceDrop Setup()
            {
                var drop = Corridor();
                Assert.IsTrue(drop.Board.TryPlace(0, 1, PieceKey.EW));
                Assert.IsTrue(drop.Board.TryPlace(1, 0, PieceKey.SE));
                return drop;
            }

            foreach (var key in PieceKeys.All)
            for (var y = 0; y < 3; y++)
            for (var x = 0; x < 3; x++)
            {
                var drop = Setup();
                Take(drop, Right, key);
                var tint = drop.Hover(Right, (x, y));
                var outcome = drop.Release(Right, (x, y), false).Outcome;
                var landed = outcome == DropOutcome.Placed || outcome == DropOutcome.Moved || outcome == DropOutcome.Replaced;
                Assert.AreEqual(landed ? GhostTint.Legal : GhostTint.Illegal, tint, $"{key} at ({x},{y}) released as {outcome}");
            }
        }
    }
}
