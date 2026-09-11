using System;
using TrainSudoku.Core;

namespace TrainSudoku.Game
{
    /// <summary>
    /// What the game flow needs from the board on screen. M2 ships a stub; M3 and M4 replace it with the 3D board.
    /// </summary>
    public interface IBoardView
    {
        /// <summary>The player tapped the board. The first one starts the clock.</summary>
        event Action Interacted;

        /// <summary>The board is in a winning state.</summary>
        event Action Completed;

        /// <summary>
        /// Shows a level. With a snapshot the saved player pieces are put back; with null only the fixed pieces show,
        /// which is also what Retry uses.
        /// </summary>
        void Load(LevelDefinition level, LevelProgress resume);

        /// <summary>Whether taps reach the board. Off while the pause, train run and win screens are up.</summary>
        void SetInteractable(bool interactable);

        /// <summary>
        /// How much track is on the board, fixed pieces included. The pause and arrival screens read it out as
        /// "rails laid"; nothing decides anything with it.
        /// </summary>
        int PieceCount { get; }

        /// <summary>
        /// How much track a solved board holds: the clue totals, which <see cref="LevelData.Validate"/> guarantees
        /// agree across the two axes. The play screen counts against it; nothing decides anything with it either.
        /// </summary>
        int TotalRails { get; }
    }
}
