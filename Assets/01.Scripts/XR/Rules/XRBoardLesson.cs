namespace TrainSudoku.XR.Rules
{
    /// <summary>Where the board lesson has got to.</summary>
    public enum XRBoardLessonStep
    {
        /// <summary>Not running: never begun, finished, or dropped.</summary>
        Idle,

        /// <summary>Ghost hands wait at the rail for the player to come to them.</summary>
        Approach,

        /// <summary>A hand is near: the ghost hands pinch, and the callout asks for both hands.</summary>
        Pinch,

        /// <summary>Both hands hold the rail: move them to carry the board, spread or close them to resize it.</summary>
        Carry,

        /// <summary>The closing card: where Re-place board lives. It ends on its button.</summary>
        Closing,
    }

    /// <summary>
    /// The lesson in moving the board (XR-PRD 7): shown once, at the start of the tutorial station, before the rules
    /// briefing. Nothing on the platform says its rail carries and resizes it in two hands, or that the wrist menu can
    /// place it again, so the first station opens by showing both. The shell feeds it what the hands are doing and
    /// draws what it asks for; it holds no engine types, and the distances that make a hand "near" are the shell's.
    /// </summary>
    /// <remarks>
    /// Skipping goes to the closing card rather than out: a player who could not manage the two-hand pinch is the one
    /// who most needs to know the board can be placed again.
    /// </remarks>
    public sealed class XRBoardLesson
    {
        private bool _carried;

        public XRBoardLessonStep Step { get; private set; } = XRBoardLessonStep.Idle;

        /// <summary>Running: the pieces stay locked until it ends.</summary>
        public bool Active => Step != XRBoardLessonStep.Idle;

        /// <summary>The ghost hands are on show, and whether they are pinching the rail or waiting open beside it.</summary>
        public bool HandsShown => Step == XRBoardLessonStep.Approach || Step == XRBoardLessonStep.Pinch;
        public bool HandsPinching => Step == XRBoardLessonStep.Pinch;

        /// <summary>The line the callout over the board says, or null when the signboard alone speaks.</summary>
        public string Key
        {
            get
            {
                switch (Step)
                {
                    case XRBoardLessonStep.Approach: return XRTutorialKeys.MoveApproach;
                    case XRBoardLessonStep.Pinch: return XRTutorialKeys.MovePinch;
                    case XRBoardLessonStep.Carry: return XRTutorialKeys.MoveCarry;
                    default: return null;
                }
            }
        }

        public void Begin()
        {
            Step = XRBoardLessonStep.Approach;
            _carried = false;
        }

        /// <summary>Drops the lesson without finishing it: the player left the station. It shows again next time.</summary>
        public void End() => Step = XRBoardLessonStep.Idle;

        /// <summary>What the hands are doing this frame. True when the step changed.</summary>
        /// <param name="handNear">A hand is within reach of the rail.</param>
        /// <param name="handsOnRail">How many hands hold the rail.</param>
        /// <param name="boardMoving">The rail is carrying or resizing the board: the pair has left its dead zone.</param>
        public bool Observe(bool handNear, int handsOnRail, bool boardMoving)
        {
            var before = Step;
            switch (Step)
            {
                case XRBoardLessonStep.Approach:
                    if (handsOnRail >= 2) Hold(boardMoving);
                    else if (handNear || handsOnRail > 0) Step = XRBoardLessonStep.Pinch;
                    break;

                case XRBoardLessonStep.Pinch:
                    if (handsOnRail >= 2) Hold(boardMoving);
                    else if (!handNear && handsOnRail == 0) Step = XRBoardLessonStep.Approach;
                    break;

                case XRBoardLessonStep.Carry:
                    if (boardMoving) _carried = true;
                    // Let go: done if the board went anywhere, otherwise the pinch is asked for again.
                    if (handsOnRail < 2) Step = _carried ? XRBoardLessonStep.Closing : XRBoardLessonStep.Pinch;
                    break;
            }

            return Step != before;
        }

        private void Hold(bool boardMoving)
        {
            Step = XRBoardLessonStep.Carry;
            _carried = boardMoving;
        }

        /// <summary>SKIP on the signboard: straight to the closing card. True when it did anything.</summary>
        public bool Skip()
        {
            if (!Active || Step == XRBoardLessonStep.Closing) return false;
            Step = XRBoardLessonStep.Closing;
            return true;
        }

        /// <summary>The closing card's button. True when the lesson ended on it, which is when it counts as seen.</summary>
        public bool Acknowledge()
        {
            if (Step != XRBoardLessonStep.Closing) return false;
            Step = XRBoardLessonStep.Idle;
            return true;
        }
    }
}
