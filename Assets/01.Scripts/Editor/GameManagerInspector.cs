using TrainSudoku.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Editor
{
    /// <summary>Adds the "Generate Scene Objects" button so the UI, board root and helpers are created once and saved with the scene.</summary>
    [CustomEditor(typeof(GameManager))]
    public sealed class GameManagerInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var manager = (GameManager)target;

            var status = new HelpBox("", HelpBoxMessageType.Info);
            root.Add(status);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.marginBottom = 8;
            var generate = new Button(() => Run(manager, manager.Generate)) { text = "Generate Scene Objects" };
            var clear = new Button(() => Run(manager, manager.ClearGenerated)) { text = "Clear" };
            buttons.Add(generate);
            buttons.Add(clear);
            root.Add(buttons);

            void UpdateStatus()
            {
                var generated = manager.IsGenerated;
                status.text = generated
                    ? "Scene objects are generated and saved with the scene. Regenerate after changing the UI code."
                    : "Scene objects are missing. They will be generated at runtime; click Generate to bake them into the scene.";
                status.messageType = generated ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
                generate.text = generated ? "Regenerate Scene Objects" : "Generate Scene Objects";
                clear.SetEnabled(generated);
            }

            UpdateStatus();
            root.schedule.Execute(UpdateStatus).Every(500);

            var progress = new VisualElement();
            progress.style.flexDirection = FlexDirection.Row;
            progress.style.marginBottom = 8;
            progress.Add(new Button(DeleteSaveFile) { text = "Delete save file", tooltip = GameManager.SaveFilePath });
            progress.Add(new Button(() => EditorUtility.RevealInFinder(GameManager.SaveFilePath)) { text = "Show save file" });
            root.Add(progress);

            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            return root;
        }

        private static void DeleteSaveFile()
        {
            var path = GameManager.SaveFilePath;
            if (!System.IO.File.Exists(path))
            {
                Debug.Log($"No save file at {path}.");
                return;
            }

            if (!EditorUtility.DisplayDialog("Delete save file", $"Delete all best times and unlocks?\n\n{path}", "Delete", "Cancel")) return;
            System.IO.File.Delete(path);
            Debug.Log($"Deleted {path}. Restart Play mode to see the effect.");
        }

        private static void Run(GameManager manager, System.Action action)
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Generate the scene objects in edit mode, not while playing.");
                return;
            }

            action();
            EditorUtility.SetDirty(manager);
            if (manager.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        }
    }
}
