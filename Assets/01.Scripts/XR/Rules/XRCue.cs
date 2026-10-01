namespace TrainSudoku.XR.Rules
{
    /// <summary>
    /// XR's own cue set (XR-PRD 9, X24): every sound and buzz the headset plays, by what happened rather than by clip.
    /// The clip, volume and feel of each live on <c>03.Data/XR/Audio/XRCues.asset</c>.
    /// </summary>
    /// <remarks>
    /// <b>Append only.</b> The library asset stores each slot by this number, so inserting or reordering a value would
    /// hand every later slot the wrong clip. <c>XRCueTests</c> pins the numbers.
    /// </remarks>
    public enum XRCue
    {
        None = 0,

        // Pieces: played from the piece.
        Grab = 1,
        Place = 2,
        Move = 3,
        Replace = 4,
        Lift = 5,
        Error = 6,
        Steered = 7,
        Fixed = 8,
        ReturnLand = 9,
        Puff = 10,
        Burst = 11,
        Whistle = 12,

        // The board: played from the board.
        LineCleared = 13,
        FinalPiece = 14,
        TrainStart = 15,
        TrainLoop = 16,
        TrainHurry = 17,

        // Signs, the wrist and the map: played from the panel pressed.
        UiPress = 18,
        UiBack = 19,
        WristOpen = 20,
        MapSelect = 21,
        MapClosed = 22,
        BoardPlaced = 23,
        Star = 24,
        Stamp = 25,
        FanfareOne = 26,
        FanfareTwo = 27,
        FanfareThree = 28,
        TutorialNote = 29,
    }

    /// <summary>Which cue a grab or a release plays (XR-PRD 4.4, 7). Engine-free, so the whole table is unit-tested.</summary>
    public static class XRCueMap
    {
        /// <summary>
        /// The cue for what a <see cref="PieceDrop"/> call did. A steered return plays the tutorial's soft note, never the
        /// error cue (7); a refused grab of a fixed piece plays its "that's fixed" note (4.2); every other refusal is silent.
        /// A throw's burst and whistle, and a fall's puff, play later, when the piece comes to rest.
        /// </summary>
        public static XRCue For(DropResult result)
        {
            switch (result.Outcome)
            {
                case DropOutcome.Taken: return XRCue.Grab;
                case DropOutcome.Lifted: return XRCue.Lift;
                case DropOutcome.Placed: return XRCue.Place;
                case DropOutcome.Moved: return XRCue.Move;
                case DropOutcome.Replaced: return XRCue.Replace;
                case DropOutcome.Returned: return result.Steered ? XRCue.Steered : XRCue.Error;
                case DropOutcome.Puffed: return result.IllegalDrop ? XRCue.Error : XRCue.None;
                case DropOutcome.Thrown: return XRCue.None;
                case DropOutcome.Refused: return result.RefuseReason == RefuseReason.FixedPiece ? XRCue.Fixed : XRCue.None;
                default: return XRCue.None;
            }
        }

        /// <summary>The fanfare for a run's stars, as the phone's arrival plays it.</summary>
        public static XRCue Fanfare(int stars) =>
            stars >= 3 ? XRCue.FanfareThree : stars == 2 ? XRCue.FanfareTwo : XRCue.FanfareOne;

        /// <summary>Whether a cue is felt in the hand that caused it. Board, train and fanfare cues are heard only.</summary>
        public static bool IsFelt(XRCue cue)
        {
            switch (cue)
            {
                case XRCue.Grab:
                case XRCue.Place:
                case XRCue.Move:
                case XRCue.Replace:
                case XRCue.Lift:
                case XRCue.Error:
                case XRCue.Steered:
                case XRCue.Fixed:
                case XRCue.UiPress:
                case XRCue.UiBack:
                case XRCue.WristOpen:
                case XRCue.MapSelect:
                case XRCue.MapClosed:
                case XRCue.BoardPlaced:
                    return true;
                default:
                    return false;
            }
        }
    }
}
