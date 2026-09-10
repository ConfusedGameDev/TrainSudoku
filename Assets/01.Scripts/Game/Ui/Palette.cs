using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The single place colour and font live. The 3D board reads from here (it used to reach into
    /// <see cref="UiBuilder"/>, which is deleted once the UI Toolkit screens land), and the USS token sheet is
    /// generated from the same values, so a re-skin is an edit to this file.
    /// </summary>
    /// <remarks>
    /// Two disjoint sets live here and they must not be confused:
    /// <list type="bullet">
    /// <item>The station-signage tokens (<see cref="Paper"/> down to <see cref="Stop"/>) are the values from the
    /// UI work order section 6. Nothing reads them yet; the shell picks them up as USS variables.</item>
    /// <item>The board values (<see cref="BoardBackground"/> down to <see cref="ClueText"/>) are carried over from
    /// <c>UiBuilder</c> unchanged, so the playfield renders exactly as it did before the move.</item>
    /// </list>
    /// The line colour is deliberately absent: it is a runtime value published as the USS variable
    /// <c>--line-current</c> from the active line, never a constant.
    /// </remarks>
    public static class Palette
    {
        // ---- Station signage tokens (work order section 6). Not yet read by anything. ----

        /// <summary>Screen ground. <c>--paper</c>.</summary>
        public static readonly Color Paper = new Color32(0xF4, 0xF5, 0xF2, 0xFF);

        /// <summary>Text, signage panels and the 4 px object borders. <c>--ink</c>.</summary>
        public static readonly Color Ink = new Color32(0x1F, 0x23, 0x21, 0xFF);

        /// <summary>Secondary text. <c>--ink-dim</c>.</summary>
        public static readonly Color InkDim = new Color32(0x5A, 0x61, 0x5D, 0xFF);

        /// <summary>Locked stations and lines. <c>--closed</c>.</summary>
        public static readonly Color Closed = new Color32(0x9A, 0xA3, 0xA0, 0xFF);

        /// <summary>The lighter half of the closed pair, for locked fills under <see cref="Closed"/> strokes.</summary>
        public static readonly Color ClosedLight = new Color32(0xB9, 0xC0, 0xBC, 0xFF);

        /// <summary>Ticker and clock glyphs. <c>--led</c>.</summary>
        public static readonly Color Led = new Color32(0xFF, 0xB0, 0x20, 0xFF);

        /// <summary>The strip behind <see cref="Led"/>. <c>--led-ground</c>.</summary>
        public static readonly Color LedGround = new Color32(0x0D, 0x0F, 0x0E, 0xFF);

        /// <summary>Platform edge and erase ring. <c>--warn</c>.</summary>
        public static readonly Color Warn = new Color32(0xF2, 0xC2, 0x30, 0xFF);

        /// <summary>Exit portal and error. <c>--stop</c>.</summary>
        public static readonly Color Stop = new Color32(0xD0, 0x34, 0x2C, 0xFF);

        // ---- Board values, carried over unchanged. Re-tinted to the platform palette later. ----

        /// <summary>The camera's solid clear colour behind the board.</summary>
        public static readonly Color BoardBackground = new Color(0.09f, 0.11f, 0.15f);

        /// <summary>A row or column clue whose count is satisfied.</summary>
        public static readonly Color Success = new Color(0.35f, 0.80f, 0.45f);

        /// <summary>A row or column holding more pieces than its clue allows.</summary>
        public static readonly Color ClueExceeded = new Color(0.95f, 0.40f, 0.35f);

        /// <summary>A clue that is neither satisfied nor exceeded, and the tunnel letters.</summary>
        public static readonly Color ClueText = new Color(0.96f, 0.96f, 0.97f);

        private static Font _font;

        /// <summary>
        /// The face used by the world-space <c>TextMesh</c> clue and tunnel labels. Those are 3D objects, not
        /// widgets, so they need a legacy <see cref="UnityEngine.Font"/> rather than a UI Toolkit font asset.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }
    }
}
