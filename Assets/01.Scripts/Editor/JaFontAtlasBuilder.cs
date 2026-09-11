using System.Collections.Generic;
using System.Linq;
using TrainSudoku.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace TrainSudoku.Editor
{
    /// <summary>
    /// Bakes the Japanese SDF atlas from the copy that actually ships (3.4). en/es/fr ride Barlow, whose whole
    /// Latin range is a few hundred glyphs, but a full CJK set is tens of thousands — so the ja atlas is baked
    /// from exactly the characters the ja build can draw: the `ja` String Table, every station name and the line
    /// name, and nothing else.
    ///
    /// The work order calls this a *static* atlas. **`AtlasPopulationMode.Static` is deprecated on 6000.7.0a6**
    /// — the Advanced Text Generator does not support static font assets — and Unity's replacement is a *dynamic*
    /// asset with a pre-baked atlas, which is what this builds. The size win is the same: the atlas holds only
    /// the baked characters, and Unity subsets the source font to match rather than shipping all of Noto.
    ///
    /// **Re-run this whenever ja copy or a station name changes**, or the new characters render as missing-glyph
    /// boxes. The menu logs the glyph count and the atlas size; watch both, because fonts are the only real
    /// build-size risk in this project.
    /// </summary>
    internal static class JaFontAtlasBuilder
    {
        private const string FontPath = "Assets/02.Graphics/Fonts/SDF/NotoSansJP-Medium SDF.asset";
        private const string JaTablePath = "Assets/03.Data/Localization/UI/UI_ja.asset";

        /// <summary>
        /// ASCII printable. Station names are untranslated proper nouns (D14) and the clue counts in
        /// `play.row_clear` are digits, so the ja face draws Latin as well as kana and kanji.
        /// </summary>
        private const int FirstAscii = 32;
        private const int LastAscii = 126;

        /// <summary>
        /// The bake settings. 48 is the number that matters: Noto's own default sampling size of 90 needs two
        /// 1024 sheets for these 151 glyphs, and a face that spans two atlas textures costs a second draw call on
        /// every screen that mixes kana with a station name. At 48 the same set lands on one sheet with room to
        /// spare, and SDF scales up to the 64 px signage sizes without softening.
        /// </summary>
        private const int SamplingPointSize = 48;
        private const int AtlasPadding = 5;
        private const int AtlasSize = 1024;

        [MenuItem("Window/TrainSudoku/Rebuild ja Font Atlas")]
        internal static void Rebuild()
        {
            var font = AssetDatabase.LoadAssetAtPath<FontAsset>(FontPath);
            if (font == null)
            {
                Debug.LogError($"No font asset at {FontPath}.");
                return;
            }

            var characters = Characters();
            var cjk = characters.Count(c => c > LastAscii);

            // The face stays Dynamic — see the note above — and the bake is what makes it cheap: TryAddCharacters
            // rasterises every character into the serialised atlas now, so the build ships that atlas and a font
            // subset rather than filling the atlas on the device from all of Noto.
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            Configure(font);
            // `false` keeps the existing atlas texture, which is a sub-asset of the font. Clearing with `true`
            // destroys it, and the rebake then has nothing persisted to draw into: the face saves at a few KB and
            // every glyph turns into a missing-glyph box on the device.
            font.ClearFontAssetData(false);

            var added = font.TryAddCharacters(characters, out var missing);
            AdoptAtlasTextures(font);
            // Again after the bake: ClearFontAssetData and TryAddCharacters both touch the face.
            RefreshFace(font);

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();

            var sheets = font.atlasTextures?.Length ?? 0;
            var report = $"ja atlas: {characters.Length} characters requested ({cjk} non-ASCII), " +
                         $"{sheets} atlas texture(s) at {font.atlasWidth}x{font.atlasHeight}.";

            if (!added || !string.IsNullOrEmpty(missing))
                Debug.LogError($"{report}\nThe atlas did not take every character — missing: {missing}");
            else if (sheets > 1)
                Debug.LogWarning($"{report}\nMore than one atlas texture: shrink the sampling point size.");
            else
                Debug.Log(report);
        }

        /// <summary>
        /// Puts the bake settings on the face. None of these has a public setter on TextCore's
        /// <see cref="FontAsset"/> — they are serialised fields the inspector writes — so they go on through
        /// <see cref="SerializedObject"/>, which is the supported way to reach them and survives a domain reload.
        /// </summary>
        private static void Configure(FontAsset font)
        {
            var serialized = new SerializedObject(font);

            SetNumber(serialized, "m_AtlasPadding", AtlasPadding);
            SetNumber(serialized, "m_AtlasWidth", AtlasSize);
            SetNumber(serialized, "m_AtlasHeight", AtlasSize);

            // One sheet or none: a silent spill onto a second texture is the failure this bake exists to avoid, so
            // let the overflow come back as missing characters that the report can name.
            serialized.FindProperty("m_IsMultiAtlasTexturesEnabled").boolValue = false;

            // A dynamic face defaults to throwing its rasterised glyphs away when a build starts, because normally
            // the device refills the atlas from the source font. That is exactly what must not happen here: the
            // bake *is* the shipped atlas, and the point of it is that all of Noto need not travel with it.
            serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            // The face metrics have to be right before the bake as well as after it.
            RefreshFace(font);
        }

        /// <summary>
        /// Writes a number without caring whether the field behind it is an int or a float — the face's own
        /// metrics mix the two, and guessing wrong logs "type is not a supported int value" and skips the write.
        /// </summary>
        private static void SetNumber(SerializedObject serialized, string path, float value)
        {
            var property = serialized.FindProperty(path);
            if (property == null)
            {
                Debug.LogWarning($"{path} is gone from FontAsset; the bake settings no longer all apply.");
                return;
            }

            if (property.propertyType == SerializedPropertyType.Float) property.floatValue = value;
            else property.intValue = Mathf.RoundToInt(value);
        }

        /// <summary>
        /// Re-reads the whole face — name and metrics — from the source at the sampling size actually used.
        /// </summary>
        /// <remarks>
        /// <b>The face info is one coherent set, and writing half of it breaks the other half.</b> Every metric in
        /// it is expressed in the units of <c>m_PointSize</c>, and the renderer scales a label by
        /// <c>fontSize / pointSize</c>. This used to overwrite <c>m_PointSize</c> with 48 and leave line height,
        /// ascent and the rest at whatever the previous bake had put there — Noto's own default of 90 — so every
        /// metric came out 90/48 = 1.875x too large. Nothing looked wrong glyph for glyph, because the glyphs are
        /// rasterised at the sampling size either way; what went wrong was the *boxes*, with a line height of 2.7x
        /// the font size instead of 1.45x. Every ja label was three times its proper height, which pushed the
        /// concourse's sign card to 452 px and squeezed the platform art underneath it.
        ///
        /// Clearing and rebaking also leaves the previous source's name behind, and a face that still calls itself
        /// Thin after a Medium bake is the trap this milestone fell into once already: the pixels are right and the
        /// label lies about them. Same cure — take the lot from the face that was actually loaded.
        /// </remarks>
        private static void RefreshFace(FontAsset font)
        {
            if (font.sourceFontFile == null) return;
            if (FontEngine.LoadFontFace(font.sourceFontFile, SamplingPointSize) != FontEngineError.Success)
            {
                Debug.LogWarning($"Could not load {font.sourceFontFile.name} at {SamplingPointSize} pt; " +
                                 "the face metrics are whatever the last bake left.");
                return;
            }

            var face = FontEngine.GetFaceInfo();
            var serialized = new SerializedObject(font);

            serialized.FindProperty("m_FaceInfo.m_FamilyName").stringValue = face.familyName;
            serialized.FindProperty("m_FaceInfo.m_StyleName").stringValue = face.styleName;

            SetNumber(serialized, "m_FaceInfo.m_PointSize", face.pointSize);
            SetNumber(serialized, "m_FaceInfo.m_Scale", face.scale);
            // m_UnitsPerEM is left alone: it is the font file's own em square, not a bake setting, and this
            // editor's FaceInfo does not expose it.
            SetNumber(serialized, "m_FaceInfo.m_LineHeight", face.lineHeight);
            SetNumber(serialized, "m_FaceInfo.m_AscentLine", face.ascentLine);
            SetNumber(serialized, "m_FaceInfo.m_CapLine", face.capLine);
            SetNumber(serialized, "m_FaceInfo.m_MeanLine", face.meanLine);
            SetNumber(serialized, "m_FaceInfo.m_Baseline", face.baseline);
            SetNumber(serialized, "m_FaceInfo.m_DescentLine", face.descentLine);
            SetNumber(serialized, "m_FaceInfo.m_SuperscriptOffset", face.superscriptOffset);
            SetNumber(serialized, "m_FaceInfo.m_SuperscriptSize", face.superscriptSize);
            SetNumber(serialized, "m_FaceInfo.m_SubscriptOffset", face.subscriptOffset);
            SetNumber(serialized, "m_FaceInfo.m_SubscriptSize", face.subscriptSize);
            SetNumber(serialized, "m_FaceInfo.m_UnderlineOffset", face.underlineOffset);
            SetNumber(serialized, "m_FaceInfo.m_UnderlineThickness", face.underlineThickness);
            SetNumber(serialized, "m_FaceInfo.m_StrikethroughOffset", face.strikethroughOffset);
            SetNumber(serialized, "m_FaceInfo.m_StrikethroughThickness", face.strikethroughThickness);
            SetNumber(serialized, "m_FaceInfo.m_TabWidth", face.tabWidth);

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Takes any atlas sheet the bake had to create into the font's own asset file. A sheet that stays loose is
        /// dropped on save, so the face would ship claiming glyphs it has no pixels for.
        /// </summary>
        private static void AdoptAtlasTextures(FontAsset font)
        {
            var textures = font.atlasTextures;
            if (textures == null) return;

            foreach (var texture in textures)
            {
                if (texture == null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture))) continue;
                AssetDatabase.AddObjectToAsset(texture, font);
            }
        }

        /// <summary>
        /// Every character the ja build can put on screen, deduplicated and ordered so the atlas packs the same
        /// way on every rebuild.
        /// </summary>
        private static string Characters()
        {
            var chars = new SortedSet<char>();
            for (var c = FirstAscii; c <= LastAscii; c++) chars.Add((char)c);

            var table = AssetDatabase.LoadAssetAtPath<StringTable>(JaTablePath);
            if (table == null) Debug.LogWarning($"No ja String Table at {JaTablePath}; baking Latin only.");
            else
                foreach (var entry in table.Values)
                    if (entry != null) Add(chars, entry.LocalizedValue);

            foreach (var level in Assets<LevelDefinition>()) Add(chars, level.DisplayName);
            foreach (var line in Assets<LineDefinition>())
            {
                Add(chars, line.DisplayName);
                Add(chars, line.Code);
            }

            return new string(chars.ToArray());
        }

        private static void Add(ISet<char> chars, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            // Smart-string braces are markup, never drawn; their replacements are digits, already in the ASCII run.
            foreach (var c in text)
                if (c != '{' && c != '}' && !char.IsControl(c)) chars.Add(c);
        }

        private static IEnumerable<T> Assets<T>() where T : Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null);
    }
}
