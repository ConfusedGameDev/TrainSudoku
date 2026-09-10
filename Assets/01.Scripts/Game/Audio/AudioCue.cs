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
        Win = 6,
        TrainStart = 7,

        // Added with the station UI (work order 10). Values are appended, never inserted, because the cue library
        // asset maps clips by enum value.
        MapOpen = 8,
        StationSelect = 9,
        LineUnlocked = 10,
        StarAwarded = 11,
    }
}
