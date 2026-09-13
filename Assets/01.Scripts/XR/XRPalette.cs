using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// Every colour and font the XR edition draws with: its single re-skin point (XR-PRD 8, X20). The values start as a
    /// copy of the phone's station-signage tokens and platform values. They are a fork: never kept in sync with the
    /// phone's <c>Palette</c>, and the phone's is never referenced from XR code.
    /// </summary>
    public static class XRPalette
    {
        // Station signage (Docs/UIDesign.MD section 6).
        public static readonly Color Paper = new Color32(0xF4, 0xF5, 0xF2, 0xFF);
        public static readonly Color Ink = new Color32(0x1F, 0x23, 0x21, 0xFF);
        public static readonly Color ClosedLight = new Color32(0xB9, 0xC0, 0xBC, 0xFF);
        public static readonly Color Led = new Color32(0xFF, 0xB0, 0x20, 0xFF);
        public static readonly Color LedGround = new Color32(0x0D, 0x0F, 0x0E, 0xFF);
        public static readonly Color Warn = new Color32(0xF2, 0xC2, 0x30, 0xFF);
        public static readonly Color Stop = new Color32(0xD0, 0x34, 0x2C, 0xFF);

        // The platform.
        public static readonly Color Concrete = new Color32(0xC4, 0xC8, 0xC0, 0xFF);
        public static readonly Color ConcreteAlt = new Color32(0xB6, 0xBB, 0xB3, 0xFF);
        public static readonly Color Steel = new Color32(0x7C, 0x82, 0x85, 0xFF);

        /// <summary>A clue's numeral once its line holds more pieces than it asks for.</summary>
        public static readonly Color ClueExceeded = Stop;

        // Grab feedback (XR-PRD 4.3): the ghost over a cell, translucent so the slab and its neighbours show through.
        /// <summary>Releasing here would place, move or replace: the phone's success green.</summary>
        public static readonly Color GhostLegal = new Color(0.35f, 0.80f, 0.45f, 0.55f);

        /// <summary>Releasing here would send the piece back: <see cref="Stop"/>, translucent.</summary>
        public static readonly Color GhostIllegal = new Color(0.816f, 0.204f, 0.173f, 0.55f);

        /// <summary>Steam (X19): off-white, so a puff reads against a light table as well as a dark one.</summary>
        public static readonly Color Steam = new Color(0.94f, 0.95f, 0.93f, 0.85f);

        private static Font _font;

        /// <summary>The face for the world-space <c>TextMesh</c> clue numerals, which need a legacy font.</summary>
        public static Font Font => _font != null ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
