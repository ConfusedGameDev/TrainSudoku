using System.Linq;
using NUnit.Framework;
using TrainSudoku.Core;
using TrainSudoku.XR.Rules;

namespace TrainSudoku.XR.Tests
{
    /// <summary>
    /// The XR tutorial coach (XR-PRD 7) on Ashgate, driven the way the game drives it: every grab and release goes
    /// through <see cref="PieceDrop"/> with the coach as its gate, and the coach observes each result with the board's
    /// overfull and line-satisfied state after it. The route is (0,1) NS, (0,2) NE, (1,2) SW, then (2,3) to (5,5).
    /// </summary>
    public class XRTutorialCoachTests
    {
        private LevelData _level;
        private Board _board;
        private PieceDrop _drop;
        private XRTutorialCoach _coach;
        private bool[] _rows;
        private bool[] _columns;

        [SetUp]
        public void SetUp()
        {
            _level = XRTestBoards.AshgateLevel();
            _board = new Board(_level);
            _drop = new PieceDrop(_board);
            _coach = new XRTutorialCoach();
            _coach.Begin(_level, _board, true);
            _drop.Gate = _coach.Admits;
            var result = WinChecker.Evaluate(_board);
            _rows = result.RowSatisfied.ToArray();
            _columns = result.ColumnSatisfied.ToArray();
        }

        private DropResult Lay(PieceKey key, int x, int y)
        {
            _drop.GrabFromTray(Hand.Right, key);
            return Observe(_drop.Release(Hand.Right, (x, y), false));
        }

        private DropResult Lift(int x, int y) => Observe(_drop.GrabFromCell(Hand.Right, x, y));

        private DropResult Throw() => Observe(_drop.Release(Hand.Right, null, true));

        private DropResult Observe(DropResult result)
        {
            var win = WinChecker.Evaluate(_board);
            var satisfied = false;
            for (var i = 0; i < _rows.Length; i++)
            {
                satisfied |= win.RowSatisfied[i] && !_rows[i];
                _rows[i] = win.RowSatisfied[i];
            }

            for (var i = 0; i < _columns.Length; i++)
            {
                satisfied |= win.ColumnSatisfied[i] && !_columns[i];
                _columns[i] = win.ColumnSatisfied[i];
            }

            _coach.Observe(result, Overfull(), satisfied);
            return result;
        }

        private bool Overfull()
        {
            for (var y = 0; y < _board.Height; y++)
                if (_board.RowCount(y) > _level.RowClues[y]) return true;
            for (var x = 0; x < _board.Width; x++)
                if (_board.ColumnCount(x) > _level.ColumnClues[x]) return true;
            return false;
        }

        private void AssertGuide(XRGuideKind kind, int x, int y, PieceKey? slot, bool locked)
        {
            var guide = _coach.Guide;
            Assert.AreEqual(kind, guide.Kind, guide.ToString());
            Assert.AreEqual((x, y), (guide.X, guide.Y), guide.ToString());
            Assert.AreEqual(slot, guide.Slot, guide.ToString());
            Assert.AreEqual(locked, guide.Locked, guide.ToString());
        }

        /// <summary>Rails 1 to 3, with the adjacency bounce on rail 2.</summary>
        private void LayTheGuidedRails()
        {
            Lay(PieceKey.NS, 0, 1);
            Lay(PieceKey.EW, 0, 2);
            Lay(PieceKey.NE, 0, 2);
            Lay(PieceKey.SW, 1, 2);
        }

        // ---- The opening

        [Test]
        public void OpensLockedOnTheFirstRailWithItsSlotLit()
        {
            AssertGuide(XRGuideKind.Lay, 0, 1, PieceKey.NS, true);
            Assert.AreEqual(XRTutorialKeys.LayFirst, _coach.Key);
        }

        [Test]
        public void TheSecondRailAsksForAPieceThatDoesNotJoinAndItFliesBack()
        {
            Lay(PieceKey.NS, 0, 1);
            AssertGuide(XRGuideKind.Adjacency, 0, 2, PieceKey.EW, true);
            Assert.AreEqual(XRTutorialKeys.Adjacency, _coach.Key);
            Assert.AreEqual(GhostTint.Illegal, HoverWith(PieceKey.EW, 0, 2), "the ghost goes red over the target");

            var result = Lay(PieceKey.EW, 0, 2);
            Assert.AreEqual(DropOutcome.Returned, result.Outcome);
            Assert.IsTrue(result.IllegalDrop, "the rules sent it back, so the error cue plays");
            Assert.IsFalse(result.Steered);

            AssertGuide(XRGuideKind.Lay, 0, 2, PieceKey.NE, true);
            Assert.AreEqual(XRTutorialKeys.AdjacencyDone, _coach.Key);
        }

