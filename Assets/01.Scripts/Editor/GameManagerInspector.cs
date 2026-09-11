using System.Collections.Generic;
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

            root.Add(BuildLineUnlocks(manager));

            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            return root;
        }

        /// <summary>
        /// A toggle per line, so the network map can be looked at at any stage of progress without playing two
        /// hundred boards to get there.
        /// </summary>
        /// <remarks>
        /// The toggles are drawn by name rather than left to the default array inspector because "Element 11" says
        /// nothing about which line it is. The readout underneath is the load-bearing part: unlocking is a chain and
        /// the map draws the open lines <i>plus the first closed one</i>, so it stops at the first gap — ticking a
        /// late line on its own shows a padlock, not that line. Rather than forbid that, the box says what the map
        /// will actually draw, which explains the rule the first time someone trips over it.
        /// </remarks>
        private VisualElement BuildLineUnlocks(GameManager manager)
        {
            var box = new VisualElement();
            box.style.marginBottom = 8;

            var property = serializedObject.FindProperty("debugUnlockedLines");
            var network = manager.Network;
            if (property == null || network == null || network.LineCount == 0) return box;

            var foldout = new Foldout { text = "Debug: line unlocks", value = false };
            box.Add(foldout);

            var readout = new HelpBox("", HelpBoxMessageType.None);
            var toggles = new List<Toggle>();

            void Sync()
            {
                // The array is authored against whatever the network holds today; a line added since should not
                // leave a row undrawable.
                if (property.arraySize != network.LineCount)
                {
                    property.arraySize = network.LineCount;
                    serializedObject.ApplyModifiedProperties();
                }

                var revealed = 0;
                for (var i = 0; i < network.LineCount; i++)
                {
                    var line = network.Line(i);
                    var open = i == 0 || property.GetArrayElementAtIndex(i).boolValue;
                    if (i < toggles.Count) toggles[i].SetValueWithoutNotify(open);
                    revealed++;
                    if (!open) break;   // the map stops at the first closed line
                }

                var last = network.Line(Mathf.Min(revealed, network.LineCount) - 1);
                var next = revealed < network.LineCount ? network.Line(revealed) : null;
                readout.text = $"The map draws {revealed} of {network.LineCount} lines, out to {Describe(last)}" +
                               (next != null ? $", with {Describe(next)} shown locked." : ". Nothing is left locked.");
            }

            for (var index = 0; index < network.LineCount; index++)
            {
                var i = index;
                var line = network.Line(i);
                var toggle = new Toggle($"{i:00}  {Describe(line)}");
                toggle.SetEnabled(i > 0);   // line 0 is always open; a box that cannot change is a lie
                toggle.tooltip = i == 0 ? "The first line is always open." : null;
                toggle.RegisterValueChangedCallback(evt =>
                {
                    property.GetArrayElementAtIndex(i).boolValue = evt.newValue;
                    serializedObject.ApplyModifiedProperties();
                    Sync();
                    if (Application.isPlaying) manager.RefreshCurrentScreen();
                });

                toggles.Add(toggle);
                foldout.Add(toggle);
            }

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 4;
            row.Add(new Button(() => SetThrough(property, manager, network.LineCount, Sync)) { text = "All" });
            row.Add(new Button(() => SetThrough(property, manager, 0, Sync)) { text = "None" });
            foldout.Add(row);
            foldout.Add(readout);

            Sync();
            return box;
        }

        /// <summary>Opens the first <paramref name="count"/> lines and closes the rest — the shape real progress has.</summary>
        private static void SetThrough(SerializedProperty property, GameManager manager, int count, System.Action sync)
        {
            for (var i = 0; i < property.arraySize; i++) property.GetArrayElementAtIndex(i).boolValue = i < count;
            property.serializedObject.ApplyModifiedProperties();
            sync();
            if (Application.isPlaying) manager.RefreshCurrentScreen();
        }

        private static string Describe(LineDefinition line) =>
            line == null ? "—" : $"{line.Code} {line.DisplayName}";

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
