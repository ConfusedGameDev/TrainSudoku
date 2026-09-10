using TrainSudoku.Game;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace TrainSudoku.Editor
{
    /// <summary>Default inspector plus a button that opens the line in the Line Map Editor window.</summary>
    [CustomEditor(typeof(LineDefinition))]
    public sealed class LineDefinitionInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var button = new Button(() => LineMapEditorWindow.Open((LineDefinition)target)) { text = "Open in Line Map Editor" };
            button.style.marginBottom = 6;
            root.Add(button);
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            return root;
        }
    }
}
