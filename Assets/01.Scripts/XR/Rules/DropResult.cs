using TrainSudoku.Core;

namespace TrainSudoku.XR.Rules
{
    /// <summary>The player's hands. Each holds at most one piece, and both may hold at once (XR-PRD 4.2).</summary>
    public enum Hand
    {
        Left,
        Right,
    }

    /// <summary>What a grab or a release did (XR-PRD 4.4). The board display plays it; the XR tutorial coach reads it.</summary>
    public enum DropOutcome
    {
        /// <summary>Nothing happened. <see cref="DropResult.RefuseReason"/> says why.</summary>
        Refused,
        /// <summary>A tray piece is now in the hand.</summary>
        Taken,
        /// <summary>A player piece was lifted off the board: its cell is empty and the piece is in the hand.</summary>
        Lifted,
        /// <summary>The piece landed on an empty cell.</summary>
        Placed,
        /// <summary>A piece lifted from one cell landed on another.</summary>
        Moved,
        /// <summary>The piece landed on a player piece, which puffed.</summary>
        Replaced,
        /// <summary>An illegal drop: the piece flew back to the tray, or back into the cell it was lifted from.</summary>
        Returned,
        /// <summary>The piece is gone in a small puff: released off the platform, or an illegal drop whose cell of origin no longer takes it.</summary>
        Puffed,
        /// <summary>The piece was thrown and bursts into steam. It is removed just as a puff removes it (X10).</summary>
        Thrown,
    }

    /// <summary>Why a grab or a release was <see cref="DropOutcome.Refused"/>.</summary>
    public enum RefuseReason
    {
        None,
        /// <summary>The hand already holds a piece.</summary>
        HandBusy,
        /// <summary>A release from a hand that holds nothing.</summary>
        NothingHeld,
        /// <summary>A grab aimed outside the grid.</summary>
        OutsideBoard,
        /// <summary>A grab aimed at an empty cell.</summary>
        EmptyCell,
        /// <summary>A grab aimed at a fixed piece, which wobbles and plays the "that's fixed" note.</summary>
        FixedPiece,
    }

    /// <summary>The ghost's tint while a held piece hovers over the platform (XR-PRD 4.3).</summary>
    public enum GhostTint
    {
        /// <summary>No ghost: nothing held, or not over a cell.</summary>
        None,
        /// <summary>Releasing here would place, move or replace.</summary>
        Legal,
        /// <summary>Releasing here would send the piece back.</summary>
        Illegal,
    }

    /// <summary>A piece in a hand, remembering where it came from.</summary>
    public readonly struct HeldPiece
    {
        public PieceKey Key { get; }

        /// <summary>The cell the piece was lifted from, or null for a tray piece.</summary>
        public (int X, int Y)? Origin { get; }

        public bool FromTray => !Origin.HasValue;

        public HeldPiece(PieceKey key, (int X, int Y)? origin)
        {
            Key = key;
            Origin = origin;
        }

        public override string ToString() => FromTray ? $"{Key} from the tray" : $"{Key} from {Origin}";
    }

    /// <summary>The result of one <see cref="PieceDrop"/> call.</summary>
    public readonly struct DropResult
    {
        public DropOutcome Outcome { get; }
        public Hand Hand { get; }

        /// <summary>The piece grabbed or released; the default key when a grab found no piece or a release found an empty hand.</summary>
        public PieceKey Key { get; }

        /// <summary>The cell the call aimed at: the grabbed cell, the landing cell, or the target of an illegal drop. Null for a tray grab or off the platform.</summary>
        public (int X, int Y)? Cell { get; }

        /// <summary>The cell a released or lifted piece came from, or null for a tray piece.</summary>
        public (int X, int Y)? Origin { get; }

        /// <summary>For <see cref="DropOutcome.Replaced"/>, the player piece that puffed.</summary>
        public PieceKey? ReplacedKey { get; }

        /// <summary>The release was an illegal drop: <see cref="DropOutcome.Returned"/>, or <see cref="DropOutcome.Puffed"/> because the way back was closed.</summary>
        public bool IllegalDrop { get; }

        /// <summary>The board changed, so the validator runs and the clues update.</summary>
        public bool BoardChanged { get; }

        public RefuseReason RefuseReason { get; }

        internal DropResult(DropOutcome outcome, Hand hand, PieceKey key, (int X, int Y)? cell, (int X, int Y)? origin = null,
            PieceKey? replacedKey = null, bool illegalDrop = false, bool boardChanged = false, RefuseReason refuseReason = RefuseReason.None)
        {
            Outcome = outcome;
            Hand = hand;
            Key = key;
            Cell = cell;
            Origin = origin;
            ReplacedKey = replacedKey;
            IllegalDrop = illegalDrop;
            BoardChanged = boardChanged;
            RefuseReason = refuseReason;
        }

        public override string ToString() =>
            Outcome == DropOutcome.Refused
                ? $"{Hand}: Refused ({RefuseReason}) at {Cell}"
                : $"{Hand}: {Outcome} {Key} at {Cell} from {(Origin.HasValue ? Origin.ToString() : "tray")}";
    }
}
