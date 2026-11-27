using UnityEditor;
using UnityEngine;

namespace TrainSudoku.XR.Editor
{
    /// <summary>
    /// Pip's art for the XR panels: sets the importers on the six textures under <c>02.Graphics/XR/Pip</c> and fills the
    /// Pip slots on <see cref="XRSignageAssets"/>. Running it again only re-applies. Batchmode:
    /// <c>-executeMethod TrainSudoku.XR.Editor.XRPipSetup.Run</c>.
    /// </summary>
    /// <remarks>
    /// The art comes from the restyle handoff (<c>newDesign/unity-handoff</c>), 1024 px square. A panel shows Pip at
    /// 300 UI pixels at most, so she is imported at 512 with mip maps, ASTC 6x6 on the headset.
    /// </remarks>
    public static class XRPipSetup
    {
        private const string Folder = "Assets/02.Graphics/XR/Pip";
        private const string SignagePath = "Assets/03.Data/XR/XRSignageAssets.asset";
        private const int MaxSize = 512;

        private static readonly (string Field, string File)[] Slots =
        {
            ("pipIdle", "pip-idle.png"),
            ("pipCheer", "pip-cheer.png"),
            ("pipWorry", "pip-worry.png"),
            ("pipSleep", "pip-sleep.png"),
            ("pipWaveBase", "pip-wave-base.png"),
            ("pipPaw", "pip-wave-paw.png"),
        };

        [MenuItem("Window/TrainSudoku/XR/Import Pip")]
        public static void Run()
        {
            var signage = AssetDatabase.LoadAssetAtPath<XRSignageAssets>(SignagePath);
            if (signage == null)
            {
                Debug.LogError($"[XR] No signage assets at {SignagePath}: run Add Flow to XR Scene first.");
                return;
            }

            var fields = new SerializedObject(signage);
            var filled = 0;
            foreach (var (field, file) in Slots)
            {
                var path = $"{Folder}/{file}";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                {
                    Debug.LogError($"[XR] No Pip texture at {path}.");
                    continue;
                }

                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.isReadable = false;
                importer.maxTextureSize = MaxSize;
                var android = importer.GetPlatformTextureSettings("Android");
                android.overridden = true;
                android.maxTextureSize = MaxSize;
                android.format = TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport();

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                fields.FindProperty(field).objectReferenceValue = texture;
                if (texture != null) filled++;
            }

            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(signage);
            AssetDatabase.SaveAssets();
            Debug.Log($"[XR] Pip: {filled} of {Slots.Length} textures imported at {MaxSize} px and assigned to {SignagePath}.");
        }
    }
}
