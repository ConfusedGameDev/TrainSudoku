using UnityEngine;
using UnityEngine.Rendering;

namespace TrainSudoku.XR
{
    /// <summary>
    /// Runtime materials for the XR board. They clone the render pipeline's default material, so they need no material
    /// assets, and every colour comes from <see cref="XRPalette"/>. The satisfied clue sign wears the active line's
    /// colour, a runtime value that arrives through <see cref="SetLineColour"/>.
    /// </summary>
    public static class XRBoardMaterials
    {
        private static Material _tile;
        private static Material _tileAlt;
        private static Material _clueChip;
        private static Material _clueChipEdge;
        private static Material _clueChipIdle;
        private static Material _clueChipIdleEdge;
        private static Material _clueChipError;
        private static Material _signPole;
        private static Material _tunnelInterior;
        private static Material _track;
        private static Material _fixedTrack;
        private static Material _entrance;
        private static Material _exit;
        private static Material _platformEdge;
        private static Color _lineColour = XRPalette.Warn;

        public static Material Tile => _tile != null ? _tile : _tile = Create("XR Tile", XRPalette.Concrete);
        public static Material TileAlt => _tileAlt != null ? _tileAlt : _tileAlt = Create("XR Tile Alt", XRPalette.ConcreteAlt);

        /// <summary>A satisfied clue: filled in the line colour and ringed in a deeper shade of it.</summary>
        public static Material ClueChip => _clueChip != null ? _clueChip : _clueChip = Create("XR Clue", _lineColour);
        public static Material ClueChipEdge => _clueChipEdge != null ? _clueChipEdge : _clueChipEdge = Create("XR Clue Edge", Darken(_lineColour));

        public static Material ClueChipIdle => _clueChipIdle != null ? _clueChipIdle : _clueChipIdle = Create("XR Clue Idle", XRPalette.Paper);
        public static Material ClueChipIdleEdge => _clueChipIdleEdge != null ? _clueChipIdleEdge : _clueChipIdleEdge = Create("XR Clue Idle Edge", XRPalette.ClosedLight);
        public static Material ClueChipError => _clueChipError != null ? _clueChipError : _clueChipError = Create("XR Clue Error", XRPalette.ClueExceeded);
        public static Material SignPole => _signPole != null ? _signPole : _signPole = Create("XR Sign Pole", XRPalette.Steel);

        /// <summary>The darkness at the far end of a tunnel, and the fallback portal's hole.</summary>
        public static Material TunnelInterior => _tunnelInterior != null ? _tunnelInterior : _tunnelInterior = Create("XR Tunnel Interior", XRPalette.LedGround);

        public static Material Track => _track != null ? _track : _track = Create("XR Track", XRPalette.Steel);

        /// <summary>Fixed track is flat ink, never line-tinted: a pre-laid piece reads as a given.</summary>
        public static Material FixedTrack => _fixedTrack != null ? _fixedTrack : _fixedTrack = Create("XR Fixed Track", XRPalette.Ink);

        public static Material Entrance => _entrance != null ? _entrance : _entrance = Create("XR Entrance", XRPalette.Ink);
        public static Material Exit => _exit != null ? _exit : _exit = Create("XR Exit", XRPalette.Stop);
        public static Material PlatformEdge => _platformEdge != null ? _platformEdge : _platformEdge = Create("XR Platform Edge", XRPalette.Warn);

        /// <summary>Tints the satisfied clue signs in the active line's colour.</summary>
        public static void SetLineColour(Color colour)
        {
            _lineColour = colour;
            if (_clueChip != null) _clueChip.color = colour;
            if (_clueChipEdge != null) _clueChipEdge.color = Darken(colour);
        }

        private static Color Darken(Color colour) => new Color(colour.r * 0.72f, colour.g * 0.72f, colour.b * 0.72f, colour.a);

        private static Material Create(string name, Color color)
        {
            // On the occlusion shader when one is configured, so the player's real body hides the board (5.6).
            var occluded = XROcclusionMaterials.Lit(name, color);
            if (occluded != null) return occluded;

            var pipeline = GraphicsSettings.currentRenderPipeline;
            var template = pipeline != null ? pipeline.defaultMaterial : null;
            var material = template != null
                ? new Material(template)
                : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            material.name = name;
            material.color = color;
            return material;
        }
    }
}
