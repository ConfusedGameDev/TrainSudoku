using TrainSudoku.Game;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Editor
{
    /// <summary>
    /// Default inspector plus a button that opens the level in the Level Editor window, and one that fills its two
    /// tunnel cells from the solver.
    /// </summary>
    [CustomEditor(typeof(LevelDefinition))]
    public sealed class LevelDefinitionInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var button = new Button(() => LevelEditorWindow.Open((LevelDefinition)target)) { text = "Open in Level Editor" };
            button.style.marginBottom = 6;
            root.Add(button);

            var bake = new Button(BakeTunnelPieces)
            {
                text = "Bake tunnel pieces",
                tooltip = "Give the entrance and exit cells the piece the solution puts there, unless they already have one",
            };
            bake.style.marginBottom = 6;
            root.Add(bake);
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            return root;
        }

        private void BakeTunnelPieces()
        {
            var level = (LevelDefinition)target;
            var report = TunnelPieceBaker.Bake(level, out var wrote);
            if (wrote) AssetDatabase.SaveAssets();
            Debug.Log($"{level.DisplayName}: {report}", level);
        }
    }
}
