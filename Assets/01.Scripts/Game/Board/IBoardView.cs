using System;

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

        /// <summary>Shows a level with only its fixed pieces. Also used for Retry.</summary>
        void Load(LevelDefinition level);

        /// <summary>Whether taps reach the board. Off while the pause, train run and win screens are up.</summary>
        void SetInteractable(bool interactable);
    }
}
