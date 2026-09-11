using System.Collections.Generic;
using NUnit.Framework;
using TrainSudoku.Core;
using TrainSudoku.Game;
using UnityEditor;

namespace TrainSudoku.Tests
{
    /// <summary>
    /// The tutorial walkthrough (M21) driven against the board it actually ships on. These are less unit tests than
    /// a transcript: the coach's whole job is to be right at every step of one specific journey, and the only way to
    /// know it is to take that journey.
    /// </summary>
    public class TutorialCoachTests
    {
        private const string TutorialPath = "Assets/03.Data/Levels/simple.asset";

        private LevelData _level;
        private Board _board;
        private PlacementSession _session;
        private TutorialCoach _coach;
        private bool _lineSatisfied;
        private bool[] _rowWas;
        private bool[] _columnWas;

        [SetUp]
        public void LoadTheTutorialStation()
        {
            var definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(TutorialPath);
            Assert.IsNotNull(definition, $"Missing {TutorialPath}");
            Assert.IsTrue(definition.IsTutorial, "the tutorial station has lost its flag");

            _level = definition.ToLevelData();
            _board = new Board(_level);
            _session = new PlacementSession(_board);
            _coach = new TutorialCoach();
            _coach.Begin(_level, _board, true);

            // The board view colours the clues once on load without announcing anything, so a line already
            // satisfied by the fixed pieces is green from the start and never raises the event.
            var start = WinChecker.Evaluate(_board);
            _rowWas = start.RowSatisfied;
            _columnWas = start.ColumnSatisfied;
            _lineSatisfied = false;
        }

        // ---- driving the board the way the player's taps would ----

        /// <summary>Taps a cell, feeding the coach whatever the session made of it.</summary>
        private void Tap(int x, int y)
        {
            var before = _board.PieceCount;
            var outcome = _session.Select(x, y);

            // The validator runs before the action is raised, which is the whole reason the coach is told about a
            // satisfied line through the same pulse rather than separately.
            if (_board.PieceCount != before) NoteSatisfiedLines();

            switch (outcome)
            {
                case SelectOutcome.AutoPlaced: Pulse(BoardAction.AutoPlaced); break;
                case SelectOutcome.Selected: Pulse(BoardAction.Selected); break;
                case SelectOutcome.Cancelled: Pulse(BoardAction.Deselected); break;
            }
        }

        /// <summary>Taps the marker or neighbour on one side of the selected cell.</summary>
        private void TapSide(Direction side)
        {
            var before = _board.PieceCount;
            var outcome = _session.Choose(side);
            if (_board.PieceCount != before) NoteSatisfiedLines();

            switch (outcome)
            {
                case ChooseOutcome.Placed: Pulse(BoardAction.Connected); break;
                case ChooseOutcome.Narrowed: Pulse(BoardAction.Narrowed); break;
                case ChooseOutcome.Reverted: Pulse(BoardAction.Selected); break;
            }
        }

        private void Hold(int x, int y)
        {
            Assert.IsTrue(_board.TryErase(x, y), $"({x},{y}) had nothing to lift");
            _session.Cancel();
            Pulse(BoardAction.Erased);
        }

        /// <summary>
        /// What the board view does before it raises the action: run the validator and remember any line that has
        /// just <i>turned</i> green. A transition, not a state — a line that was already satisfied announces nothing.
        /// </summary>
        private void NoteSatisfiedLines()
        {
            var result = WinChecker.Evaluate(_board);
            for (var y = 0; y < _board.Height; y++)
                _lineSatisfied |= result.RowSatisfied[y] && !_rowWas[y];
            for (var x = 0; x < _board.Width; x++)
                _lineSatisfied |= result.ColumnSatisfied[x] && !_columnWas[x];

            _rowWas = result.RowSatisfied;
            _columnWas = result.ColumnSatisfied;
        }

        private void Pulse(BoardAction action)
        {
            var selected = _session.IsActive ? (_session.X, _session.Y) : ((int X, int Y)?)null;
            var chosen = _session.IsActive ? _session.First : null;
            _coach.Observe(new BoardPulse(action, selected, chosen, Overfull(), _lineSatisfied));
            _lineSatisfied = false;
        }

