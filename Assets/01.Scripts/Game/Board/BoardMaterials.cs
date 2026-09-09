using UnityEngine;
using UnityEngine.Rendering;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Runtime materials for the placeholder board. They clone the render pipeline's default material so they work
    /// under URP without material assets; the art pass replaces them.
    /// </summary>
    public static class BoardMaterials
    {
        private static Material _tile;
        private static Material _tileAlt;
        private static Material _fixedPiece;
        private static Material _entrance;
        private static Material _exit;

        public static Material Tile => _tile != null ? _tile : _tile = Create("Board Tile", new Color(0.78f, 0.80f, 0.74f));
        public static Material TileAlt => _tileAlt != null ? _tileAlt : _tileAlt = Create("Board Tile Alt", new Color(0.70f, 0.73f, 0.66f));
        public static Material FixedPiece => _fixedPiece != null ? _fixedPiece : _fixedPiece = Create("Fixed Piece", new Color(0.22f, 0.22f, 0.25f));
        public static Material Entrance => _entrance != null ? _entrance : _entrance = Create("Entrance", new Color(0.22f, 0.60f, 0.32f));
        public static Material Exit => _exit != null ? _exit : _exit = Create("Exit", new Color(0.85f, 0.45f, 0.18f));

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
