using System;

namespace TrainSudoku.XR.Rules
{
    /// <summary>What Pip, the cat conductor, is doing: which body she wears and how she moves.</summary>
    public enum XRPipMood
    {
        Idle,
        Wave,
        Cheer,
        Worry,
        Sleep,
    }

    /// <summary>Rows of the `XR` String Table that Pip speaks, on the signboard in play.</summary>
    public static class XRPipKeys
    {
        public const string Start = "xr.pip.start";
        public const string RowClear = "xr.pip.row_clear";
        public const string ColumnClear = "xr.pip.col_clear";
        public const string RowOver = "xr.pip.row_over";
        public const string ColumnOver = "xr.pip.col_over";
    }

    /// <summary>
    /// Pip's reactions in play, as on the web edition (<c>game/pipReactions.ts</c>): no game logic, only a mapping from
    /// what the board already reports to a mood and a speech bubble, with timers. If two events land inside one
    /// reaction window the later one wins and restarts the window.
    /// </summary>
    /// <remarks>
    /// Time is passed in (<see cref="Update"/>), never read, so every timing is testable without a clock. The web's
    /// hint and idle nudge are not here: XR v1 has no hints (X15).
    /// </remarks>
    public sealed class XRPipReactions
    {
        /// <summary>How long a clue reaction lasts before Pip settles and the bubble goes, in seconds.</summary>
        public const double ReactionSeconds = 1.8;

        /// <summary>How long the start-of-station wave lasts, in seconds.</summary>
        public const double StartSeconds = 2.5;

        private double _revertIn;
        private bool _active;

        public XRPipMood Mood { get; private set; } = XRPipMood.Idle;

        /// <summary>The bubble's row in the String Table, or null when Pip has nothing to say.</summary>
        public string BubbleKey { get; private set; }

        /// <summary>The 1-based row or column the bubble names, or 0 when its line takes no number.</summary>
        public int BubbleNumber { get; private set; }

        /// <summary>The mood or the bubble changed.</summary>
        public event Action Changed;

        /// <summary>A station starts: Pip waves and says so, then settles.</summary>
        public void Begin()
        {
            _active = true;
            Show(XRPipMood.Wave, XRPipKeys.Start, 0, StartSeconds);
        }

        /// <summary>A row or column has just become satisfied. <paramref name="index"/> is 0-based.</summary>
        public void LineCleared(bool isRow, int index) =>
            Show(XRPipMood.Cheer, isRow ? XRPipKeys.RowClear : XRPipKeys.ColumnClear, index + 1, ReactionSeconds);

        /// <summary>A row or column has just gone over its clue. <paramref name="index"/> is 0-based.</summary>
        public void LineExceeded(bool isRow, int index) =>
            Show(XRPipMood.Worry, isRow ? XRPipKeys.RowOver : XRPipKeys.ColumnOver, index + 1, ReactionSeconds);

        /// <summary>Leaving play: back to a quiet Pip with nothing to say.</summary>
        public void Stop()
        {
            _active = false;
            _revertIn = 0;
            Set(XRPipMood.Idle, null, 0);
        }

        /// <summary>Advances the timers by <paramref name="dt"/> seconds.</summary>
        public void Update(double dt)
        {
            if (!_active || dt <= 0 || _revertIn <= 0) return;
            _revertIn -= dt;
            if (_revertIn > 0) return;
            _revertIn = 0;
            Set(XRPipMood.Idle, null, 0);
        }

        private void Show(XRPipMood mood, string key, int number, double seconds)
        {
            if (!_active) return;
            _revertIn = seconds;
            Set(mood, key, number);
        }

        private void Set(XRPipMood mood, string key, int number)
        {
            var same = mood == Mood && key == BubbleKey && number == BubbleNumber;
            Mood = mood;
            BubbleKey = key;
            BubbleNumber = number;
            if (!same) Changed?.Invoke();
        }
    }
}
