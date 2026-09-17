using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The train's body colour, which is the active line's. The board's counterpart is <see cref="BoardMaterials"/>
    /// and this follows its shape: one cached material, published through <see cref="SetLineColour"/> from
    /// <c>GameManager</c> on every state change, and never a line colour written as a constant.
    /// </summary>
    /// <remarks>
    /// Two things differ from <see cref="BoardMaterials"/>, both deliberate.
    /// <list type="bullet">
    /// <item>It clones <b>the source material</b> from <see cref="TrainAssets.BodyMaterial"/> rather than the render
    /// pipeline's default, so the smoothness and metallic the car was authored with survive the tint. A
    /// pipeline-default clone would repaint the train in flat plastic.</item>
    /// <item>The clone is mandatory rather than merely tidy: the car models import their materials <i>externally</i>,
    /// so writing to a renderer's <c>sharedMaterial</c> would edit <c>M_TrainColor.mat</c> on disk in the Editor.</item>
    /// </list>
    /// Cars are destroyed and rebuilt on every run while this material is static, which is the right way round —
    /// the tint outlives the train and a rebuilt car picks up the colour already published.
    /// </remarks>
    public static class TrainMaterials
    {
        /// <summary>URP Lit's main colour. <c>Material.color</c> resolves to it, but only on a shader that has it.</summary>
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private static Material _body;
        private static Material _source;
        private static Color _lineColour = Palette.Warn;

        /// <summary>
        /// The tinted stand-in for <paramref name="source"/>, or null when there is nothing to clone. A different
        /// source — a different train — replaces the clone rather than tinting the old one.
        /// </summary>
        public static Material Body(Material source)
        {
            if (source == null) return null;
            if (_body != null && _source == source) return _body;

            _source = source;
            _body = new Material(source) { name = source.name + " (line)" };
            Tint(_body, _lineColour);
            return _body;
        }

        /// <summary>Publishes the active line's colour to the train, the same way the board and the shell get it.</summary>
        public static void SetLineColour(Color colour)
        {
            _lineColour = colour;
            if (_body != null) Tint(_body, colour);
        }

        private static void Tint(Material material, Color colour)
        {
            if (material.HasProperty(BaseColor)) material.SetColor(BaseColor, colour);
            else material.color = colour;
        }
    }
}
