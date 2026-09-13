using System.Collections.Generic;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// Makes the XR board's materials hide behind the player's real body (XR-PRD 5.6). Only materials on an occlusion
    /// shader are hidden, so every board and train material comes from the two templates configured here: the board's
    /// own colours through <see cref="Lit"/>, the kit's materials through <see cref="Occluded"/> (keeping their colour
    /// and texture), and TextMesh text through <see cref="Text"/>. Without templates every call hands back what the
    /// board used before, so the board still draws, just unoccluded.
    /// </summary>
    public static class XROcclusionMaterials
    {
        private static Material _lit;
        private static Material _text;
        private static readonly Dictionary<Material, Material> Twins = new Dictionary<Material, Material>();
        private static readonly Dictionary<Font, Material> Texts = new Dictionary<Font, Material>();

        public static void Configure(Material litTemplate, Material textTemplate)
        {
            _lit = litTemplate;
            _text = textTemplate;
            Twins.Clear();
            Texts.Clear();
        }

        /// <summary>A new occluded material in a flat colour, or null when there is no template.</summary>
        public static Material Lit(string name, Color color) =>
            _lit == null ? null : new Material(_lit) { name = name, color = color };

        /// <summary>The occluded twin of <paramref name="source"/>, keeping its colour and texture. Made once per source.</summary>
        public static Material Occluded(Material source)
        {
            if (_lit == null || source == null || source.shader == _lit.shader) return source;
            if (Twins.TryGetValue(source, out var twin) && twin != null) return twin;

            twin = new Material(_lit) { name = source.name + " (occluded)" };
            if (source.HasProperty("_BaseColor")) twin.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            else if (source.HasProperty("_Color")) twin.SetColor("_BaseColor", source.color);
            var textureProperty = source.HasProperty("_BaseMap") ? "_BaseMap" : source.HasProperty("_MainTex") ? "_MainTex" : null;
            if (textureProperty != null && source.GetTexture(textureProperty) is Texture texture)
            {
                twin.SetTexture("_BaseMap", texture);
                twin.SetTextureScale("_BaseMap", source.GetTextureScale(textureProperty));
                twin.SetTextureOffset("_BaseMap", source.GetTextureOffset(textureProperty));
            }

            Twins[source] = twin;
            return twin;
        }

        /// <summary>Swaps every renderer under <paramref name="root"/> onto occluded twins; text is left to <see cref="Text"/>.</summary>
        public static void Convert(GameObject root)
        {
            if (_lit == null || root == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponent<TextMesh>() != null) continue;
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++) materials[i] = Occluded(materials[i]);
                renderer.sharedMaterials = materials;
            }
        }

        /// <summary>The material for TextMesh text in <paramref name="font"/>: occluded when there is a template, the font's own otherwise.</summary>
        public static Material Text(Font font)
        {
            if (font == null) return null;
            if (_text == null) return font.material;
            if (Texts.TryGetValue(font, out var material) && material != null) return material;
            material = new Material(_text) { name = font.name + " (occluded)", mainTexture = font.material.mainTexture };
            Texts[font] = material;
            return material;
        }
    }
}
