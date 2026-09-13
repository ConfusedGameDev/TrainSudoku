using System;
using TrainSudoku.Core;

namespace TrainSudoku.XR.Rules
{
    /// <summary>
    /// Grab-and-drop on the shared <see cref="Board"/>, XR-PRD section 4.4. The XR input layer turns hardware events into
    /// these calls (the grab interface, 10.4) and nothing else touches the board. Every landing goes through
    /// <see cref="Board.TryPlace"/> and <see cref="Legality"/>, so a piece can only land where the phone would take it.
    /// </summary>
    public sealed class PieceDrop
    {
        private readonly HeldPiece?[] _held = new HeldPiece?[2];

        public Board Board { get; }

        public PieceDrop(Board board)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
        }

        public HeldPiece? Held(Hand hand) => _held[(int)hand];
        public bool IsHolding(Hand hand) => _held[(int)hand].HasValue;

        /// <summary>A hand takes a piece from a tray slot. Supply is unlimited, so only a busy hand is refused.</summary>
        public DropResult GrabFromTray(Hand hand, PieceKey key)
        {
            if (IsHolding(hand)) return Refuse(hand, RefuseReason.HandBusy, key, null);
            _held[(int)hand] = new HeldPiece(key, null);
            return new DropResult(DropOutcome.Taken, hand, key, null);
        }

        /// <summary>A hand lifts a player piece. Lifting erases it at once: the cell empties and the clues update (4.2).</summary>
        public DropResult GrabFromCell(Hand hand, int x, int y)
        {
            var cell = (x, y);
            if (IsHolding(hand)) return Refuse(hand, RefuseReason.HandBusy, default, cell);
            if (!Board.InBounds(x, y)) return Refuse(hand, RefuseReason.OutsideBoard, default, cell);
            if (!(Board[x, y] is Piece piece)) return Refuse(hand, RefuseReason.EmptyCell, default, cell);
            if (piece.IsFixed) return Refuse(hand, RefuseReason.FixedPiece, piece.Key, cell);

            Board.TryErase(x, y);
            _held[(int)hand] = new HeldPiece(piece.Key, cell);
            return new DropResult(DropOutcome.Lifted, hand, piece.Key, cell, cell, boardChanged: true);
        }

        /// <summary>The ghost tint for a held piece over <paramref name="cell"/>. Never changes the board: clues update on release, not while hovering (4.3).</summary>
        public GhostTint Hover(Hand hand, (int X, int Y)? cell)
        {
            if (!(Held(hand) is HeldPiece held) || !cell.HasValue) return GhostTint.None;
            var (x, y) = cell.Value;
            if (!Board.InBounds(x, y)) return GhostTint.None;
            return CanLand(held.Key, x, y) ? GhostTint.Legal : GhostTint.Illegal;
        }

        /// <summary>
        /// A hand lets go over <paramref name="cell"/>, or off the platform when it is null or outside the grid.
        /// <paramref name="thrown"/> comes from the hand speed; a throw removes the piece wherever it happens (X10).
        /// </summary>
        public DropResult Release(Hand hand, (int X, int Y)? cell, bool thrown)
        {
            if (!(Held(hand) is HeldPiece held)) return Refuse(hand, RefuseReason.NothingHeld, default, cell);
            _held[(int)hand] = null;
            var key = held.Key;
            var origin = held.Origin;

            if (thrown) return new DropResult(DropOutcome.Thrown, hand, key, cell, origin);
            if (!cell.HasValue || !Board.InBounds(cell.Value.X, cell.Value.Y))
                return new DropResult(DropOutcome.Puffed, hand, key, null, origin);

            var (x, y) = cell.Value;
            if (!(Board[x, y] is Piece existing))
            {
                if (!Board.TryPlace(x, y, key)) return SendBack(hand, key, cell, origin);
                var moved = origin.HasValue && !origin.Value.Equals(cell.Value);
                return new DropResult(moved ? DropOutcome.Moved : DropOutcome.Placed, hand, key, cell, origin, boardChanged: true);
            }

            if (!CanLand(key, x, y)) return SendBack(hand, key, cell, origin);
            Board.TryErase(x, y);
            if (!Board.TryPlace(x, y, key))
                throw new InvalidOperationException($"{key} was legal at ({x},{y}) once {existing.Key} was gone, but did not place.");
            return new DropResult(DropOutcome.Replaced, hand, key, cell, origin, existing.Key, boardChanged: true);
        }

        /// <summary>
        /// Whether releasing <paramref name="key"/> over the cell would place, move or replace: an empty cell where the key
        /// is legal, or a player piece whose cell takes the key once that piece is gone. Never over a fixed piece.
        /// </summary>
        public bool CanLand(PieceKey key, int x, int y)
        {
            if (!(Board[x, y] is Piece existing)) return Legality.IsLegal(Board, x, y, key);
            if (existing.IsFixed) return false;
            var trial = Board.Clone();
            trial.TryErase(x, y);
            return Legality.IsLegal(trial, x, y, key);
        }

        /// <summary>
        /// An illegal drop never costs the player anything: the piece goes back where it came from. A lifted piece is
        /// re-seated in its cell if that is still legal, and puffs if the board has changed around it since.
        /// </summary>
        private DropResult SendBack(Hand hand, PieceKey key, (int X, int Y)? cell, (int X, int Y)? origin)
        {
            if (!origin.HasValue) return new DropResult(DropOutcome.Returned, hand, key, cell, illegalDrop: true);
            var (ox, oy) = origin.Value;
            return Board.TryPlace(ox, oy, key)
                ? new DropResult(DropOutcome.Returned, hand, key, cell, origin, illegalDrop: true, boardChanged: true)
                : new DropResult(DropOutcome.Puffed, hand, key, cell, origin, illegalDrop: true);
        }

        private static DropResult Refuse(Hand hand, RefuseReason reason, PieceKey key, (int X, int Y)? cell) =>
            new DropResult(DropOutcome.Refused, hand, key, cell, refuseReason: reason);
    }
}
