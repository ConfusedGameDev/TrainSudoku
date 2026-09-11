using System.Collections.Generic;

namespace TrainSudoku.Core
{
    /// <summary>What the player just did to the board, as the tutorial needs to hear it.</summary>
    /// <remarks>
    /// Deliberately coarser than <see cref="SelectOutcome"/> and <see cref="ChooseOutcome"/>: the coach cares that a
    /// piece arrived <i>through the two-tap route</i>, not which key it was. The board raises these; nothing else in
    /// the game listens.
    /// </remarks>
    public enum BoardAction
    {
        /// <summary>A cell was selected and its sides are marked.</summary>
        Selected,

        /// <summary>The selection was dropped without placing.</summary>
        Deselected,

        /// <summary>The first side was taken; the cell is waiting on its second.</summary>
        Narrowed,

        /// <summary>One tap was enough: only one key was legal there.</summary>
        AutoPlaced,

        /// <summary>A piece was placed by choosing its sides.</summary>
        Connected,

        /// <summary>A piece the player laid was lifted off.</summary>
        Erased,

        /// <summary>A hold landed on a fixed piece, which cannot be lifted.</summary>
        EraseRefused,

        /// <summary>A tap landed away from the guided cell while the tutorial had the board locked.</summary>
        Refused,
    }

    /// <summary>What the player is being asked to do to the cell the tutorial is pointing at.</summary>
    public enum GuideKind
    {
        /// <summary>Nothing is being guided.</summary>
        None,

        /// <summary>Tap the cell.</summary>
        Select,

        /// <summary>The cell is selected; tap the neighbour on <see cref="TutorialGuide.Side"/>.</summary>
        Side,

        /// <summary>Lay a rail here on purpose, knowing it is wrong.</summary>
        Mistake,

        /// <summary>Hold the piece in the cell to lift it.</summary>
        Erase,
    }

    /// <summary>
    /// Where the tutorial is pointing, read by both halves of the game: the board draws a ring there and gates taps
    /// on it, and the Play screen anchors its callout to it.
    /// </summary>
    public readonly struct TutorialGuide
    {
        public bool Active { get; }
        public int X { get; }
        public int Y { get; }

        /// <summary>Set only for <see cref="GuideKind.Side"/>: the neighbour to tap, not the cell itself.</summary>
        public Direction? Side { get; }

        public GuideKind Kind { get; }

        /// <summary>While true the board refuses every tap that is not on the guided target.</summary>
        public bool Locked { get; }

        public TutorialGuide(int x, int y, Direction? side, GuideKind kind, bool locked)
        {
            Active = kind != GuideKind.None;
            X = x;
            Y = y;
            Side = side;
            Kind = kind;
            Locked = locked;
        }

        public static TutorialGuide None => default;

        /// <summary>The cell a tap must land on: the guided cell, or its neighbour when a side is being asked for.</summary>
        public (int X, int Y) TapCell => Side.HasValue ? (X + Side.Value.Dx(), Y + Side.Value.Dy()) : (X, Y);
    }

    /// <summary>One player action and the state of the board immediately after it.</summary>
    /// <remarks>
    /// <see cref="LineSatisfied"/> is carried here rather than pushed separately because the board raises its
    /// line-cleared event from inside the validator, <b>before</b> the action — so a coach told about it directly
    /// would have the news wiped by the very placement that earned it.
    /// </remarks>
    public readonly struct BoardPulse
    {
        public BoardAction Action { get; }
        public (int X, int Y)? Selected { get; }
        public Direction? Chosen { get; }
        public bool Overfull { get; }
        public bool LineSatisfied { get; }

        public BoardPulse(BoardAction action, (int X, int Y)? selected = null, Direction? chosen = null,
            bool overfull = false, bool lineSatisfied = false)
        {
            Action = action;
            Selected = selected;
            Chosen = chosen;
            Overfull = overfull;
            LineSatisfied = lineSatisfied;
        }
    }

