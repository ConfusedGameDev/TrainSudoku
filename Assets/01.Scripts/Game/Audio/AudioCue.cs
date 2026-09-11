namespace TrainSudoku.Game
{
    /// <summary>Sound hooks (PRD section 11). UI cues are wired to the pack in Assets/05.Audio/UI; game cues get their content later.</summary>
    public enum AudioCue
    {
        UiClick = 0,
        UiBack = 1,
        UiConfirm = 2,
        Place = 3,
        Erase = 4,
        Error = 5,

        /// <summary>The last rail lands and the board is solved. The fanfare that follows is the arrival screen's.</summary>
        FinalPiece = 6,

        /// <summary>The departure whistle, once, as the train pulls away. <see cref="TrainMoving"/> is the loop under it.</summary>
        TrainStart = 7,

        // Added with the station UI (work order 10). Values are appended, never inserted, because the cue library
        // asset maps clips by enum value.
        MapOpen = 8,
        StationSelect = 9,
        LineUnlocked = 10,
        StarAwarded = 11,

        // Added with the audio layer (M22). Appended, for the same reason.

        /// <summary>A cell is selected. Silent before M22, and a haptic tick only.</summary>
        CellSelect = 12,

        /// <summary>A side is chosen, narrowing the piece. Silent before M22, and a haptic tick only.</summary>
        SideChosen = 13,

        /// <summary>Looped for the length of the run.</summary>
        TrainMoving = 14,

        /// <summary>Looped while a piece is held down to erase it; its pitch rides the progress ring.</summary>
        EraseHold = 15,

        /// <summary>A row or column just reached its clue. At most once per placement, and never on the winning one.</summary>
        LineCleared = 16,

        WinFanfareOne = 17,
        WinFanfareTwo = 18,
        WinFanfareThree = 19,
    }
}
