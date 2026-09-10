// TEMPORARY DIAGNOSTIC — delete once the concourse layout question is settled.
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace TrainSudoku.Editor
{
    internal static class UiTreeDump
    {
        [MenuItem("Window/TrainSudoku/Dump UI Tree")]
        private static void Dump()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"playing={Application.isPlaying}  screen={Screen.width}x{Screen.height}  safeArea={Screen.safeArea}");

            Probe(sb);

            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.AppendLine($"UIDocuments found: {docs.Length}");

            foreach (var doc in docs)
            {
                var root = doc.rootVisualElement;
                sb.AppendLine($"\n=== {doc.gameObject.name}  activeInHierarchy={doc.gameObject.activeInHierarchy} enabled={doc.enabled} root={(root == null ? "NULL" : "ok")} panelSettings={(doc.panelSettings == null ? "NULL" : doc.panelSettings.name)}");
                if (root == null) continue;
                sb.AppendLine($"    sheets={root.styleSheets.count} rootRect={root.layout}");
                Walk(root, 1, sb);
            }

            var path = Path.GetFullPath("ui-tree-dump.txt");
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"UI tree written to {path}");
        }

        private static void Probe(StringBuilder sb)
        {
            sb.AppendLine("\n--- localisation ---");
            try
            {
                var locale = LocalizationSettings.SelectedLocale;
                sb.AppendLine($"SelectedLocale = {(locale == null ? "NULL" : locale.Identifier.Code)}");

                foreach (var key in new[] { "play.pause", "concourse.board" })
                {
                    try { sb.AppendLine($"  string '{key}' -> '{LocalizationSettings.StringDatabase.GetLocalizedString("UI", key)}'"); }
                    catch (System.Exception e) { sb.AppendLine($"  string '{key}' THREW {e.GetType().Name}: {e.Message}"); }
                }

                foreach (var key in new[] { "font.signage", "font.body", "font.numerals" })
                {
                    try
                    {
                        var face = LocalizationSettings.AssetDatabase.GetLocalizedAsset<FontAsset>("Fonts", key);
                        sb.AppendLine($"  font   '{key}' -> {(face == null ? "NULL" : face.name)}");
                    }
                    catch (System.Exception e) { sb.AppendLine($"  font   '{key}' THREW {e.GetType().Name}: {e.Message}"); }
                }
            }
            catch (System.Exception e) { sb.AppendLine($"probe THREW {e.GetType().Name}: {e.Message}"); }
        }

        private static void Walk(VisualElement e, int depth, StringBuilder sb)
        {
            var pad = new string(' ', depth * 2);
            var classes = string.Join(".", e.GetClasses());
            var text = e is UnityEngine.UIElements.TextElement t && !string.IsNullOrEmpty(t.text) ? $" text='{t.text}'" : "";
            var rs = e.resolvedStyle;
            var font = "";
            if (e is UnityEngine.UIElements.TextElement te)
            {
                var fd = te.resolvedStyle.unityFontDefinition;
                font = $" font={(fd.fontAsset != null ? fd.fontAsset.name : fd.font != null ? fd.font.name : "NONE")} size={rs.fontSize:0}";
            }

            sb.AppendLine($"{pad}{e.GetType().Name}{(string.IsNullOrEmpty(classes) ? "" : " ." + classes)}{text}");
            sb.AppendLine($"{pad}   rect=({rs.left:0},{rs.top:0} {rs.width:0}x{rs.height:0}) display={rs.display} visible={e.visible} opacity={rs.opacity:0.##} bg={rs.backgroundColor} color={rs.color}{font}");

            foreach (var child in e.Children()) Walk(child, depth + 1, sb);
        }
    }
}
