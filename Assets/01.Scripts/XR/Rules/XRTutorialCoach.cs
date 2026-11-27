using System.Collections.Generic;
using TrainSudoku.Core;

namespace TrainSudoku.XR.Rules
{
    /// <summary>What the XR tutorial is pointing at a cell for (XR-PRD 7).</summary>
    public enum XRGuideKind
    {
        None,
        /// <summary>Lay the next rail of the solved route here.</summary>
        Lay,
        /// <summary>A hint: the rail on this cell is not part of the route, lift it off.</summary>
        Erase,
    }

    /// <summary>Where the XR tutorial is pointing, and whether the board is still locked to it.</summary>
    public readonly struct XRTutorialGuide
    {
        public XRGuideKind Kind { get; }
        public int X { get; }
        public int Y { get; }

        /// <summary>The tray slot to light beside the cell, or null for the cell alone.</summary>
        public PieceKey? Slot { get; }

        /// <summary>For the two opening rails, a drop anywhere else is steered back with a note.</summary>
        public bool Locked { get; }

        public bool Active => Kind != XRGuideKind.None;

        public XRTutorialGuide(XRGuideKind kind, int x, int y, PieceKey? slot, bool locked)
        {
            Kind = kind;
            X = x;
            Y = y;
            Slot = slot;
            Locked = locked;
        }

        public static XRTutorialGuide None => default;

        public override string ToString() => Active ? $"{Kind} at ({X},{Y}) slot {Slot?.ToString() ?? "-"}{(Locked ? " locked" : "")}" : "None";
    }

    /// <summary>
    /// The XR tutorial (XR-PRD 7): two rails shown, then the board is the player's, with a hint when they stall. The
    /// phone's <see cref="TutorialCoach"/> is built on taps, sides and long-press, so XR has its own (X22) over the same
    /// shared <see cref="Solver"/>, <see cref="PathFinder"/> and <see cref="Legality"/>.
    /// </summary>
    /// <remarks>
    /// The script, all read off the board rather than written against Ashgate:
    /// <list type="number">
    /// <item>Rails 1 and 2 light both the tray slot and the cell, and the board is <b>locked</b> to them:
    /// <see cref="Admits"/> is the <see cref="PieceDrop.Gate"/>, so a legal drop anywhere else comes back
    /// <see cref="DropResult.Steered"/> with a note rather than the error cue.</item>
    /// <item>Then the ring goes and the player is on their own. Nothing is refused.</item>
    /// <item>After <see cref="IdleSeconds"/> with no grab or release, <see cref="Tick"/> raises a <b>hint</b>: a rail
    /// that is not the solution's is ringed to be lifted; otherwise the next empty cell of the route is ringed with its
    /// tray slot lit. The hint stays until the board changes, and then the clock starts again.</item>
    /// </list>
    /// The hint is the one exception to "no hints" (X15), and it exists only here: a coach that is not teaching never
    /// ticks.
    /// </remarks>
    public sealed class XRTutorialCoach
    {
        /// <summary>Rails the player is walked through before the board is handed over.</summary>
        public const int GuidedRails = 2;

        /// <summary>How long the player may go without a grab or a release before the coach points at the next move.</summary>
        public const double IdleSeconds = 30.0;

        private LevelData _level;
        private Board _board;
        private Board _solution;
        private readonly List<(int X, int Y)> _order = new List<(int X, int Y)>();
        private readonly List<PieceKey> _keys = new List<PieceKey>();

        private bool _active;
        private bool _won;
        private int _placements;
        private bool _unlocked;

        /// <summary>The "you have it" line is owed: set on unlocking, dropped by the player's next action.</summary>
        private bool _unlockedDue;

        private double _idle;
        private bool _hinting;

        private bool _clueDue;
        private bool _saidClue;
        private bool _saidLay;
        private string _note;

        public XRTutorialGuide Guide { get; private set; }

        /// <summary>The localisation key of the line to print, or null when there is nothing to say.</summary>
        /// <remarks>A line can stand without a ring: <see cref="Guide"/> may be inactive while this is set.</remarks>
        public string Key { get; private set; }

        /// <summary>Whether this level is teaching at all.</summary>
        public bool IsActive => _active && !_won;