    /// <summary>
    /// The teaching script for the tutorial station: it knows the level's solution, walks the player along it rail
    /// by rail, and decides which single line of instruction should be on screen. Pure state, no timers and no
    /// engine — the board asks for <see cref="Guide"/> and the screen asks for <see cref="Key"/>.
    /// </summary>
    /// <remarks>
    /// <b>A walkthrough over the solved path.</b> <see cref="Begin"/> solves the level once and walks it from the
    /// entrance with <see cref="PathFinder"/>, which gives the order a railway would actually be laid in. The target
    /// is then always the first cell of that order still empty, so the player is guided to a finished board — and
    /// the coach never has to be told where they are, because the board itself says.
    ///
    /// <b>The lessons are read off the board, not written down.</b> Which rung a step teaches comes from
    /// <see cref="Legality.LegalKeys"/> at that cell: one legal key is the "a single tap lays it" lesson, more than
    /// one is the "choose the two sides" lesson. So a different tutorial board produces its own script rather than
    /// needing this file edited.
    ///
    /// <b>The opening is locked.</b> Until the erase lesson is done, <see cref="TutorialGuide.Locked"/> is set and
    /// the board refuses taps anywhere else. A lesson that can be walked past teaches nothing to the player who
    /// walks past it; after it, the callout is only a pointer and the board is the player's again.
    ///
    /// On top of all of it sits a <b>note</b>: a one-off correction — a tap in the wrong place, a hold on a fixed
    /// piece, a line pushed past its clue — that replaces the line until the next thing the player does. Notes need
    /// no clock because an action is what clears them, which is also the only moment anyone would look away.
    /// </remarks>
    public sealed class TutorialCoach
    {
        /// <summary>Rails laid before the mistake is staged. Long enough for laying to feel routine first.</summary>
        private const int MistakeAfterRails = 3;

        private LevelData _level;
        private Board _board;
        private int _placements;

        /// <summary>Cells the player must fill, in the order the track runs from S to E. Fixed cells are not in it.</summary>
        private readonly List<(int X, int Y)> _order = new List<(int X, int Y)>();

        /// <summary>The solved key for every cell in <see cref="_order"/>, same index.</summary>
        private readonly List<PieceKey> _keys = new List<PieceKey>();

        private bool _active;
        private bool _won;

        // Which lessons have had their full copy shown. Each is shown once; afterwards the line goes terse.
        private bool _saidSelect;
        private bool _saidAuto;
        private bool _saidUnlocked;

        // The mistake detour: lay a rail where it does not belong, watch a clue turn red, then lift it off.
        private bool _mistaking;
        private bool _erasing;
        private (int X, int Y) _mistakeCell;
        private bool _unlocked;

        private bool _clueDue;
        private bool _saidClue;

        private (int X, int Y)? _selected;
        private Direction? _chosen;
        private string _note;

        /// <summary>Where the tutorial is pointing. <see cref="TutorialGuide.None"/> when it has nothing to say.</summary>
        public TutorialGuide Guide { get; private set; }

        /// <summary>The localisation key of the line to print, or null when there is nothing to say.</summary>
        public string Key { get; private set; }

        /// <summary>Whether this level is teaching at all.</summary>
        public bool IsActive => _active && Guide.Active;

        /// <summary>
        /// Starts (or restarts) the script on a level. Called on every load, Retry included.
        /// </summary>
        /// <param name="level">The level being played, for the solver.</param>
        /// <param name="board">The live board the player is filling. The coach reads it, never writes to it.</param>
        /// <param name="active">Whether this level teaches at all (<c>LevelDefinition.IsTutorial</c>).</param>
        /// <remarks>
        /// A level that will not solve leaves the coach inert rather than throwing: a tutorial that cannot be
        /// walked is a content fault, and it must not take the level down with it.
        /// </remarks>
        public void Begin(LevelData level, Board board, bool active)
        {
            _level = level;
            _board = board;
            _order.Clear();
            _keys.Clear();
            _active = false;
            _won = false;
            _placements = 0;
            _saidSelect = _saidAuto = _saidUnlocked = false;
            _mistaking = _erasing = _unlocked = false;
            _clueDue = _saidClue = false;
            _selected = null;
            _chosen = null;
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

            // A board resumed half-built skips straight past the locked opening: the player who built it has
            // already had those lessons, and re-locking the board under them would be a punishment for coming back.
            if (_active && board.PieceCount > level.FixedPieces.Count) Unlock();

            Refresh();
        }

