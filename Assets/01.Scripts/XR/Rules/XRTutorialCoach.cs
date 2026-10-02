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
        /// <summary>Staged mistake 1: drop a piece that does not join the rail beside it, and watch it fly back.</summary>
        Adjacency,
        /// <summary>Staged mistake 2: lay a rail that pushes a line past its clue.</summary>
        Mistake,
        /// <summary>The clue has turned red: lift the rail just laid.</summary>
        Erase,
        /// <summary>The lifted rail is in the hand: throw it, or let it go off the board.</summary>
        Discard,
    }

    /// <summary>Where the XR tutorial is pointing, and whether the board is still locked to it.</summary>
    public readonly struct XRTutorialGuide
    {
        public XRGuideKind Kind { get; }
        public int X { get; }
        public int Y { get; }

        /// <summary>The tray slot to light beside the cell, or null for the cell alone (from the fourth rail on).</summary>
        public PieceKey? Slot { get; }

        /// <summary>Until the erase lesson is done, a drop anywhere else is steered back with a note.</summary>
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
    /// The XR tutorial (XR-PRD 7): the phone's walkthrough over the solved path, rewritten for grab-and-drop. The
    /// phone's <see cref="TutorialCoach"/> is built on taps, sides and long-press, so XR has its own (X22) over the same
    /// shared <see cref="Solver"/>, <see cref="PathFinder"/> and <see cref="Legality"/>.
    /// </summary>
    /// <remarks>
    /// The script, all read off the board rather than written against Ashgate:
    /// <list type="number">
    /// <item>Rails 1 to 3 light both the tray slot and the cell.</item>
    /// <item>On rail 2 the lit slot is a key that is <i>illegal</i> there: the ghost goes red and the piece flies back,
    /// which teaches the adjacency rule. Then the right slot lights.</item>
    /// <item>After three rails, the cell alone lights: an off-path cell where the one piece that fits overfills a line.
    /// The player finds that piece, watches the clue turn red, lifts the rail and throws it away.</item>
    /// <item>The board is then the player's. The ring keeps pointing along the route, but nothing is refused.</item>
    /// </list>
    /// Until then the coach is <b>locked</b>: <see cref="Admits"/> is the <see cref="PieceDrop.Gate"/>, so a legal drop
    /// anywhere else comes back <see cref="DropResult.Steered"/> with a note rather than the error cue.
    /// </remarks>
    public sealed class XRTutorialCoach
    {
        /// <summary>Rails laid before the overfill mistake is staged: long enough for laying to feel routine first.</summary>
        public const int GuidedRails = 3;

        /// <summary>The rail (0-based count of rails already laid) that stages the adjacency mistake.</summary>
        public const int AdjacencyRail = 1;

        private LevelData _level;
        private Board _board;
        private readonly List<(int X, int Y)> _order = new List<(int X, int Y)>();
        private readonly List<PieceKey> _keys = new List<PieceKey>();

        private bool _active;
        private bool _won;
        private int _placements;
        private bool _unlocked;

        private bool _adjacencyDone;
        private bool _adjacencyBounced;
        private bool _saidAdjacencyDone;
        private PieceKey _adjacencyKey;

        private bool _mistaking;
        private bool _erasing;
        private bool _discarding;
        private (int X, int Y) _mistakeCell;

        private bool _clueDue;
        private bool _saidClue;
        private bool _saidLay;
        private bool _saidUnlocked;
        private string _note;

        public XRTutorialGuide Guide { get; private set; }

        /// <summary>The localisation key of the line to print, or null when there is nothing to say.</summary>
        public string Key { get; private set; }

        /// <summary>Whether this level is teaching at all.</summary>
        public bool IsActive => _active && Guide.Active;

        /// <summary>Starts (or restarts) the script. Called on every load, Retry included.</summary>
        /// <param name="active">Whether this level teaches at all (<c>LevelDefinition.IsTutorial</c>).</param>
        /// <remarks>A level that will not solve leaves the coach inert: a content fault must not take the level down.</remarks>
        public void Begin(LevelData level, Board board, bool active)
        {
            _level = level;
            _board = board;
            _order.Clear();
            _keys.Clear();
            _active = _won = _unlocked = false;
            _placements = 0;
            _adjacencyDone = _adjacencyBounced = _saidAdjacencyDone = false;
            _mistaking = _erasing = _discarding = false;
            _clueDue = _saidClue = _saidLay = _saidUnlocked = false;
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
            }

            // A board resumed half-built has had its lessons: re-locking it would punish coming back.
            if (_active && board.PieceCount > level.FixedPieces.Count) Unlock();

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
            if (!guide.Active || x != guide.X || y != guide.Y) return false;

            switch (guide.Kind)
            {
                case XRGuideKind.Lay:
                    return key == SolvedKey(x, y);
                case XRGuideKind.Adjacency:
                    // The wrong key is wanted: the rules, not the gate, send it back. The right one is fine too.
                    return key == _adjacencyKey || key == SolvedKey(x, y);
                case XRGuideKind.Mistake:
                    // Only one key fits there; any other is refused by the rules with a red ghost, which is a lesson too.
                    return true;
                default:
                    // Erase: the rail on the cell is to be lifted, not covered.
                    return false;
            }
        }

        /// <summary>Takes one grab or release, with the board as it now is.</summary>
        /// <param name="overfull">Some row or column holds more rails than its clue.</param>
        /// <param name="lineSatisfied">A row or column turned green with this action.</param>
        public void Observe(DropResult result, bool overfull, bool lineSatisfied)
        {
            if (!_active || _won) return;

            // Anything the player does acknowledges the last correction.
            _note = null;
            var guide = Guide;

            switch (result.Outcome)
            {
                case DropOutcome.Refused:
                    if (result.RefuseReason == RefuseReason.FixedPiece) _note = XRTutorialKeys.NoteFixed;
                    break;

                case DropOutcome.Lifted:
                    if (!_unlocked)
                    {
                        // The mistake rail, lifted as asked: next it goes in the bin. Any other lift unprompted means
                        // the player has found erasing alone, and the detour has nothing left to teach.
                        _discarding = _erasing;
                        Unlock();
                    }

                    break;

                case DropOutcome.Placed:
                case DropOutcome.Replaced:
                case DropOutcome.Moved:
                    _discarding = false;
                    if (result.Outcome != DropOutcome.Moved) _placements++;
                    if (_saidClue) _clueDue = false;

                    if (_mistaking && result.Cell == _mistakeCell)
                    {
                        _mistaking = false;
                        _erasing = true;
                    }

                    if (guide.Kind == XRGuideKind.Adjacency) _adjacencyDone = true;
                    break;

                case DropOutcome.Returned:
                    _discarding = false;
                    if (guide.Kind == XRGuideKind.Adjacency && result.IllegalDrop && result.Cell == (guide.X, guide.Y))
                    {
                        _adjacencyDone = true;
                        _adjacencyBounced = true;
                    }
                    else if (result.Steered)
                    {
                        var atTarget = result.Cell == (guide.X, guide.Y);
                        _note = atTarget && guide.Slot.HasValue ? XRTutorialKeys.NoteWrongPiece : XRTutorialKeys.NoteSteered;
                    }

                    break;

                case DropOutcome.Puffed:
                case DropOutcome.Thrown:
                    _discarding = false;
                    break;
            }

            if (lineSatisfied && !_saidClue) _clueDue = true;

            // A line over its clue outranks every other correction, except during the erase lesson, which says it already.
            if (overfull && !_erasing && !_discarding) _note = XRTutorialKeys.NoteOverfull;

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
            _mistaking = false;
            _erasing = false;
            _unlocked = true;
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

        /// <summary>
        /// Somewhere to lay a rail that is definitely wrong: an empty cell off the solved path where exactly one key is
        /// legal (so the player can find it), and where laying it pushes a row or column past its clue, so they
        /// <i>see</i> the mistake turn red. Nearest to the last rail laid wins. A port of the phone coach's search.
        /// </summary>
        private bool TryFindMistake(out (int X, int Y) cell)
        {
            cell = default;
            var from = _order[System.Math.Max(0, System.Math.Min(_placements, _order.Count) - 1)];
            var best = int.MaxValue;
            for (var y = 0; y < _board.Height; y++)
            for (var x = 0; x < _board.Width; x++)
            {
                if (!_board.IsEmpty(x, y)) continue;
                if (_order.Contains((x, y))) continue;
                if (_board.RowCount(y) < _level.RowClues[y] && _board.ColumnCount(x) < _level.ColumnClues[x]) continue;
                if (Legality.LegalKeys(_board, x, y).Count != 1) continue;

                var distance = System.Math.Abs(x - from.X) + System.Math.Abs(y - from.Y);
                if (distance >= best) continue;
                best = distance;
                cell = (x, y);
            }

            return best < int.MaxValue;
        }

        /// <summary>The first key, in <see cref="PieceKey"/> order, that is illegal on (x, y); false when every key fits.</summary>
        private bool TryFindIllegalKey(int x, int y, out PieceKey key)
        {
            var legal = Legality.LegalKeys(_board, x, y);
            for (var k = PieceKey.NS; k <= PieceKey.SE; k++)
            {
                if (Contains(legal, k)) continue;
                key = k;
                return true;
            }

            key = default;
            return false;
        }

        private static bool Contains(IReadOnlyList<PieceKey> keys, PieceKey key)
        {
            for (var i = 0; i < keys.Count; i++)
                if (keys[i] == key)
                    return true;
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

            if (_discarding) return new XRTutorialGuide(XRGuideKind.Discard, _mistakeCell.X, _mistakeCell.Y, null, false);
            if (_erasing) return new XRTutorialGuide(XRGuideKind.Erase, _mistakeCell.X, _mistakeCell.Y, null, true);
            if (_mistaking) return new XRTutorialGuide(XRGuideKind.Mistake, _mistakeCell.X, _mistakeCell.Y, null, true);

            if (!_unlocked && _placements >= GuidedRails)
            {
                if (TryFindMistake(out _mistakeCell))
                {
                    _mistaking = true;
                    return new XRTutorialGuide(XRGuideKind.Mistake, _mistakeCell.X, _mistakeCell.Y, null, true);
                }

                // Nowhere on this board to stage one: better to hand the board over than to stall on a lesson.
                Unlock();
            }

            var index = NextIndex();
            if (index < 0) return XRTutorialGuide.None;
            var (x, y) = _order[index];

            if (_unlocked) return new XRTutorialGuide(XRGuideKind.Lay, x, y, null, false);

            if (_placements == AdjacencyRail && !_adjacencyDone)
            {
                if (TryFindIllegalKey(x, y, out _adjacencyKey))
                    return new XRTutorialGuide(XRGuideKind.Adjacency, x, y, _adjacencyKey, true);
                _adjacencyDone = true;   // every key fits here, so there is no rule to show off
            }

            return new XRTutorialGuide(XRGuideKind.Lay, x, y, _keys[index], true);
        }

        private string ResolveKey()
        {
            if (!Guide.Active) return null;

            switch (Guide.Kind)
            {
                case XRGuideKind.Adjacency: return XRTutorialKeys.Adjacency;
                case XRGuideKind.Mistake: return XRTutorialKeys.Mistake;
                case XRGuideKind.Erase: return XRTutorialKeys.Erase;
                case XRGuideKind.Discard: return XRTutorialKeys.Discard;
            }

            if (_adjacencyBounced && !_saidAdjacencyDone)
            {
                _saidAdjacencyDone = true;
                return XRTutorialKeys.AdjacencyDone;
            }

            if (_clueDue && !_saidClue) return XRTutorialKeys.Clue;

            if (_unlocked)
            {
                if (_saidUnlocked) return XRTutorialKeys.Next;
                _saidUnlocked = true;
                return XRTutorialKeys.Unlocked;
            }

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
        public const string Adjacency = "xr.tutorial.adjacency";
        public const string AdjacencyDone = "xr.tutorial.adjacency_done";
        public const string Clue = "xr.tutorial.clue";
        public const string Mistake = "xr.tutorial.mistake";
        public const string Erase = "xr.tutorial.erase";
        public const string Discard = "xr.tutorial.discard";
        public const string Unlocked = "xr.tutorial.unlocked";
        public const string Next = "xr.tutorial.next";
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