        /// <summary>Starts (or restarts) the script. Called on every load, Retry included.</summary>
        /// <param name="active">Whether this level teaches at all (<c>LevelDefinition.IsTutorial</c>).</param>
        /// <remarks>A level that will not solve leaves the coach inert: a content fault must not take the level down.</remarks>
        public void Begin(LevelData level, Board board, bool active)
        {
            _level = level;
            _board = board;
            _solution = null;
            _order.Clear();
            _keys.Clear();
            _active = _won = _unlocked = _unlockedDue = false;
            _placements = 0;
            _idle = 0.0;
            _hinting = false;
            _clueDue = _saidClue = _saidLay = false;
            _note = null;

            if (active && level != null && board != null && Solver.TrySolve(level, out var solution)
                && PathFinder.TryFindPath(solution, out var path))
            {
                foreach (var (x, y) in path)
                {
                    if (!(solution[x, y] is Piece piece)) continue;
                    if (level.TryGetFixedPiece(x, y, out _)) continue;
                    _order.Add((x, y));
                    _keys.Add(piece.Key);
                }

                _active = _order.Count > 0;
                _solution = solution;
            }

            // A board resumed half-built has had its lessons: it comes back open, and without the line that ends them.
            if (_active && board.PieceCount > level.FixedPieces.Count) _unlocked = true;

            Refresh();
        }

        /// <summary>
        /// The <see cref="PieceDrop.Gate"/>: whether a landing of <paramref name="key"/> on (x, y) is one the tutorial is
        /// asking for. Always true once unlocked, or when the coach is not teaching.
        /// </summary>
        public bool Admits(int x, int y, PieceKey key)
        {
            if (!_active || _won || _unlocked) return true;
            var guide = Guide;
            return guide.Kind == XRGuideKind.Lay && x == guide.X && y == guide.Y && key == SolvedKey(x, y);
        }

        /// <summary>
        /// Runs the idle clock. Call it only while the board is in play, so a pause or a briefing does not count.
        /// </summary>
        /// <returns>True when a hint has just come up, so the caller redraws the ring and the line.</returns>
        public bool Tick(double seconds)
        {
            if (!_active || _won || !_unlocked || _hinting || seconds <= 0.0) return false;
            _idle += seconds;
            if (_idle < IdleSeconds) return false;

            _idle = 0.0;
            _hinting = true;
            // The hint is the newest thing to say: it replaces a correction that has stood unanswered this long.
            _note = null;
            _unlockedDue = false;
            Refresh();
            if (Guide.Active) return true;

            _hinting = false;   // nothing to point at
            return false;
        }

        /// <summary>Takes one grab or release, with the board as it now is.</summary>
        /// <param name="overfull">Some row or column holds more rails than its clue.</param>
        /// <param name="lineSatisfied">A row or column turned green with this action.</param>
        public void Observe(DropResult result, bool overfull, bool lineSatisfied)
        {
            if (!_active || _won) return;

            // Anything the player does acknowledges the last correction, and is not standing idle.
            _note = null;
            _idle = 0.0;
            var guide = Guide;
            var wasOpen = _unlocked;
            _unlockedDue = false;

            switch (result.Outcome)
            {
                case DropOutcome.Refused:
                    if (result.RefuseReason == RefuseReason.FixedPiece) _note = XRTutorialKeys.NoteFixed;
                    break;

                case DropOutcome.Lifted:
                    _hinting = false;
                    // A rail lifted unprompted: the player has found their own way round the board already.
                    if (!_unlocked) Unlock();
                    break;

                case DropOutcome.Placed:
                case DropOutcome.Replaced:
                case DropOutcome.Moved:
                    _hinting = false;
                    if (result.Outcome != DropOutcome.Moved) _placements++;
                    if (_saidClue) _clueDue = false;
                    if (!_unlocked && _placements >= GuidedRails) Unlock();
                    break;

                case DropOutcome.Returned:
                    if (result.Steered)
                    {
                        var atTarget = result.Cell == (guide.X, guide.Y);
                        _note = atTarget && guide.Slot.HasValue ? XRTutorialKeys.NoteWrongPiece : XRTutorialKeys.NoteSteered;
                    }

                    break;
            }

            // Worth one line, but only once the player is laying rails of their own choosing.
            if (lineSatisfied && wasOpen && !_saidClue) _clueDue = true;

            // A line over its clue outranks every other correction.
            if (overfull) _note = XRTutorialKeys.NoteOverfull;

            Refresh();
        }

        /// <summary>The board is won. Nothing more to teach.</summary>
        public void Complete()
        {
            _won = true;
            _note = null;
            Refresh();
        }

        private void Unlock()
        {
            _unlocked = true;
            _unlockedDue = true;
        }

        private PieceKey SolvedKey(int x, int y)
        {
            var index = _order.IndexOf((x, y));
            return index < 0 ? default : _keys[index];
        }

