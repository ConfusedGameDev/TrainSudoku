using System.IO;
using System.Text;
using TrainSudoku.Core;
using TrainSudoku.Game;
using UnityEditor;
using UnityEngine;

namespace TrainSudoku.Editor
{
    /// <summary>
    /// Window &gt; TrainSudoku &gt; Save File. The save lives under <c>Application.persistentDataPath</c>, which is
    /// somewhere nobody can be expected to remember, so the progress it records is reachable from the menu bar
    /// instead of the file system.
    /// </summary>
    public static class SaveFileMenu
    {
        private const string Menu = "Window/TrainSudoku/Save File/";

        /// <summary>Prints the save as a readable table: station names rather than level ids, stars and best times.</summary>
        [MenuItem(Menu + "Show Progress in Console", priority = 100)]
        public static void ShowProgress() => Debug.Log(BuildReport());

        /// <summary>The report text, so a tool or a test can read it without going through the console.</summary>
        public static string BuildReport()
        {
            var path = GameManager.SaveFilePath;
            if (!File.Exists(path)) return $"No save file yet. It appears at {path} once a level is played.";

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (IOException e)
            {
                return $"Could not read the save file: {e.Message}";
            }

            if (!SaveJson.TryRead(text, out var data)) return $"The save file at {path} is not valid JSON.\n\n{text}";

            var sb = new StringBuilder();
            sb.AppendLine($"TrainSudoku save (format version {data.Version})");
            sb.AppendLine(path);
            sb.AppendLine();

            var collection = FindCollection();
            if (collection == null)
            {
                sb.AppendLine("No LevelCollection found, so ids are shown rather than station names.");
                foreach (var pair in data.BestTimes)
                    sb.AppendLine($"  {pair.Key}: {ProgressTracker.FormatTime(pair.Value)}" +
                                  (data.Stars.TryGetValue(pair.Key, out var s) ? $"  {Stars(s)}" : ""));
            }
            else
            {
                sb.AppendLine("  STATION            STARS   BEST      IN PROGRESS");
                var totalStars = 0;
                for (var i = 0; i < collection.Count; i++)
                {
                    var level = collection[i];
                    if (level == null) continue;

                    var name = string.IsNullOrEmpty(level.DisplayName) ? level.name : level.DisplayName;
                    var stars = data.Stars.TryGetValue(level.Id, out var count) ? count : 0;
                    totalStars += stars;

                    var best = data.BestTimes.TryGetValue(level.Id, out var seconds)
                        ? ProgressTracker.FormatTime(seconds)
                        : "-";
                    var resume = data.InProgress.TryGetValue(level.Id, out var progress) && progress != null
                        ? $"{progress.Pieces.Count} pieces at {ProgressTracker.FormatTime(progress.Elapsed)}"
                        : "";

                    sb.AppendLine($"  {name,-18} {Stars(stars),-7} {best,-9} {resume}");
                }

                sb.AppendLine();
                sb.AppendLine($"  {totalStars} of {collection.Count * 3} stars on the line.");
            }

            // Levels in the file that are not in the shipped collection: an id that was renamed, or a level dropped.
            foreach (var pair in data.BestTimes)
                if (collection != null && !Contains(collection, pair.Key))
                    sb.AppendLine($"  (orphaned id '{pair.Key}' in the save; no shipped level claims it)");

            return sb.ToString();
        }

        [MenuItem(Menu + "Show Raw JSON in Console", priority = 101)]
        public static void ShowRaw()
        {
            var path = GameManager.SaveFilePath;
            if (!File.Exists(path)) { Debug.Log($"No save file yet at {path}."); return; }
            Debug.Log($"{path}\n\n{File.ReadAllText(path)}");
        }

        [MenuItem(Menu + "Reveal in Finder", priority = 102)]
        public static void Reveal() => EditorUtility.RevealInFinder(GameManager.SaveFilePath);

        [MenuItem(Menu + "Delete Save File", priority = 200)]
        public static void Delete()
        {
            var path = GameManager.SaveFilePath;
            if (!File.Exists(path)) { Debug.Log($"No save file at {path}."); return; }
            if (!EditorUtility.DisplayDialog("Delete save file",
                    $"Delete every best time, star and unlock?\n\n{path}", "Delete", "Cancel")) return;

            File.Delete(path);
            Debug.Log($"Deleted {path}. Restart Play mode to see the effect.");
        }

        private static string Stars(int count) => count <= 0 ? "-" : new string('*', count);

        private static bool Contains(LevelCollection collection, string id)
        {
            for (var i = 0; i < collection.Count; i++)
                if (collection[i] != null && collection[i].Id == id) return true;
            return false;
        }

        /// <summary>The collection the game ships, found by asset search so no scene has to be open.</summary>
        private static LevelCollection FindCollection()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(LevelCollection));
            LevelCollection best = null;
            foreach (var guid in guids)
            {
                var candidate = AssetDatabase.LoadAssetAtPath<LevelCollection>(AssetDatabase.GUIDToAssetPath(guid));
                if (candidate == null) continue;
                if (best == null || candidate.Count > best.Count) best = candidate;
            }

            return best;
        }
    }
}
