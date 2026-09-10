using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The single place colour and font live. The 3D board reads from here — it used to reach into the uGUI widget
    /// factory, which is now gone — and <c>Uss/tokens.uss</c> mirrors the same values, so a re-skin is an edit to
    /// this file and that sheet.
    /// </summary>
    /// <remarks>
    /// Two disjoint sets live here and they must not be confused:
    /// <list type="bullet">
    /// <item>The station-signage tokens (<see cref="Paper"/> down to <see cref="Stop"/>) are the values from the
    /// UI work order section 6, mirrored by <c>Uss/tokens.uss</c> for the screens.</item>
    /// <item>The board values (<see cref="BoardBackground"/> down to <see cref="ClueText"/>) are the platform: the
    /// same tokens applied to a 3D scene, so ballast, concrete and steel are named here rather than in
    /// <see cref="BoardMaterials"/>.</item>
    /// </list>
    /// The line colour is deliberately absent: it is a runtime value published as the USS variable
    /// <c>--line-current</c> from the active line, never a constant.
    /// </remarks>
    public static class Palette
    {
        // ---- Station signage tokens (work order section 6). ----

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

        // ---- The platform: the board's own colours, re-tinted to the station palette at M19 (5.4). ----

        /// <summary>
        /// The camera's solid clear colour behind the board: ballast, the crushed stone a track is laid on. Dark
        /// enough that the concrete slabs and the amber markers read against it under the 60 degree pitch.
        /// </summary>
        public static readonly Color BoardBackground = new Color32(0x26, 0x2A, 0x28, 0xFF);

        /// <summary>A platform slab. The board is a station platform seen from above, not a sheet of paper.</summary>
        public static readonly Color Concrete = new Color32(0xC4, 0xC8, 0xC0, 0xFF);

        /// <summary>The alternating slab, one shade down, so the grid reads without drawn gridlines.</summary>
        public static readonly Color ConcreteAlt = new Color32(0xB6, 0xBB, 0xB3, 0xFF);

        /// <summary>Rail steel: player track, and the stand-in train's chassis.</summary>
        public static readonly Color Steel = new Color32(0x7C, 0x82, 0x85, 0xFF);

        /// <summary>A row or column clue whose count is satisfied.</summary>
        public static readonly Color Success = new Color(0.35f, 0.80f, 0.45f);

        /// <summary>A row or column holding more pieces than its clue allows. The signage <see cref="Stop"/> red.</summary>
        public static readonly Color ClueExceeded = Stop;

        /// <summary>A clue that is neither satisfied nor exceeded, and the tunnel letters. Signage <see cref="Paper"/>.</summary>
        public static readonly Color ClueText = Paper;

        /// <summary>
        /// A neighbouring slab the selected cell may connect to. Muted against <see cref="Success"/>, because this is a
        /// whole slab rather than a glyph and a full-strength green would shout over the track sitting on it.
        /// </summary>
        public static readonly Color NeighbourOpen = new Color32(0x7C, 0xB8, 0x8A, 0xFF);

        /// <summary>A neighbouring slab the selected cell may not connect to. The muted counterpart of <see cref="Stop"/>.</summary>
        public static readonly Color NeighbourBlocked = new Color32(0xB8, 0x7C, 0x7C, 0xFF);

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