        /// <summary>The first cell of the solved order still empty, or -1 when they all are filled.</summary>
        private int NextIndex()
        {
            for (var i = 0; i < _order.Count; i++)
                if (!_board[_order[i].X, _order[i].Y].HasValue)
                    return i;
            return -1;
        }

        /// <summary>A rail the player laid that the solution does not have there: the thing in the way of finishing.</summary>
        private bool TryFindWrongRail(out (int X, int Y) cell)
        {
            for (var y = 0; y < _board.Height; y++)
            for (var x = 0; x < _board.Width; x++)
            {
                if (!(_board[x, y] is Piece laid)) continue;
                if (_level.TryGetFixedPiece(x, y, out _)) continue;
                if (_solution[x, y] is Piece solved && solved.Key == laid.Key) continue;
                cell = (x, y);
                return true;
            }

            cell = default;
            return false;
        }

        private void Refresh()
        {
            Guide = ResolveGuide();
            Key = _note ?? ResolveKey();
            if (Key == XRTutorialKeys.Clue) _saidClue = true;
        }

        private XRTutorialGuide ResolveGuide()
        {
            if (!_active || _won) return XRTutorialGuide.None;

            if (_unlocked)
            {
                if (!_hinting) return XRTutorialGuide.None;
                // What is wrong comes off before anything more goes on: the next rail may not even fit beside it.
                if (TryFindWrongRail(out var wrong)) return new XRTutorialGuide(XRGuideKind.Erase, wrong.X, wrong.Y, null, false);
                var next = NextIndex();
                return next < 0
                    ? XRTutorialGuide.None
                    : new XRTutorialGuide(XRGuideKind.Lay, _order[next].X, _order[next].Y, _keys[next], false);
            }

            var index = NextIndex();
            return index < 0
                ? XRTutorialGuide.None
                : new XRTutorialGuide(XRGuideKind.Lay, _order[index].X, _order[index].Y, _keys[index], true);
        }

        private string ResolveKey()
        {
            if (!_active || _won) return null;

            if (_unlocked)
            {
                if (Guide.Kind == XRGuideKind.Erase) return XRTutorialKeys.HintLift;
                if (Guide.Kind == XRGuideKind.Lay) return XRTutorialKeys.Next;
                if (_unlockedDue) return XRTutorialKeys.Unlocked;
                return _clueDue && !_saidClue ? XRTutorialKeys.Clue : null;
            }

            if (!Guide.Active) return null;
            if (_saidLay) return XRTutorialKeys.LayNext;
            _saidLay = true;
            return XRTutorialKeys.LayFirst;
        }
    }

    /// <summary>The XR tutorial's lines. Keys in the `XR` String Table (XR10); named here so the rules can talk about them.</summary>
    public static class XRTutorialKeys
    {
        public const string LayFirst = "xr.tutorial.lay_first";
        public const string LayNext = "xr.tutorial.lay_next";
        public const string Clue = "xr.tutorial.clue";
        public const string Unlocked = "xr.tutorial.unlocked";
        public const string Next = "xr.tutorial.next";
        public const string HintLift = "xr.tutorial.hint_lift";
        public const string NoteSteered = "xr.tutorial.note_steered";
        public const string NoteWrongPiece = "xr.tutorial.note_wrong_piece";
        public const string NoteFixed = "xr.tutorial.note_fixed";
        public const string NoteOverfull = "xr.tutorial.note_overfull";

        public const string Brief1Title = "xr.brief.1_title";
        public const string Brief1Body = "xr.brief.1_body";
        public const string Brief2Title = "xr.brief.2_title";
        public const string Brief2Body = "xr.brief.2_body";
        public const string Brief3Title = "xr.brief.3_title";
        public const string Brief3Body = "xr.brief.3_body";
        public const string BriefNext = "xr.brief.next";
        public const string BriefStart = "xr.brief.start";

        // The board lesson (XRBoardLesson): the callout's three lines, then the signboard's two cards.
        public const string MoveApproach = "xr.move.approach";
        public const string MovePinch = "xr.move.pinch";
        public const string MoveCarry = "xr.move.carry";
        public const string MoveTitle = "xr.move.title";
        public const string MoveBody = "xr.move.body";
        public const string MoveSkip = "xr.move.skip";
        public const string MoveClosingTitle = "xr.move.closing_title";
        public const string MoveClosingBody = "xr.move.closing_body";
        public const string MoveDone = "xr.move.done";
    }
}
