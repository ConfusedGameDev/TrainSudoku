using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TrainSudoku.Game;
using TrainSudoku.XR.Rules;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace TrainSudoku.XR.Editor
{
    /// <summary>
    /// XR10 (XR-PRD 9, X24): XR's own `XR` String Table, its own Japanese atlas, and its cue library, each written from
    /// code so the result can be rebuilt and reviewed. Every step skips what already exists and never touches the
    /// phone's table, atlas or cue library. <see cref="RunAll"/> is the batchmode entry point
    /// (<c>-executeMethod TrainSudoku.XR.Editor.XRLanguageAudioSetup.RunAll</c>).
    /// </summary>
    public static class XRLanguageAudioSetup
    {
        private const string TableDirectory = "Assets/03.Data/XR/Localization";
        private const string PhoneFontPath = "Assets/02.Graphics/Fonts/SDF/NotoSansJP-Medium SDF.asset";
        private const string FontDirectory = "Assets/02.Graphics/XR/Fonts";
        private const string FontPath = FontDirectory + "/NotoSansJP-Medium XR SDF.asset";
        private const string SignagePath = "Assets/03.Data/XR/XRSignageAssets.asset";
        private const string CuesDirectory = "Assets/03.Data/XR/Audio";
        private const string CuesPath = CuesDirectory + "/XRCues.asset";
        private const string ScenePath = "Assets/Scenes/XR.unity";

        /// <summary>The phone's bake settings, for the same reasons (see the phone's <c>JaFontAtlasBuilder</c>).</summary>
        private const int SamplingPointSize = 48;
        private const int AtlasPadding = 5;
        private const int AtlasSize = 1024;
        private const int FirstAscii = 32;
        private const int LastAscii = 126;

        /// <summary>Line height over point size on a correctly baked face: about 1.45 (XR-PRD 10.7).</summary>
        private const float ExpectedLineRatio = 1.45f;

        public static void RunAll()
        {
            WriteStringTable();
            RebuildJaAtlas();
            WriteCueLibrary();
            Check();
        }

        // ------------------------------------------------------------------ the String Table

        [MenuItem("Window/TrainSudoku/XR/Write XR String Table")]
        internal static void WriteStringTable()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(XRText.Table);
            if (collection == null)
            {
                EnsureFolder(TableDirectory);
                collection = LocalizationEditorSettings.CreateStringTableCollection(XRText.Table, TableDirectory, LocalizationEditorSettings.GetLocales());
            }

            foreach (var table in collection.StringTables)
            {
                var code = table.LocaleIdentifier.Code;
                foreach (var row in XRCopy.Rows) table.AddEntry(row.Key, row.Value.For(code));
                EditorUtility.SetDirty(table);
            }

            // Rows the copy no longer has: gone from every locale, so a stale line can never ship.
            var stale = collection.SharedData.Entries.Select(e => e.Key).Where(key => !XRCopy.Rows.ContainsKey(key)).ToList();
            foreach (var key in stale) collection.RemoveEntry(key);

            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();
            Debug.Log($"[XR] `{XRText.Table}` String Table: {XRCopy.Rows.Count} rows in {collection.StringTables.Count} locales" +
                      (stale.Count > 0 ? $"; removed {stale.Count} stale row(s)." : "."));
        }

        // ------------------------------------------------------------------ the Japanese atlas

        /// <summary>
        /// Bakes XR's own ja face (X24) from exactly what the XR ja build can draw: the `XR` table's ja rows, the station
        /// and line names and codes, the language names the settings row shows in every locale, and ASCII. Started as a
        /// copy of the phone's face, so it inherits its source (the static Medium instance) and settings; from then on the
        /// two are baked separately and never kept in step.
        /// </summary>
        [MenuItem("Window/TrainSudoku/XR/Rebuild XR ja Font Atlas")]
        internal static void RebuildJaAtlas()
        {
            if (AssetDatabase.LoadAssetAtPath<FontAsset>(FontPath) == null)
            {
                EnsureFolder(FontDirectory);
                if (!AssetDatabase.CopyAsset(PhoneFontPath, FontPath))
                {
                    Debug.LogError($"[XR] Could not copy {PhoneFontPath} to {FontPath}.");
                    return;
                }
            }

            var font = AssetDatabase.LoadAssetAtPath<FontAsset>(FontPath);
            var characters = Characters();

            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            Configure(font);
            font.ClearFontAssetData(false);
            var added = font.TryAddCharacters(characters, out var missing);
            AdoptAtlasTextures(font);
            RefreshFace(font);
            EditorUtility.SetDirty(font);

            var signage = AssetDatabase.LoadAssetAtPath<XRSignageAssets>(SignagePath);
            if (signage != null)
            {
                signage.SetJapaneseFont(font);
                EditorUtility.SetDirty(signage);
            }
            else Debug.LogWarning($"[XR] No signage assets at {SignagePath}: the baked face is not wired to the signboard.");

            AssetDatabase.SaveAssets();

            var sheets = font.atlasTextures?.Length ?? 0;
            var ratio = font.faceInfo.pointSize > 0 ? font.faceInfo.lineHeight / font.faceInfo.pointSize : 0f;
            var report = $"[XR] ja atlas: {characters.Length} characters ({characters.Count(c => c > LastAscii)} non-ASCII), " +
                         $"{sheets} sheet(s) at {font.atlasWidth}x{font.atlasHeight}, line height {ratio:0.00} x point size.";
            if (!added || !string.IsNullOrEmpty(missing)) Debug.LogError($"{report}\nMissing: {missing}");
            else if (sheets > 1) Debug.LogWarning($"{report}\nMore than one sheet: shrink the sampling size.");
            else if (Mathf.Abs(ratio - ExpectedLineRatio) > 0.15f) Debug.LogError($"{report}\nThe face metrics are off: expected about {ExpectedLineRatio}.");
            else Debug.Log(report);
        }

        private static string Characters()
        {
            var chars = new SortedSet<char>();
            for (var c = FirstAscii; c <= LastAscii; c++) chars.Add((char)c);
            foreach (var row in XRCopy.Rows.Values) Add(chars, row.Ja);
            foreach (var name in XRLocale.NativeNames) Add(chars, name);
            // The settings row's back arrow.
            Add(chars, "‹");
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
            // Smart-string braces are markup; their replacements are digits, already in the ASCII run.
            foreach (var c in text)
                if (c != '{' && c != '}' && !char.IsControl(c)) chars.Add(c);
        }

        private static void Configure(FontAsset font)
        {
            var serialized = new SerializedObject(font);
            SetNumber(serialized, "m_AtlasPadding", AtlasPadding);
            SetNumber(serialized, "m_AtlasWidth", AtlasSize);
            SetNumber(serialized, "m_AtlasHeight", AtlasSize);
            serialized.FindProperty("m_IsMultiAtlasTexturesEnabled").boolValue = false;
            // The bake is the shipped atlas: a build must not throw it away (CLAUDE.md, the ja face).
            serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            RefreshFace(font);
        }

        private static void SetNumber(SerializedObject serialized, string path, float value)
        {
            var property = serialized.FindProperty(path);
            if (property == null)
            {
                Debug.LogWarning($"[XR] {path} is gone from FontAsset; the bake settings no longer all apply.");
                return;
            }

            if (property.propertyType == SerializedPropertyType.Float) property.floatValue = value;
            else property.intValue = Mathf.RoundToInt(value);
        }

        /// <summary>The whole face, name and metrics, re-read at the sampling size: half of it is the 1.875x bug (CLAUDE.md).</summary>
        private static void RefreshFace(FontAsset font)
        {
            if (font.sourceFontFile == null) return;
            if (FontEngine.LoadFontFace(font.sourceFontFile, SamplingPointSize) != FontEngineError.Success)
            {
                Debug.LogWarning($"[XR] Could not load {font.sourceFontFile.name} at {SamplingPointSize} pt.");
                return;
            }

            var face = FontEngine.GetFaceInfo();
            var serialized = new SerializedObject(font);
            serialized.FindProperty("m_FaceInfo.m_FamilyName").stringValue = face.familyName;
            serialized.FindProperty("m_FaceInfo.m_StyleName").stringValue = face.styleName;
            SetNumber(serialized, "m_FaceInfo.m_PointSize", face.pointSize);
            SetNumber(serialized, "m_FaceInfo.m_Scale", face.scale);
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

        private static void AdoptAtlasTextures(FontAsset font)
        {
            var textures = font.atlasTextures;
            if (textures == null) return;
            foreach (var texture in textures)
                if (texture != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture)))
                    AssetDatabase.AddObjectToAsset(texture, font);
        }

        // ------------------------------------------------------------------ the cues

        /// <summary>
        /// Clips from the phone's set (<c>05.Audio</c>), chosen to match what the phone plays for the same moment. The
        /// steam, the whistle, the train's run and its hurry have no clip yet: those slots are left empty, which is silent.
        /// </summary>
        private static readonly Dictionary<XRCue, (string Clip, float Volume, float Haptic, float Seconds)> Seeds =
            new Dictionary<XRCue, (string, float, float, float)>
            {
                [XRCue.Grab] = ("UI/drop_002", 0.6f, 0.15f, 0.03f),
                [XRCue.Place] = ("UI/drop_001", 1f, 0.35f, 0.05f),
                [XRCue.Move] = ("UI/drop_001", 1f, 0.35f, 0.05f),
                [XRCue.Replace] = ("UI/drop_001", 1f, 0.35f, 0.05f),
                [XRCue.Lift] = ("UI/minimize_001", 0.8f, 0.25f, 0.04f),
                [XRCue.Error] = ("UI/error_001", 0.8f, 0.6f, 0.12f),
                [XRCue.Steered] = ("UI/back_002", 0.7f, 0.2f, 0.05f),
                [XRCue.Fixed] = ("UI/error_004", 0.6f, 0.5f, 0.1f),
                [XRCue.ReturnLand] = ("UI/drop_003", 0.5f, 0f, 0f),
                [XRCue.Puff] = (null, 1f, 0f, 0f),
                [XRCue.Burst] = (null, 1f, 0f, 0f),
                [XRCue.Whistle] = (null, 1f, 0f, 0f),
                [XRCue.LineCleared] = ("UI/glass_002", 0.7f, 0f, 0f),
                [XRCue.FinalPiece] = ("UI/glass_001", 1f, 0f, 0f),
                [XRCue.TrainStart] = ("UI/maximize_001", 1f, 0f, 0f),
                [XRCue.TrainLoop] = (null, 0.6f, 0f, 0f),
                [XRCue.TrainHurry] = (null, 1f, 0f, 0f),
                [XRCue.UiPress] = ("UI/click_001", 0.8f, 0.2f, 0.03f),
                [XRCue.UiBack] = ("UI/back_001", 0.8f, 0.2f, 0.03f),
                [XRCue.WristOpen] = ("UI/scroll_003", 0.8f, 0.2f, 0.03f),
                [XRCue.MapSelect] = ("UI/select_003", 0.9f, 0.25f, 0.04f),
                [XRCue.MapClosed] = ("UI/back_003", 0.7f, 0.4f, 0.08f),
                [XRCue.BoardPlaced] = ("UI/confirmation_001", 0.9f, 0.3f, 0.06f),
                [XRCue.Star] = ("UI/bong_001", 0.9f, 0f, 0f),
                [XRCue.Stamp] = ("UI/bong_001", 1f, 0f, 0f),
                [XRCue.FanfareOne] = ("Music/Audio/Sax jingles/jingles_SAX07", 1f, 0f, 0f),
                [XRCue.FanfareTwo] = ("Music/Audio/Sax jingles/jingles_SAX14", 1f, 0f, 0f),
                [XRCue.FanfareThree] = ("Music/Audio/Sax jingles/jingles_SAX12", 1f, 0f, 0f),
                [XRCue.TutorialNote] = ("UI/glass_004", 0.6f, 0f, 0f),
            };

        /// <summary>
        /// Writes <c>XRCues.asset</c> with a slot for every cue, seeded from <see cref="Seeds"/>. A slot that already has
        /// a clip keeps it, so a person's choices survive a rerun. Then wires the asset to the game in <c>XR.unity</c>.
        /// </summary>
        [MenuItem("Window/TrainSudoku/XR/Write XR Cue Library")]
        internal static void WriteCueLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<XRCueLibrary>(CuesPath);
            if (library == null)
            {
                EnsureFolder(CuesDirectory);
                library = ScriptableObject.CreateInstance<XRCueLibrary>();
                AssetDatabase.CreateAsset(library, CuesPath);
            }

            var entries = new List<XRCueLibrary.Entry>();
            foreach (XRCue cue in System.Enum.GetValues(typeof(XRCue)))
            {
                if (cue == XRCue.None) continue;
                var entry = library.Find(cue) ?? new XRCueLibrary.Entry { cue = cue };
                if (entry.clip == null && Seeds.TryGetValue(cue, out var seed))
                {
                    if (seed.Clip != null) entry.clip = Clip(seed.Clip);
                    entry.volume = seed.Volume;
                    entry.hapticAmplitude = seed.Haptic;
                    entry.hapticSeconds = seed.Seconds;
                }

                entries.Add(entry);
            }

            library.SetEntries(entries);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            WireScene(library);
            Debug.Log($"[XR] Cue library: {entries.Count} cues, {entries.Count(e => e.clip != null)} with a clip.");
        }

        private static AudioClip Clip(string path)
        {
            foreach (var extension in new[] { ".ogg", ".wav", ".mp3" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/05.Audio/{path}{extension}");
                if (clip != null) return clip;
            }

            Debug.LogWarning($"[XR] No clip at Assets/05.Audio/{path}: that cue stays silent.");
            return null;
        }

        private static void WireScene(XRCueLibrary library)
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var game = scene.GetRootGameObjects().Select(go => go.GetComponentInChildren<XRGame>(true)).FirstOrDefault(g => g != null);
                if (game == null)
                {
                    Debug.LogWarning($"[XR] No XRGame in {ScenePath}: the cue library is not wired.");
                    return;
                }

                var serialized = new SerializedObject(game);
                serialized.FindProperty("cues").objectReferenceValue = library;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ------------------------------------------------------------------ the check

        /// <summary>
        /// Every key the code names (<see cref="XRKeys"/>, <see cref="XRTutorialKeys"/>) has a non-empty row in every
        /// locale, and the XR ja face holds every character of the ja rows. Errors name what is missing.
        /// </summary>
        [MenuItem("Window/TrainSudoku/XR/Check XR Copy")]
        public static bool Check()
        {
            var problems = new StringBuilder();
            var collection = LocalizationEditorSettings.GetStringTableCollection(XRText.Table);
            if (collection == null) problems.AppendLine($"No `{XRText.Table}` String Table.");
            else
                foreach (var key in CodeKeys())
                foreach (var table in collection.StringTables)
                {
                    var entry = table.GetEntry(key);
                    if (entry == null || string.IsNullOrEmpty(entry.LocalizedValue))
                        problems.AppendLine($"{table.LocaleIdentifier.Code}: no row for {key}");
                }

            var font = AssetDatabase.LoadAssetAtPath<FontAsset>(FontPath);
            if (font == null) problems.AppendLine($"No XR ja face at {FontPath}.");
            else
            {
                var missing = new SortedSet<char>();
                foreach (var row in XRCopy.Rows.Values)
                    foreach (var c in row.Ja)
                        if (c != '{' && c != '}' && !char.IsControl(c) && !font.characterLookupTable.ContainsKey(c)) missing.Add(c);
                if (missing.Count > 0) problems.AppendLine($"The XR ja face lacks: {new string(missing.ToArray())} (rebake it).");
            }

            if (problems.Length > 0)
            {
                Debug.LogError($"[XR] Copy check failed:\n{problems}");
                return false;
            }

            Debug.Log("[XR] Copy check: every key in every locale, every ja character baked.");
            return true;
        }

        /// <summary>Every key constant the code reads the table with.</summary>
        public static IEnumerable<string> CodeKeys() =>
            new[] { typeof(XRKeys), typeof(XRTutorialKeys) }
                .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue());

        // ------------------------------------------------------------------ helpers

        private static IEnumerable<T> Assets<T>() where T : Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null);

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