        /// <summary>Takes one player action and the board state after it.</summary>
        public void Observe(BoardPulse pulse)
        {
            if (!_active) return;

            // Anything the player does is an acknowledgement of the last correction.
            _note = null;
            _selected = pulse.Selected;
            _chosen = pulse.Chosen;

            switch (pulse.Action)
            {
                case BoardAction.AutoPlaced:
                case BoardAction.Connected:
                    _placements++;

                    // A clue line that has had its turn on screen is retired by the next rail laid.
                    if (_saidClue) _clueDue = false;

                    // The rail just laid in the wrong place is the one to practise lifting on.
                    if (_mistaking)
                    {
                        _mistaking = false;
                        _erasing = true;
                    }

                    break;

                case BoardAction.Erased:
                    if (_erasing) Unlock();
                    else if (!_unlocked) Unlock();   // lifted a rail unprompted: the lesson has landed already
                    break;

                case BoardAction.EraseRefused:
                    _note = TutorialKeys.NoteFixed;
                    break;

                case BoardAction.Refused:
                    _note = TutorialKeys.Refused;
                    break;
            }

            if (pulse.LineSatisfied && !_saidClue) _clueDue = true;

            // A line over its clue outranks every other correction: it is the one that needs saying, and the one
            // whose answer — lift a rail — is the lesson the player may not have reached yet. Not during the
            // mistake detour, where the erase line already says it, better and in context.
            if (pulse.Overfull && !_erasing) _note = TutorialKeys.NoteOverfull;

            Refresh();
        }

        /// <summary>The board is won. Nothing more to teach.</summary>
        public void Complete()
        {
            _won = true;
            _note = null;
            Refresh();
        }

        /// <summary>The erase detour is done (or was never needed): the board is the player's from here.</summary>
        private void Unlock()
        {
            _mistaking = false;
            _erasing = false;
            _unlocked = true;
        }