        [Test]
        public void LayingTheRightPieceDuringTheAdjacencyLessonSkipsIt()
        {
            Lay(PieceKey.NS, 0, 1);
            var result = Lay(PieceKey.NE, 0, 2);
            Assert.AreEqual(DropOutcome.Placed, result.Outcome);
            AssertGuide(XRGuideKind.Lay, 1, 2, PieceKey.SW, true);
            Assert.AreNotEqual(XRTutorialKeys.AdjacencyDone, _coach.Key);
        }

        [Test]
        public void TheSlotIsLitForTheFirstThreeRailsOnly()
        {
            Lay(PieceKey.NS, 0, 1);
            Lay(PieceKey.EW, 0, 2);
            Assert.IsTrue(_coach.Guide.Slot.HasValue, "rail 2");
            Lay(PieceKey.NE, 0, 2);
            Assert.IsTrue(_coach.Guide.Slot.HasValue, "rail 3");
            Lay(PieceKey.SW, 1, 2);
            Assert.IsFalse(_coach.Guide.Slot.HasValue, "from rail 4 the player finds the piece");
        }

        // ---- The lock

        [Test]
        public void ALegalDropOnAnotherCellIsSteeredBackWithANote()
        {
            Assert.IsTrue(Legality.IsLegal(_board, 2, 2, PieceKey.EW), "precondition: the drop is legal by the rules");
            Assert.AreEqual(GhostTint.Illegal, HoverWith(PieceKey.EW, 2, 2), "the ghost agrees with the gate");

            var result = Lay(PieceKey.EW, 2, 2);
            Assert.AreEqual(DropOutcome.Returned, result.Outcome);
            Assert.IsTrue(result.Steered);
            Assert.IsFalse(result.IllegalDrop, "a steered drop is not an error");
            Assert.IsTrue(_board.IsEmpty(2, 2));
            Assert.AreEqual(XRTutorialKeys.NoteSteered, _coach.Key);
            AssertGuide(XRGuideKind.Lay, 0, 1, PieceKey.NS, true);
        }

        [Test]
        public void AnotherLegalPieceOnTheTargetIsSteeredBackAskingForTheLitOne()
        {
            Assert.IsTrue(Legality.IsLegal(_board, 0, 1, PieceKey.NE));
            var result = Lay(PieceKey.NE, 0, 1);
            Assert.IsTrue(result.Steered);
            Assert.AreEqual(XRTutorialKeys.NoteWrongPiece, _coach.Key);
        }

        [Test]
        public void ANoteClearsOnTheNextAction()
        {
            Lay(PieceKey.EW, 2, 2);
            Assert.AreEqual(XRTutorialKeys.NoteSteered, _coach.Key);
            Lay(PieceKey.NS, 0, 1);
            Assert.AreNotEqual(XRTutorialKeys.NoteSteered, _coach.Key);
        }

        [Test]
        public void GrabbingAFixedPieceSaysItWasLaidBeforeYou()
        {
            var result = Lift(0, 0);
            Assert.AreEqual(RefuseReason.FixedPiece, result.RefuseReason);
            Assert.AreEqual(XRTutorialKeys.NoteFixed, _coach.Key);
        }

        // ---- The staged overfill and the erase lesson

        [Test]
        public void AfterThreeRailsTheMistakeCellIsLitAloneOffTheRoute()
        {
            LayTheGuidedRails();
            AssertGuide(XRGuideKind.Mistake, 1, 1, null, true);
            Assert.AreEqual(XRTutorialKeys.Mistake, _coach.Key);
            Assert.AreEqual(1, Legality.LegalKeys(_board, 1, 1).Count, "one piece fits, so the player can find it");
        }

        [Test]
        public void LayingTheMistakeOverfillsALineThenErasingAndThrowingItUnlocks()
        {
            LayTheGuidedRails();
            var key = Legality.LegalKeys(_board, 1, 1)[0];
            Lay(key, 1, 1);
            Assert.IsTrue(Overfull(), "the clue turns red");
            AssertGuide(XRGuideKind.Erase, 1, 1, null, true);
            Assert.AreEqual(XRTutorialKeys.Erase, _coach.Key, "the erase line, not the overfull note");

            Assert.AreEqual(DropOutcome.Lifted, Lift(1, 1).Outcome);
            Assert.AreEqual(XRGuideKind.Discard, _coach.Guide.Kind);
            Assert.AreEqual(XRTutorialKeys.Discard, _coach.Key);

            Throw();
            AssertGuide(XRGuideKind.Lay, 2, 3, null, false);
            Assert.AreEqual(XRTutorialKeys.Unlocked, _coach.Key);
        }

        [Test]
        public void ARailOnTheMistakeCellCannotBeCoveredWhileItWaitsToBeLifted()
        {
            LayTheGuidedRails();
            Lay(Legality.LegalKeys(_board, 1, 1)[0], 1, 1);
            Assert.IsTrue(Lay(PieceKey.EW, 2, 3).Steered);
            Assert.AreEqual(XRGuideKind.Erase, _coach.Guide.Kind);
        }