        private bool Overfull()
        {
            for (var y = 0; y < _board.Height; y++)
                if (_board.RowCount(y) > _level.RowClues[y]) return true;
            for (var x = 0; x < _board.Width; x++)
                if (_board.ColumnCount(x) > _level.ColumnClues[x]) return true;
            return false;
        }

        private void AssertGuide(int x, int y, GuideKind kind, Direction? side = null)
        {
            var guide = _coach.Guide;
            Assert.IsTrue(guide.Active, "the coach has stopped pointing at anything");
            Assert.AreEqual((x, y), (guide.X, guide.Y), $"pointing at ({guide.X},{guide.Y}), not ({x},{y})");
            Assert.AreEqual(kind, guide.Kind);
            Assert.AreEqual(side, guide.Side);
        }

        /// <summary>Plays the locked opening: three rails, then the staged mistake and lifting it off.</summary>
        private void WalkTheOpening()
        {
            Tap(0, 1); TapSide(Direction.South);   // NS — row 1 reaches its clue of 1
            Tap(0, 2); TapSide(Direction.East);    // NE — column 0 reaches its clue of 3
            Tap(1, 2);                             // SW, the rail that lays itself

            var mistake = _coach.Guide;
            Tap(mistake.X, mistake.Y);
            Hold(mistake.X, mistake.Y);
        }

        // ---- the tests ----

        [Test]
        public void ItOpensPointingAtTheFirstRailWithTheBoardLocked()
        {
            AssertGuide(0, 1, GuideKind.Select);
            Assert.IsTrue(_coach.Guide.Locked);
            Assert.AreEqual(TutorialKeys.SelectFirst, _coach.Key);
        }

        [Test]
        public void SelectingTheFirstCellPointsAtTheSideThatFinishesIt()
        {
            Tap(0, 1);

            // South, not North: taking North would leave two partners and need a third tap, and the coach is
            // required never to ask for more than two.
            AssertGuide(0, 1, GuideKind.Side, Direction.South);
            Assert.AreEqual(TutorialKeys.SideFirst, _coach.Key);

            TapSide(Direction.South);
            Assert.AreEqual(PieceKey.NS, _board[0, 1].Value.Key);
        }

        [Test]
        public void TakingTheForcedSideFirstIsStillWalkedToTheRightPiece()
        {
            Tap(0, 1);
            TapSide(Direction.North);   // the marked, forced side — an inviting wrong first move

            AssertGuide(0, 1, GuideKind.Side, Direction.South);
            Assert.AreEqual(TutorialKeys.SideSecond, _coach.Key);

            TapSide(Direction.South);
            Assert.AreEqual(PieceKey.NS, _board[0, 1].Value.Key);
        }

        [Test]
        public void AClueGoingGreenIsWorthOneLine()
        {
            // Row 1 asks for a single rail, so the player's very first one satisfies it — which is the earliest
            // the numbers can be explained with something on screen to point at.
            Tap(0, 1); TapSide(Direction.South);

            Assert.AreEqual(1, _board.RowCount(1));
            Assert.AreEqual(TutorialKeys.Clue, _coach.Key);

            // And it is retired by the next rail rather than lingering.
            Tap(0, 2); TapSide(Direction.East);
            Assert.AreNotEqual(TutorialKeys.Clue, _coach.Key);
        }

        [Test]
        public void TheCellThatOnlyOnePieceFitsGetsItsOwnLesson()
        {
            Tap(0, 1); TapSide(Direction.South);
            Tap(0, 2); TapSide(Direction.East);

            AssertGuide(1, 2, GuideKind.Select);
            Assert.AreEqual(TutorialKeys.SelectAuto, _coach.Key);
            Assert.AreEqual(1, Legality.LegalKeys(_board, 1, 2).Count, "the lesson only holds if one key is legal");
        }

