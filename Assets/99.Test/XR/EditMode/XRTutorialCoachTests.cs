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
    /// Two rails are guided; after that the coach is silent until the player has stood idle for thirty seconds.
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

        private DropResult Take(PieceKey key) => Observe(_drop.GrabFromTray(Hand.Right, key));

        private DropResult Drop(int x, int y) => Observe(_drop.Release(Hand.Right, (x, y), false));

        /// <summary>The two rails the coach walks the player through.</summary>
        private void LayTheGuidedRails()
        {
            Lay(PieceKey.NS, 0, 1);
            Lay(PieceKey.NE, 0, 2);
        }

        // ---- The opening

        [Test]
        public void OpensLockedOnTheFirstRailWithItsSlotLit()
        {
            AssertGuide(XRGuideKind.Lay, 0, 1, PieceKey.NS, true);
            Assert.AreEqual(XRTutorialKeys.LayFirst, _coach.Key);
        }

        [Test]
        public void TheSecondRailIsShownTheSameWay()
        {
            Lay(PieceKey.NS, 0, 1);
            AssertGuide(XRGuideKind.Lay, 0, 2, PieceKey.NE, true);
            Assert.AreEqual(XRTutorialKeys.LayNext, _coach.Key, "row 1 turned green, but the clue line waits for the player's own rails");
        }

        [Test]
        public void AfterTwoRailsTheRingGoesAndTheBoardIsThePlayers()
        {
            LayTheGuidedRails();
            Assert.IsFalse(_coach.Guide.Active, _coach.Guide.ToString());
            Assert.AreEqual(XRTutorialKeys.Unlocked, _coach.Key);
            Assert.IsTrue(_coach.Admits(3, 3, PieceKey.EW));
        }

        [Test]
        public void TheHandOverLineGoesWithTheNextAction()
        {
            LayTheGuidedRails();
            Take(PieceKey.SW);
            Assert.IsNull(_coach.Key);
            Assert.IsFalse(_coach.Guide.Active);
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

        [Test]
        public void LiftingARailUnpromptedOpensTheBoardEarly()
        {
            Lay(PieceKey.NS, 0, 1);
            Lift(0, 1);
            Assert.IsFalse(_coach.Guide.Active);
            Assert.AreEqual(DropOutcome.Placed, Drop(0, 1).Outcome);
        }

        [Test]
        public void TheClockDoesNotRunWhileTheOpeningIsLocked()
        {
            Assert.IsFalse(_coach.Tick(XRTutorialCoach.IdleSeconds * 4));
            AssertGuide(XRGuideKind.Lay, 0, 1, PieceKey.NS, true);
            Assert.AreEqual(XRTutorialKeys.LayFirst, _coach.Key);
        }

        // ---- On their own

        [Test]
        public void OnceUnlockedNothingIsSteered()
        {
            LayTheGuidedRails();
            Assert.IsFalse(Lay(PieceKey.EW, 2, 2).Steered);
            Assert.IsFalse(_board.IsEmpty(2, 2));
        }

        [Test]
        public void AnOverfullLineGetsTheOverfullNote()
        {
            LayTheGuidedRails();
            // Row 0 asks for one rail and already holds the fixed SW: a second overfills it.
            Lay(PieceKey.EW, 2, 0);
            Assert.IsTrue(Overfull());
            Assert.AreEqual(XRTutorialKeys.NoteOverfull, _coach.Key);
            Assert.IsFalse(_coach.Guide.Active, "a note, not a ring");
        }

        // ---- The idle hint

        [Test]
        public void NothingIsPointedAtUntilThirtySecondsHavePassed()
        {
            LayTheGuidedRails();
            Assert.IsFalse(_coach.Tick(XRTutorialCoach.IdleSeconds - 0.1));
            Assert.IsFalse(_coach.Guide.Active);

            Assert.IsTrue(_coach.Tick(0.2), "the hint has just come up");
            AssertGuide(XRGuideKind.Lay, 1, 2, PieceKey.SW, false);
            Assert.AreEqual(XRTutorialKeys.Next, _coach.Key);
            Assert.IsTrue(_coach.Admits(3, 3, PieceKey.EW), "a hint points, it does not lock");
            Assert.IsFalse(_coach.Tick(XRTutorialCoach.IdleSeconds), "already showing");
        }

        [Test]
        public void AnyGrabOrReleaseStartsTheClockAgain()
        {
            LayTheGuidedRails();
            Assert.IsFalse(_coach.Tick(20));
            Take(PieceKey.EW);
            Assert.IsFalse(_coach.Tick(20), "the twenty seconds before the grab no longer count");
            Assert.IsTrue(_coach.Tick(10));
        }

        [Test]
        public void TheHintStaysUntilTheBoardChangesThenWaitsAgain()
        {
            LayTheGuidedRails();
            Assert.IsTrue(_coach.Tick(XRTutorialCoach.IdleSeconds));

            Take(PieceKey.SW);
            AssertGuide(XRGuideKind.Lay, 1, 2, PieceKey.SW, false);

            Assert.AreEqual(DropOutcome.Placed, Drop(1, 2).Outcome);
            Assert.IsFalse(_coach.Guide.Active, "the rail is down: the ring goes");

            Assert.IsFalse(_coach.Tick(XRTutorialCoach.IdleSeconds - 1));
            Assert.IsTrue(_coach.Tick(1));
            Assert.AreEqual((2, 3), (_coach.Guide.X, _coach.Guide.Y));
        }

        [Test]
        public void ARailThatIsNotOnTheLineIsPointedAtFirst()
        {
            LayTheGuidedRails();
            Lay(PieceKey.EW, 2, 0);
            Assert.IsTrue(_coach.Tick(XRTutorialCoach.IdleSeconds));
            AssertGuide(XRGuideKind.Erase, 2, 0, null, false);
            Assert.AreEqual(XRTutorialKeys.HintLift, _coach.Key, "the hint replaces the note that stood unanswered");

            Assert.AreEqual(DropOutcome.Lifted, Lift(2, 0).Outcome);
            Assert.IsFalse(_coach.Guide.Active);
            Throw();
            Assert.IsTrue(_coach.Tick(XRTutorialCoach.IdleSeconds));
            AssertGuide(XRGuideKind.Lay, 1, 2, PieceKey.SW, false);
        }

        [Test]
        public void FollowingTheHintsToTheEndWinsTheBoardAndTheClueLineShowsOnce()
        {
            LayTheGuidedRails();
            var clueLines = 0;
            for (var guard = 0; guard < 40 && !WinChecker.Evaluate(_board).IsWin; guard++)
            {
                Assert.IsTrue(_coach.Tick(XRTutorialCoach.IdleSeconds));
                var guide = _coach.Guide;
                Assert.AreEqual(XRGuideKind.Lay, guide.Kind, guide.ToString());
                Assert.IsTrue(guide.Slot.HasValue);
                Assert.AreEqual(DropOutcome.Placed, Lay(guide.Slot.Value, guide.X, guide.Y).Outcome);
                if (_coach.Key == XRTutorialKeys.Clue) clueLines++;
            }

            Assert.IsTrue(WinChecker.Evaluate(_board).IsWin);
            Assert.AreEqual(1, clueLines);
            _coach.Complete();
            Assert.IsFalse(_coach.Guide.Active);
            Assert.IsNull(_coach.Key);
            Assert.IsFalse(_coach.Tick(XRTutorialCoach.IdleSeconds));
        }

        // ---- Coming back, and not teaching

        [Test]
        public void AHalfBuiltBoardComesBackOpenAndQuietButStillHints()
        {
            Assert.IsTrue(_board.TryPlace(0, 1, PieceKey.NS));
            _coach.Begin(_level, _board, true);
            Assert.IsFalse(_coach.Guide.Active);
            Assert.IsNull(_coach.Key);
            Assert.IsTrue(_coach.Admits(3, 3, PieceKey.EW));

            Assert.IsTrue(_coach.Tick(XRTutorialCoach.IdleSeconds));
            AssertGuide(XRGuideKind.Lay, 0, 2, PieceKey.NE, false);
        }

        [Test]
        public void ALevelThatDoesNotTeachIsSilentAdmitsEverythingAndNeverHints()
        {
            _coach.Begin(_level, _board, false);
            Assert.IsFalse(_coach.IsActive);
            Assert.IsNull(_coach.Key);
            Assert.IsTrue(_coach.Admits(2, 2, PieceKey.EW));
            Assert.IsFalse(_coach.Tick(XRTutorialCoach.IdleSeconds * 10));
            Assert.IsFalse(_coach.Guide.Active);
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
            Assert.IsFalse(_coach.Tick(XRTutorialCoach.IdleSeconds));
        }

        [Test]
        public void TheShippedTutorialStationIsTwoRailsThenHintsToTheEnd()
        {
            _level = XRTestBoards.KettleburnLevel();
            _board = new Board(_level);
            _drop = new PieceDrop(_board);
            _coach.Begin(_level, _board, true);
            _drop.Gate = _coach.Admits;
            var start = WinChecker.Evaluate(_board);
            _rows = start.RowSatisfied.ToArray();
            _columns = start.ColumnSatisfied.ToArray();

            AssertGuide(XRGuideKind.Lay, 1, 1, PieceKey.EW, true);
            Lay(PieceKey.EW, 1, 1);
            AssertGuide(XRGuideKind.Lay, 2, 1, PieceKey.EW, true);
            Lay(PieceKey.EW, 2, 1);
            Assert.IsFalse(_coach.Guide.Active);

            for (var guard = 0; guard < 10 && !WinChecker.Evaluate(_board).IsWin; guard++)
            {
                Assert.IsTrue(_coach.Tick(XRTutorialCoach.IdleSeconds));
                var guide = _coach.Guide;
                Assert.AreEqual(XRGuideKind.Lay, guide.Kind, guide.ToString());
                Assert.AreEqual(DropOutcome.Placed, Lay(guide.Slot.Value, guide.X, guide.Y).Outcome);
            }

            Assert.IsTrue(WinChecker.Evaluate(_board).IsWin);
            Assert.AreEqual(7, _board.PieceCount - _level.FixedPieces.Count);
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
