using UnityEngine;
using UnityEngine.Rendering;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Runtime materials for the board. They clone the render pipeline's default material so they work under URP
    /// without material assets, and every colour comes from <see cref="Palette"/>: concrete slabs, steel rails, a
    /// yellow platform edge under the selected cell and a red exit portal (work order 5.4).
    /// </summary>
    /// <remarks>
    /// Two of them are <b>line-tinted</b> rather than constant — fixed pieces and the forced marker — because the
    /// work order asks for line-colour fixed pieces and a marker that hops in <c>--line-current</c>. The line colour
    /// is a runtime value, so <see cref="SetLineColour"/> is how it arrives; the constants below are only what those
    /// two look like before any line has been chosen.
    /// </remarks>
    public static class BoardMaterials
    {
        private static Material _tile;
        private static Material _tileAlt;
        private static Material _tileSelected;
        private static Material _fixedPiece;
        private static Material _track;
        private static Material _fixedTrack;
        private static Material _entrance;
        private static Material _exit;
        private static Material _marker;
        private static Material _markerForced;
        private static Material _hold;
        private static Material _platformEdge;
        private static Color _lineColour = Palette.Warn;

        /// <summary>A platform slab.</summary>
        public static Material Tile => _tile != null ? _tile : _tile = Create("Board Tile", Palette.Concrete);

        /// <summary>The alternating slab, so the grid reads without drawn gridlines.</summary>
        public static Material TileAlt => _tileAlt != null ? _tileAlt : _tileAlt = Create("Board Tile Alt", Palette.ConcreteAlt);

        /// <summary>The selected cell, in the platform-edge yellow.</summary>
        public static Material TileSelected => _tileSelected != null ? _tileSelected : _tileSelected = Create("Board Tile Selected", Palette.Warn);

        /// <summary>The dark inside of a tunnel mouth.</summary>
        public static Material FixedPiece => _fixedPiece != null ? _fixedPiece : _fixedPiece = Create("Tunnel Interior", Palette.LedGround);

        public static Material Track => _track != null ? _track : _track = Create("Track", Palette.Steel);

        /// <summary>Fixed track, in the active line's colour so a pre-laid piece reads as part of the line.</summary>
        public static Material FixedTrack => _fixedTrack != null ? _fixedTrack : _fixedTrack = Create("Fixed Track", _lineColour);

        public static Material Entrance => _entrance != null ? _entrance : _entrance = Create("Entrance", Palette.Ink);

        /// <summary>The exit portal, in signage red.</summary>
        public static Material Exit => _exit != null ? _exit : _exit = Create("Exit", Palette.Stop);

        public static Material Marker => _marker != null ? _marker : _marker = Create("Marker", Palette.Paper);

        /// <summary>The only-legal-choice marker, in the active line's colour (work order 9, marker hop).</summary>
        public static Material MarkerForced => _markerForced != null ? _markerForced : _markerForced = Create("Marker Forced", _lineColour);

        /// <summary>The long-press erase ring, in the platform-edge yellow.</summary>
        public static Material Hold => _hold != null ? _hold : _hold = Create("Hold Ring", Palette.Warn);

        /// <summary>The yellow tactile strip along the platform edge (D13 environment art).</summary>
        public static Material PlatformEdge => _platformEdge != null ? _platformEdge : _platformEdge = Create("Platform Edge", Palette.Warn);

        /// <summary>
        /// Publishes the active line's colour to the two materials that carry it. The board's counterpart of the
        /// shell's tint pass, and the reason no line colour is ever written as a constant.
        /// </summary>
        public static void SetLineColour(Color colour)
        {
            _lineColour = colour;
            if (_fixedTrack != null) _fixedTrack.color = colour;
            if (_markerForced != null) _markerForced.color = colour;
        }

        private static Material Create(string name, Color color)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            var template = pipeline != null ? pipeline.defaultMaterial : null;
            Material material;
            if (template != null)
            {
                material = new Material(template);
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
            }

            material.name = name;
            material.color = color;
            return material;
        }
    }
}