        [Test]
        public void LiftingARailUnpromptedSkipsTheDetour()
        {
            Lay(PieceKey.NS, 0, 1);
            Lift(0, 1);
            Assert.IsFalse(_coach.Guide.Locked);
            Assert.AreNotEqual(XRGuideKind.Discard, _coach.Guide.Kind, "only the mistake rail is asked to be thrown away");
            Assert.AreEqual(DropOutcome.Placed, Observe(_drop.Release(Hand.Right, (0, 1), false)).Outcome);
        }

        [Test]
        public void OnceUnlockedNothingIsSteered()
        {
            Lay(PieceKey.NS, 0, 1);
            Lift(0, 1);
            Throw();
            Assert.IsFalse(Lay(PieceKey.EW, 2, 2).Steered);
            Assert.IsFalse(_board.IsEmpty(2, 2));
        }

        [Test]
        public void AnOverfullLineOutsideTheLessonGetsTheOverfullNote()
        {
            Lay(PieceKey.NS, 0, 1);
            Lift(0, 1);
            Throw();
            // Row 0 asks for one rail and already holds the fixed SW: a second overfills it.
            Lay(PieceKey.EW, 2, 0);
            Assert.IsTrue(Overfull());
            Assert.AreEqual(XRTutorialKeys.NoteOverfull, _coach.Key);
        }

        // ---- The rest of the walk

        [Test]
        public void TheClueLineWaitsForTheLessonsAndShowsOnce()
        {
            // Row 1 asks for one rail and turns green with the first, but the adjacency lesson and its answer outrank it.
            Lay(PieceKey.NS, 0, 1);
            Assert.AreEqual(XRTutorialKeys.Adjacency, _coach.Key);
            Lay(PieceKey.EW, 0, 2);
            Assert.AreEqual(XRTutorialKeys.AdjacencyDone, _coach.Key);
            Lay(PieceKey.NE, 0, 2);
            Assert.AreEqual(XRTutorialKeys.Clue, _coach.Key);
            Lay(PieceKey.SW, 1, 2);
            Assert.AreEqual(XRTutorialKeys.Mistake, _coach.Key);
        }

        [Test]
        public void FollowingTheGuideToTheEndWinsTheBoard()
        {
            LayTheGuidedRails();
            Lay(Legality.LegalKeys(_board, 1, 1)[0], 1, 1);
            Lift(1, 1);
            Throw();

            Assert.IsTrue(Solver.TrySolve(_level, out var solution));
            for (var guard = 0; guard < 40 && _coach.Guide.Active; guard++)
            {
                var guide = _coach.Guide;
                Assert.AreEqual(XRGuideKind.Lay, guide.Kind);
                Assert.IsTrue(solution[guide.X, guide.Y] is Piece piece);
                Assert.AreEqual(DropOutcome.Placed, Lay(((Piece)solution[guide.X, guide.Y]).Key, guide.X, guide.Y).Outcome);
            }

            Assert.IsTrue(WinChecker.Evaluate(_board).IsWin);
            _coach.Complete();
            Assert.IsFalse(_coach.Guide.Active);
            Assert.IsNull(_coach.Key);
        }

        [Test]
        public void AHalfBuiltBoardComesBackUnlocked()
        {
            Assert.IsTrue(_board.TryPlace(0, 1, PieceKey.NS));
            _coach.Begin(_level, _board, true);
            AssertGuide(XRGuideKind.Lay, 0, 2, null, false);
            Assert.IsTrue(_coach.Admits(3, 3, PieceKey.EW));
        }

        [Test]
        public void ALevelThatDoesNotTeachIsSilentAndAdmitsEverything()
        {
            _coach.Begin(_level, _board, false);
            Assert.IsFalse(_coach.IsActive);
            Assert.IsNull(_coach.Key);
            Assert.IsTrue(_coach.Admits(2, 2, PieceKey.EW));
        }

        [Test]
        public void AnUnsolvableLevelLeavesTheCoachInert()
        {
            var level = XRTestBoards.AshgateLevel();
            level.RowClues[0] = 6;
            var board = new Board(level);
            _coach.Begin(level, board, true);
            Assert.IsFalse(_coach.IsActive);
            Assert.IsTrue(_coach.Admits(2, 2, PieceKey.EW));
        }

        private GhostTint HoverWith(PieceKey key, int x, int y)
        {
            _drop.GrabFromTray(Hand.Left, key);
            var tint = _drop.Hover(Hand.Left, (x, y));
            _drop.Release(Hand.Left, null, false);   // off the platform: puffs, the board is untouched
            return tint;
        }
    }
}
