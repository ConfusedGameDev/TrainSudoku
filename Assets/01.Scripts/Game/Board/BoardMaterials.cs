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
    /// Three of them are <b>line-tinted</b> rather than constant — <see cref="TileForced"/>, the side the player must
    /// connect to, and the two that make up a satisfied clue chip — because the work order asks for those prompts in
    /// <c>--line-current</c>. The line colour is a runtime value, so <see cref="SetLineColour"/> is how it arrives;
    /// the constants below are only what they look like before any line has been chosen. Fixed track is deliberately <b>not</b> line-tinted: a pre-laid piece is a given, so
    /// it is flat ink whatever line it sits on.
    /// </remarks>
    public static class BoardMaterials
    {
        private static Material _tile;
        private static Material _tileAlt;
        private static Material _tileSelected;
        private static Material _tileOpen;
        private static Material _tileBlocked;
        private static Material _tileForced;
        private static Material _clueChip;
        private static Material _clueChipEdge;
        private static Material _clueChipIdle;
        private static Material _clueChipIdleEdge;
        private static Material _clueChipError;
        private static Material _fixedPiece;
        private static Material _track;
        private static Material _fixedTrack;
        private static Material _entrance;
        private static Material _exit;
        private static Material _hold;
        private static Material _guide;
        private static Material _platformEdge;
        private static Color _lineColour = Palette.Warn;
        private static Color _holdColour = Palette.Warn;

        /// <summary>A platform slab.</summary>
        public static Material Tile => _tile != null ? _tile : _tile = Create("Board Tile", Palette.Concrete);

        /// <summary>The alternating slab, so the grid reads without drawn gridlines.</summary>
        public static Material TileAlt => _tileAlt != null ? _tileAlt : _tileAlt = Create("Board Tile Alt", Palette.ConcreteAlt);

        /// <summary>The selected cell, in the platform-edge yellow.</summary>
        public static Material TileSelected => _tileSelected != null ? _tileSelected : _tileSelected = Create("Board Tile Selected", Palette.Warn);

        /// <summary>A neighbour the selected cell may connect to (PRD 3.3 "open").</summary>
        public static Material TileOpen => _tileOpen != null ? _tileOpen : _tileOpen = Create("Board Tile Open", Palette.NeighbourOpen);

        /// <summary>A neighbour the selected cell may not connect to: forbidden by the rules, or narrowed out by the first choice.</summary>
        public static Material TileBlocked => _tileBlocked != null ? _tileBlocked : _tileBlocked = Create("Board Tile Blocked", Palette.NeighbourBlocked);

        /// <summary>A neighbour the piece <i>must</i> connect to, in the active line's colour.</summary>
        public static Material TileForced => _tileForced != null ? _tileForced : _tileForced = Create("Board Tile Forced", _lineColour);

        /// <summary>A clue chip whose line is satisfied: filled in the active line, ringed in a darker shade of it.</summary>
        public static Material ClueChip => _clueChip != null ? _clueChip : _clueChip = Create("Clue Chip", _lineColour);

        public static Material ClueChipEdge => _clueChipEdge != null ? _clueChipEdge : _clueChipEdge = Create("Clue Chip Edge", Darken(_lineColour));

        /// <summary>A clue chip still to be satisfied: paper, ringed in the closed grey.</summary>
        public static Material ClueChipIdle => _clueChipIdle != null ? _clueChipIdle : _clueChipIdle = Create("Clue Chip Idle", Palette.Paper);

        public static Material ClueChipIdleEdge => _clueChipIdleEdge != null ? _clueChipIdleEdge : _clueChipIdleEdge = Create("Clue Chip Idle Edge", Palette.ClosedLight);

        /// <summary>A line holding more pieces than its clue allows.</summary>
        public static Material ClueChipError => _clueChipError != null ? _clueChipError : _clueChipError = Create("Clue Chip Error", Palette.ClueExceeded);

        /// <summary>The dark inside of a tunnel mouth.</summary>
        public static Material FixedPiece => _fixedPiece != null ? _fixedPiece : _fixedPiece = Create("Tunnel Interior", Palette.LedGround);

        public static Material Track => _track != null ? _track : _track = Create("Track", Palette.Steel);

        /// <summary>Fixed track: flat ink, so a pre-laid piece reads as a given rather than as line decoration.</summary>
        public static Material FixedTrack => _fixedTrack != null ? _fixedTrack : _fixedTrack = Create("Fixed Track", Palette.Ink);

        public static Material Entrance => _entrance != null ? _entrance : _entrance = Create("Entrance", Palette.Ink);

        /// <summary>The exit portal, in signage red.</summary>
        public static Material Exit => _exit != null ? _exit : _exit = Create("Exit", Palette.Stop);

        /// <summary>The long-press erase ring, in the platform-edge yellow unless the Game Manager overrides it.</summary>
        /// <summary>
        /// The tutorial's ring. Line-coloured, so the cell being pointed at and the callout's tail read as the
        /// same instruction rather than as two unrelated cues.
        /// </summary>
        public static Material Guide => _guide != null ? _guide : _guide = Create("Guide", _lineColour);

        public static Material Hold => _hold != null ? _hold : _hold = Create("Hold Ring", _holdColour);

        /// <summary>The yellow tactile strip along the platform edge (D13 environment art).</summary>
        public static Material PlatformEdge => _platformEdge != null ? _platformEdge : _platformEdge = Create("Platform Edge", Palette.Warn);

        /// <summary>
        /// Publishes the active line's colour to the materials that carry it: the forced side — marked on the
        /// neighbouring slab, or on an edge marker where the side leaves the board — and a satisfied clue chip. The
        /// board's counterpart of the shell's tint pass, and the reason no line colour is ever written as a constant.
        /// </summary>
        public static void SetLineColour(Color colour)
        {
            _lineColour = colour;
            if (_tileForced != null) _tileForced.color = colour;
            if (_clueChip != null) _clueChip.color = colour;
            if (_clueChipEdge != null) _clueChipEdge.color = Darken(colour);
            if (_guide != null) _guide.color = colour;
        }

        /// <summary>
        /// The ring around a satisfied clue chip. The artboard draws it as a deeper shade of the line rather than a
        /// colour of its own, which is the only way it keeps working when a second line ships in another hue.
        /// </summary>
        private static Color Darken(Color colour) => new Color(colour.r * 0.72f, colour.g * 0.72f, colour.b * 0.72f, colour.a);

        /// <summary>
        /// Overrides the erase ring's colour from the Game Manager. The one board colour that is deliberately tunable
        /// in the inspector, because it is a feedback cue tuned against whatever is under it rather than a skin value;
        /// a fully transparent colour means "leave the palette alone".
        /// </summary>
        public static void SetHoldColour(Color colour)
        {
            _holdColour = colour.a <= 0f ? Palette.Warn : colour;
            if (_hold != null) _hold.color = _holdColour;
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