        /// <summary>
        /// After three rails the coach stages a mistake rather than asking the player to undo good work: the rail
        /// it points at is off the solution, lays itself in one tap, and pushes a line past its clue — so the
        /// player watches a number turn red and is then shown the way out of it.
        /// </summary>
        [Test]
        public void AfterThreeRailsItStagesAMistakeToEraseRatherThanUndoingGoodWork()
        {
            Tap(0, 1); TapSide(Direction.South);
            Tap(0, 2); TapSide(Direction.East);
            Tap(1, 2);

            var mistake = _coach.Guide;
            Assert.AreEqual(GuideKind.Mistake, mistake.Kind);
            Assert.AreEqual(TutorialKeys.Mistake, _coach.Key);
            Assert.IsTrue(mistake.Locked);
            Assert.IsTrue(_board.IsEmpty(mistake.X, mistake.Y));
            Assert.AreEqual(1, Legality.LegalKeys(_board, mistake.X, mistake.Y).Count,
                "the staged mistake has to go down in one tap, like the rail before it");

            Tap(mistake.X, mistake.Y);
            Assert.IsFalse(_board.IsEmpty(mistake.X, mistake.Y));
            Assert.IsTrue(Overfull(), "the mistake has to turn a number red, or there is nothing to notice");

            AssertGuide(mistake.X, mistake.Y, GuideKind.Erase);
            Assert.AreEqual(TutorialKeys.Erase, _coach.Key, "the erase line outranks the overfull note here");

            Hold(mistake.X, mistake.Y);
            Assert.IsFalse(Overfull(), "lifting it puts the board back as it was");
            Assert.IsFalse(_coach.Guide.Locked, "the board is the player's once the erase lesson is done");
            Assert.AreEqual(TutorialKeys.Unlocked, _coach.Key);
        }

        /// <summary>The staged mistake must never be a rail the player will actually need.</summary>
        [Test]
        public void TheStagedMistakeIsNotOnTheSolution()
        {
            Tap(0, 1); TapSide(Direction.South);
            Tap(0, 2); TapSide(Direction.East);
            Tap(1, 2);

            var mistake = _coach.Guide;
            Assert.IsTrue(Solver.TrySolve(_level, out var solution));
            Assert.IsFalse(solution[mistake.X, mistake.Y].HasValue,
                $"({mistake.X},{mistake.Y}) carries a rail in the solution, so it is not a mistake at all");
        }

        [Test]
        public void LiftingAPieceBeforeBeingAskedSkipsTheDetour()
        {
            Tap(0, 1); TapSide(Direction.South);
            Hold(0, 1);

            Assert.IsFalse(_coach.Guide.Locked, "a player who has already lifted a rail does not need telling how");
            AssertGuide(0, 1, GuideKind.Select);
        }

        [Test]
        public void AfterTheOpeningItPointsAtEveryRemainingRailInOrder()
        {
            WalkTheOpening();
            Assert.IsFalse(_coach.Guide.Locked);

            var expected = new[]
            {
                (2, 3), (3, 3), (2, 4), (1, 4), (1, 5), (2, 5), (3, 5), (4, 5), (5, 5),
            };

            var first = true;
            foreach (var (x, y) in expected)
            {
                AssertGuide(x, y, GuideKind.Select);

                // The line that closes the locked opening is said over the first free rail, not in place of it.
                Assert.AreEqual(first ? TutorialKeys.Unlocked : TutorialKeys.SelectNext, _coach.Key);
                first = false;

                Tap(x, y);
                if (_board.IsEmpty(x, y))
                {
                    // A cell with a choice: the coach names the side, then the piece lands.
                    Assert.AreEqual(GuideKind.Side, _coach.Guide.Kind);
                    Assert.AreEqual(TutorialKeys.SideNext, _coach.Key);
                    TapSide(_coach.Guide.Side.Value);
                }

                Assert.IsFalse(_board.IsEmpty(x, y), $"({x},{y}) should be laid by now");
            }

            Assert.IsTrue(WinChecker.Evaluate(_board).IsWin, "following the coach must finish the level");
            Assert.IsFalse(_coach.Guide.Active, "with the board full there is nothing left to point at");

            _coach.Complete();
            Assert.IsNull(_coach.Key);
            Assert.IsFalse(_coach.IsActive);
        }

        [Test]
        public void ANonTutorialLevelPointsAtNothingAndStaysSilent()
        {
            var quiet = new TutorialCoach();
            quiet.Begin(_level, _board, false);

            Assert.IsFalse(quiet.Guide.Active);
            Assert.IsNull(quiet.Key);

            quiet.Observe(new BoardPulse(BoardAction.Selected, (0, 1)));
            Assert.IsNull(quiet.Key, "an inactive coach must stay silent whatever the board does");
        }

