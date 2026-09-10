using System;
using System.Collections.Generic;

namespace TrainSudoku.Core
{
    public enum SelectOutcome
    {
        /// <summary>The cell is occupied or cannot be filled; nothing is selected.</summary>
        Rejected,
        /// <summary>The already selected cell was tapped again; the selection is cleared.</summary>
        Cancelled,
        /// <summary>Exactly one key was legal, so it was placed straight away.</summary>
        AutoPlaced,
        /// <summary>The cell is selected and <see cref="PlacementSession.Available"/> lists the marker directions.</summary>
        Selected,
    }

    public enum ChooseOutcome
    {
        /// <summary>No selection, or the direction is not offered.</summary>
        Ignored,
        /// <summary>First connection chosen; <see cref="PlacementSession.Available"/> now lists the compatible second sides.</summary>
        Narrowed,
        /// <summary>A piece was placed and the selection cleared.</summary>
        Placed,
        /// <summary>The first connection was released; the cell stays selected with its full set of sides back on offer.</summary>
        Reverted,
    }

    /// <summary>How a side of the selected cell reads to the player, for <see cref="PlacementSession.MarkOf"/>.</summary>
    public enum SideMark
    {
        /// <summary>Not on offer: forbidden by the rules, or narrowed out by the first choice.</summary>
        Blocked,
        /// <summary>The player may connect this way.</summary>
        Open,
        /// <summary>The player must connect this way: a neighbour points at this cell, or the side is the S/E tunnel.</summary>
        Forced,
        /// <summary>Already taken as the first connection; the piece is waiting on its second side.</summary>
        Chosen,
    }

    /// <summary>
    /// The tap-to-place interaction of PRD section 4 on top of <see cref="Legality"/>: select a cell, see how each of
    /// its four sides reads through <see cref="MarkOf"/>, pick two. Placement goes through <see cref="Board.TryPlace"/>.
    /// Auto-places when only one key is legal on selection, and when the first chosen side leaves a single possible
    /// second side.
    /// </summary>
    public sealed class PlacementSession
    {
        private readonly Board _board;
        private readonly List<Direction> _available = new List<Direction>();
        private DirectionClass[] _classes;

        public PlacementSession(Board board)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
        }

        public bool IsActive { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }

        /// <summary>The first side chosen, or null while waiting for the first tap on a marker.</summary>
        public Direction? First { get; private set; }

        /// <summary>Sides the player may still tap.</summary>
        public IReadOnlyList<Direction> Available => _available;

        /// <summary>The key and cell of the most recent placement made through this session.</summary>
        public PieceKey? LastPlaced { get; private set; }
        public (int X, int Y) LastPlacedCell { get; private set; }

        public bool IsForced(Direction side) => IsActive && _classes[(int)side] == DirectionClass.Forced;

        /// <summary>
        /// How one side of the selected cell should read: the view marks the neighbour that way and lets a tap on a
        /// non-<see cref="SideMark.Blocked"/> one through to <see cref="Choose"/>. Everything is
        /// <see cref="SideMark.Blocked"/> while nothing is selected.
        /// </summary>
        public SideMark MarkOf(Direction side)
        {
            if (!IsActive) return SideMark.Blocked;
            if (First == side) return SideMark.Chosen;
            if (!_available.Contains(side)) return SideMark.Blocked;
            return IsForced(side) ? SideMark.Forced : SideMark.Open;
        }

        public SelectOutcome Select(int x, int y)
        {
            if (IsActive && X == x && Y == y)
            {
                Cancel();
                return SelectOutcome.Cancelled;
            }

            Cancel();
            if (!_board.IsEmpty(x, y)) return SelectOutcome.Rejected;

            var keys = Legality.LegalKeys(_board, x, y);
            if (keys.Count == 0) return SelectOutcome.Rejected;

            if (keys.Count == 1)
            {
                Place(x, y, keys[0]);
                return SelectOutcome.AutoPlaced;
            }

            IsActive = true;
            X = x;
            Y = y;
            First = null;
            _classes = Legality.Classify(_board, x, y);
            Offer(keys);

            return SelectOutcome.Selected;
        }

        /// <summary>Every side carried by any of the legal keys, in N, E, S, W order so the view marks them predictably.</summary>
        private void Offer(IReadOnlyList<PieceKey> keys)
        {
            _available.Clear();
            foreach (var side in DirectionExtensions.All)
                foreach (var key in keys)
                    if (PieceKeys.Has(key, side))
                    {
                        _available.Add(side);
                        break;
                    }
        }

        public ChooseOutcome Choose(Direction side)
        {
            if (!IsActive) return ChooseOutcome.Ignored;

            // Tapping the side already chosen takes it back: the player is saying "not that way" and gets the cell as
            // it looked on selection. Nothing has been placed since, so the legal keys are still the ones Select found.
            if (First == side)
            {
                First = null;
                Offer(Legality.LegalKeys(_board, X, Y));
                return ChooseOutcome.Reverted;
            }

            if (!_available.Contains(side)) return ChooseOutcome.Ignored;

            if (First.HasValue)
            {
                if (!PieceKeys.TryFromDirections(First.Value, side, out var key) || !Legality.IsLegal(_classes, key))
                {
                    Cancel();
                    return ChooseOutcome.Ignored;
                }

                Place(X, Y, key);
                return ChooseOutcome.Placed;
            }

            var remaining = new List<Direction>();
            foreach (var other in DirectionExtensions.All)
                if (other != side && PieceKeys.TryFromDirections(side, other, out var candidate) && Legality.IsLegal(_classes, candidate))
                    remaining.Add(other);

            if (remaining.Count == 0)
            {
                Cancel();
                return ChooseOutcome.Ignored;
            }

            if (remaining.Count == 1)
            {
                PieceKeys.TryFromDirections(side, remaining[0], out var only);
                Place(X, Y, only);
                return ChooseOutcome.Placed;
            }

            First = side;
            _available.Clear();
            _available.AddRange(remaining);
            return ChooseOutcome.Narrowed;
        }

        public void Cancel()
        {
            IsActive = false;
            First = null;
            _classes = null;
            _available.Clear();
        }

        private void Place(int x, int y, PieceKey key)
        {
            if (!_board.TryPlace(x, y, key))
                throw new InvalidOperationException($"Legality offered {key} at ({x},{y}) but the board refused it.");
            LastPlaced = key;
            LastPlacedCell = (x, y);
            Cancel();
        }
    }
}
