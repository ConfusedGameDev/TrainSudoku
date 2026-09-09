using TrainSudoku.Game;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace TrainSudoku.Editor
{
    /// <summary>Default inspector plus a button that opens the level in the Level Editor window.</summary>
    [CustomEditor(typeof(LevelDefinition))]
    public sealed class LevelDefinitionInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var button = new Button(() => LevelEditorWindow.Open((LevelDefinition)target)) { text = "Open in Level Editor" };
            button.style.marginBottom = 6;
            root.Add(button);
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            return root;
        }
    }
}