        /// <summary>
        /// Somewhere to lay a rail that is definitely wrong: an empty cell off the solved path where exactly one
        /// key is legal — so it goes down in a single tap, like the rail before it — and where laying it pushes a
        /// row or column past its clue, so the player <i>sees</i> the mistake turn red rather than being told.
        /// </summary>
        /// <remarks>
        /// Nearest to the last rail laid wins, because that is where the eye already is and where a wrong move
        /// would plausibly have been made. Nothing is hard-coded to a board: a tutorial level that offered no such
        /// cell would simply skip the detour, which is why the shipped one is held to having it by a test.
        /// </remarks>
        private bool TryFindMistake(out (int X, int Y) cell)
        {
            cell = default;
            if (_level == null) return false;

            var from = _order[System.Math.Min(_placements, _order.Count) - 1];
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

        /// <summary>The first cell of the solved order the player has not filled yet, or null when they all are.</summary>
        private int NextIndex()
        {
            for (var i = 0; i < _order.Count; i++)
                if (!_board[_order[i].X, _order[i].Y].HasValue)
                    return i;
            return -1;
        }

        private void Refresh()
        {
            Guide = ResolveGuide();
            Key = _note ?? ResolveKey();

            // The clue line has now actually been on screen, so it can be retired by the next placement.
            if (Key == TutorialKeys.Clue) _saidClue = true;
        }

        private TutorialGuide ResolveGuide()
        {
            if (!_active || _won) return TutorialGuide.None;

            if (_erasing) return new TutorialGuide(_mistakeCell.X, _mistakeCell.Y, null, GuideKind.Erase, true);
            if (_mistaking) return new TutorialGuide(_mistakeCell.X, _mistakeCell.Y, null, GuideKind.Mistake, true);

            // Three rails in and laying them is routine, so it is time to show what happens when one is wrong.
            if (!_unlocked && _placements >= MistakeAfterRails && _saidAuto)
            {
                if (TryFindMistake(out _mistakeCell))
                {
                    _mistaking = true;
                    return new TutorialGuide(_mistakeCell.X, _mistakeCell.Y, null, GuideKind.Mistake, true);
                }

                // Nowhere on this board to stage one. Better to hand the board over than to stall on a lesson.
                Unlock();
            }

            var index = NextIndex();
            if (index < 0) return TutorialGuide.None;

            var (x, y) = _order[index];
            var locked = !_unlocked;

            // Not selected yet, or the player is looking at some other cell: point at the one to tap.
            if (!_selected.HasValue || _selected.Value != (x, y))
                return new TutorialGuide(x, y, null, GuideKind.Select, locked);

            var side = FinishingSide(x, y, _keys[index]);
            return side.HasValue
                ? new TutorialGuide(x, y, side, GuideKind.Side, locked)
                : new TutorialGuide(x, y, null, GuideKind.Select, locked);
        }

        /// <summary>
        /// Which side of the solved key the player should tap next: the one they have not already taken. When
        /// neither is taken, the one that <b>completes the piece on its own</b> is preferred, so a guided placement
        /// is never more than two taps — <see cref="PlacementSession"/> auto-places as soon as a chosen side leaves
        /// a single legal partner.
        /// </summary>
        private Direction? FinishingSide(int x, int y, PieceKey key)
        {
            var (a, b) = PieceKeys.Connections(key);
            if (_chosen.HasValue) return _chosen.Value == a ? b : _chosen.Value == b ? a : (Direction?)null;

            var classes = Legality.Classify(_board, x, y);
            return Completes(classes, a) ? a : Completes(classes, b) ? b : a;
        }

        /// <summary>True when taking <paramref name="side"/> leaves exactly one legal partner, so the piece lands.</summary>
        private bool Completes(DirectionClass[] classes, Direction side)
        {
            var partners = 0;
            foreach (var other in DirectionExtensions.All)
                if (other != side && PieceKeys.TryFromDirections(side, other, out var candidate)
                    && Legality.IsLegal(classes, candidate))
                    partners++;
            return partners == 1;
        }

        private string ResolveKey()
        {
            if (!Guide.Active) return null;

            switch (Guide.Kind)
            {
                case GuideKind.Mistake: return TutorialKeys.Mistake;
                case GuideKind.Erase: return TutorialKeys.Erase;
                case GuideKind.Side:
                    if (_chosen.HasValue) return TutorialKeys.SideSecond;
                    // Full copy for as long as the board is locked — both opening rails get the whole sentence,
                    // which is one repetition and cheap. It goes terse the moment the player is on their own.
                    return _unlocked ? TutorialKeys.SideNext : TutorialKeys.SideFirst;
            }

            // A clue going green is worth one line, and it is worth more than "next rail here".
            if (_clueDue && !_saidClue) return TutorialKeys.Clue;

            if (_unlocked && !_saidUnlocked)
            {
                _saidUnlocked = true;
                return TutorialKeys.Unlocked;
            }

            var singleKey = Legality.LegalKeys(_board, Guide.X, Guide.Y).Count == 1;
            if (singleKey && !_saidAuto)
            {
                _saidAuto = true;
                return TutorialKeys.SelectAuto;
            }

            if (!_saidSelect)
            {
                _saidSelect = true;
                return TutorialKeys.SelectFirst;
            }

            return TutorialKeys.SelectNext;
        }
    }

    /// <summary>
    /// The tutorial's rows in the `UI` String Table. They are named here, in Core, so the state machine and its
    /// tests can talk about them without a reference to the localisation package.
    /// </summary>
    public static class TutorialKeys
    {
        public const string SelectFirst = "tutorial.select_first";
        public const string SelectAuto = "tutorial.select_auto";
        public const string SelectNext = "tutorial.select_next";
        public const string SideFirst = "tutorial.side_first";
        public const string SideSecond = "tutorial.side_second";
        public const string SideNext = "tutorial.side_next";
        public const string Clue = "tutorial.clue";
        public const string Mistake = "tutorial.mistake";
        public const string Erase = "tutorial.erase";
        public const string Unlocked = "tutorial.unlocked";
        public const string Refused = "tutorial.refused";
        public const string NoteFixed = "tutorial.note_fixed";
        public const string NoteOverfull = "tutorial.note_overfull";
    }
}
