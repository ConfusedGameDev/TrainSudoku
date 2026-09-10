using System.Collections.Generic;
using System.Text;
using TrainSudoku.Core;
using TrainSudoku.Game;
using UnityEditor;
using UnityEngine;

namespace TrainSudoku.Editor
{
    /// <summary>
    /// Gives the entrance and exit cells of a level a fixed piece, read from the level's own solution, so a puzzle is
    /// always anchored at both ends instead of opening on two empty tunnel mouths.
    /// </summary>
    /// <remarks>
    /// The piece is part of the single solution the level already has, so baking it in can only remove solutions that
    /// were never there: uniqueness survives, and so do <see cref="LevelData.Validate"/>'s clue-versus-fixed-count
    /// checks, because the solution satisfies every clue by definition. A cell that already carries a fixed piece is
    /// left exactly as the author left it — this only fills the gaps.
    ///
    /// Solving is the expensive part, so it runs once per level and only when a level actually needs it. A level the
    /// solver cannot finish within the budget is reported and skipped rather than half-written.
    /// </remarks>
    public static class TunnelPieceBaker
    {
        private const string CollectionPath = "Assets/03.Data/Levels/LevelCollection.asset";

        /// <summary>The same budget the Level Editor's "Search longer" and "Fill from solver" use.</summary>
        private const long SolveBudget = 20_000_000;

        [MenuItem("Window/TrainSudoku/Bake Tunnel Pieces")]
        public static void BakeCollection()
        {
            var collection = AssetDatabase.LoadAssetAtPath<LevelCollection>(CollectionPath);
            if (collection == null)
            {
                Debug.LogError($"Bake Tunnel Pieces: no level collection at {CollectionPath}.");
                return;
            }

            var report = new StringBuilder("Bake Tunnel Pieces");
            var changed = 0;
            for (var i = 0; i < collection.Count; i++)
            {
                var level = collection[i];
                if (level == null) continue;
                report.Append('\n').Append(level.DisplayName).Append(": ").Append(Bake(level, out var wrote));
                if (wrote) changed++;
            }

            if (changed > 0) AssetDatabase.SaveAssets();
            report.Append($"\n{changed} of {collection.Count} levels changed.");
            Debug.Log(report.ToString());
        }

        /// <summary>
        /// Fills in whichever of the two tunnel cells has no fixed piece yet. Returns a one-line report and, through
        /// <paramref name="wrote"/>, whether the asset was touched.
        /// </summary>
        public static string Bake(LevelDefinition definition, out bool wrote)
        {
            wrote = false;
            if (definition == null) return "no level";

            var data = definition.ToLevelData();
            var wanted = new List<(int X, int Y)>();
            foreach (var tunnel in new[] { data.Entrance, data.Exit })
            {
                var cell = (tunnel.CellX(data.Width), tunnel.CellY(data.Height));
                if (!data.TryGetFixedPiece(cell.Item1, cell.Item2, out _) && !wanted.Contains(cell)) wanted.Add(cell);
            }

            if (wanted.Count == 0) return "both ends already fixed";

            var result = Solver.Solve(data, 1, SolveBudget);
            if (result.First == null)
                return result.Exhausted
                    ? "skipped: the solver ran out of budget before finding a solution"
                    : "skipped: no solution";

            var laid = new List<string>();
            foreach (var (x, y) in wanted)
            {
                var piece = result.First[x, y];
                if (!piece.HasValue) return $"skipped: the solution leaves ({x},{y}) empty";
                LevelAuthoring.SetFixedPiece(data, x, y, piece.Value.Key);
                laid.Add($"{piece.Value.Key} at ({x},{y})");
            }

            var problems = data.Validate();
            if (problems.Count > 0) return $"skipped: {string.Join("; ", problems)}";

            Undo.RecordObject(definition, "Bake tunnel pieces");
            definition.SetFrom(data);
            EditorUtility.SetDirty(definition);
            wrote = true;
            return "fixed " + string.Join(", ", laid);
        }
    }
}
