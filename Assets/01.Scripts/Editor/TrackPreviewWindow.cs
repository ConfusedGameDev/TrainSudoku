using System.Linq;
using TrainSudoku.Core;
using TrainSudoku.Game;
using UnityEditor;
using UnityEngine;

namespace TrainSudoku.Editor
{
    /// <summary>
    /// Lays the six bent track keys out in a row in the open scene so the curve quality can be eyeballed and
    /// screenshotted without loading a level. Nothing here ships: the row is a throwaway object.
    /// </summary>
    public static class TrackPreviewWindow
    {
        private const string RootName = "Track Preview (temporary)";

        [MenuItem("Window/TrainSudoku/Track Preview")]
        public static void Build()
        {
            Clear();

            var assets = AssetDatabase.FindAssets("t:TrackAssets")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TrackAssets>)
                .FirstOrDefault(a => a != null);

            var profile = assets != null ? assets.ResolveProfile() : TrackAssets.PlaceholderProfile();
            var material = assets != null ? assets.TrackMaterial : BoardMaterials.Track;

            var root = new GameObject(RootName);
            var keys = PieceKeys.All.ToList();
            for (var i = 0; i < keys.Count; i++)
            {
                var go = new GameObject(keys[i].ToString());
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(i * 1.25f, 0f, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = TrackMeshBender.ForKey(profile, keys[i]);
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }

            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log($"Track Preview: {keys.Count} keys from '{profile.Name}'. Run the menu item again to rebuild, and delete the object when done.");
        }

        [MenuItem("Window/TrainSudoku/Track Preview (clear)")]
        public static void Clear()
        {
            foreach (var existing in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                         .Where(t => t != null && t.name == RootName)
                         .Select(t => t.gameObject)
                         .ToList())
            {
                Object.DestroyImmediate(existing);
            }
        }
    }
}