        [Test]
        public void ALevelThatWillNotSolveLeavesTheCoachInertRatherThanThrowing()
        {
            // A board whose clues cannot be met: the solver finds nothing, and a tutorial that cannot be walked
            // must not take the level down with it.
            var broken = new LevelData(3, 3) { Name = "Broken" };
            for (var i = 0; i < 3; i++) broken.RowClues[i] = 3;
            broken.Entrance = new Tunnel(Direction.West, 0);
            broken.Exit = new Tunnel(Direction.East, 0);

            var coach = new TutorialCoach();
            Assert.DoesNotThrow(() => coach.Begin(broken, new Board(broken), true));
            Assert.IsFalse(coach.Guide.Active);
            Assert.IsNull(coach.Key);
        }

        [Test]
        public void ARefusedTapSaysSoAndTheNextActionClearsIt()
        {
            _coach.Observe(new BoardPulse(BoardAction.Refused));
            Assert.AreEqual(TutorialKeys.Refused, _coach.Key);
            AssertGuide(0, 1, GuideKind.Select);

            Tap(0, 1);
            Assert.AreEqual(TutorialKeys.SideFirst, _coach.Key, "the next action acknowledges the note");
        }

        [Test]
        public void HoldingAFixedPieceExplainsWhyNothingHappened()
        {
            _coach.Observe(new BoardPulse(BoardAction.EraseRefused));
            Assert.AreEqual(TutorialKeys.NoteFixed, _coach.Key);
        }

        [Test]
        public void AnOverfullLineOutranksEveryOtherNote()
        {
            _coach.Observe(new BoardPulse(BoardAction.EraseRefused, overfull: true));
            Assert.AreEqual(TutorialKeys.NoteOverfull, _coach.Key);
        }

        [Test]
        public void AHalfBuiltBoardComesBackUnlocked()
        {
            var resumed = new Board(_level);
            resumed.SetUnchecked(0, 1, new Piece(PieceKey.NS, false));

            var coach = new TutorialCoach();
            coach.Begin(_level, resumed, true);

            Assert.IsFalse(coach.Guide.Locked, "re-locking the board under a returning player would be a punishment");
            Assert.AreEqual((0, 2), (coach.Guide.X, coach.Guide.Y), "and it picks up where they left off");
        }

        [Test]
        public void TheGuidedTapCellIsTheNeighbourWhenASideIsAskedFor()
        {
            Tap(0, 1);

            var guide = _coach.Guide;
            Assert.AreEqual(Direction.South, guide.Side);
            Assert.AreEqual((0, 2), guide.TapCell, "the board gates taps on the neighbour, not on the selected cell");
        }

        /// <summary>Every guided placement has to be reachable in at most two taps, or the script is not a script.</summary>
        [Test]
        public void NoGuidedRailEverNeedsMoreThanTwoTaps()
        {
            var laid = new List<(int X, int Y)>();
            for (var guard = 0; guard < 40 && _coach.Guide.Active; guard++)
            {
                var guide = _coach.Guide;
                if (guide.Kind == GuideKind.Erase) { Hold(guide.X, guide.Y); continue; }
                if (guide.Kind == GuideKind.Mistake) { Tap(guide.X, guide.Y); continue; }

                var before = _board.PieceCount;
                Tap(guide.X, guide.Y);
                if (_board.PieceCount == before)
                {
                    Assert.AreEqual(GuideKind.Side, _coach.Guide.Kind,
                        $"({guide.X},{guide.Y}) did not lay and the coach did not name a side");
                    TapSide(_coach.Guide.Side.Value);
                    Assert.Greater(_board.PieceCount, before,
                        $"({guide.X},{guide.Y}) still empty after two taps");
                }

                laid.Add((guide.X, guide.Y));
            }

            Assert.IsTrue(WinChecker.Evaluate(_board).IsWin, "following the coach must finish the level");

            // The staged mistake is laid and lifted without ever entering this list, so every cell here is a rail
            // of the solution and none may repeat: a repeat would mean the coach had sent the player in a circle.
            CollectionAssert.AllItemsAreUnique(laid);
        }
    }
}
