using System;
using System.Collections.Generic;
using System.Text;
using TrainSudoku.Core;
using TrainSudoku.Game;
using UnityEditor;
using UnityEngine;

namespace TrainSudoku.Editor
{
    /// <summary>
    /// Builds the twenty lines of <see cref="NetworkLines"/> and lays them out on the map. The four lines that
    /// shipped before it are never touched.
    /// </summary>
    /// <remarks>
    /// <b>Generating the boards and placing them on the map are two menu items, deliberately.</b> The map is the
    /// part that needs iterating — a corner that reads badly is a thirty-second fix — and re-running a hundred and
    /// eighty solver searches to see it would make that unaffordable. Laying out the map needs only the lines to
    /// exist, not their levels.
    ///
    /// <b>Difficulty is the deduction budget, not the board size.</b> Eight cells is the hard cap (D18), so twenty
    /// more lines cannot ramp by growing. What ramps is <c>rails - pre-laid</c>: how much of the route the player is
    /// actually asked to work out. Measuring it showed the obvious lever does not work — asking for a longer route
    /// moves that number around almost at random, because how much pre-laid track a route needs to be provably
    /// unique depends on the route, not its length. At 8x8 with a 36-cell route the budget ranged from 15 to 33
    /// across fourteen seeds. So this does not set a route length and hope: it names the budget it wants and searches
    /// seeds until it finds a board that has it. A search costs a fraction of a second, which is what makes that
    /// affordable.
    ///
    /// <b>What it still cannot author is the star thresholds.</b> They stay the placeholder ramp off the rail count
    /// that <see cref="NetworkGenerator"/> established, and D12 wants them seeded from real play. That debt was 36
    /// stations; it is now 216.
    /// </remarks>
    public static class NetworkExpansion
    {
        private const string LevelFolder = "Assets/03.Data/Levels";
        private const string NetworkPath = "Assets/03.Data/Levels/Network.asset";

        private const long SolverBudget = 2_000_000;
        private const long WalkBudget = 2_000_000;
        private const int RouteAttempts = 30;

        /// <summary>How many seeds to try before settling for the closest board to the budget asked for.</summary>
        private const int SeedAttempts = 16;

        /// <summary>Close enough to stop looking. The budget is a dial, not a specification.</summary>
        private const int GoodEnough = 1;

        /// <summary>Seconds per rail for three stars, and the multiple of that for two. A placeholder; see remarks.</summary>
        private const float ThreeStarPerRail = 4f;
        private const float TwoStarMultiple = 2f;

        // ------------------------------------------------------------------ difficulty

        /// <summary>
        /// The board a station is built on. Size ramps inside a line — three 6x6, three 7x7, three 8x8, never past
        /// the cap — and the deduction budget ramps across the whole run of twenty lines, so the last line's easy
        /// board still asks more of the player than the first line's hard one.
        /// </summary>
        /// <param name="ordinal">Which of the twenty new lines this is, 0-based.</param>
        private static (int Size, int Route, int Deduce) Target(int ordinal, int station)
        {
            var t = NetworkLines.All.Length > 1 ? ordinal / (float)(NetworkLines.All.Length - 1) : 0f;
            var step = station % 3;   // the three boards of one size, easiest first

            // The bands are what a fourteen-seed probe showed is actually reachable at each size, and the route
            // lengths are set long enough to leave headroom at the top of each band.
            //
            // The low ends are not zero-based: they start where the four lines that already shipped sit, measured
            // off their own assets (CW/RM/EF average 13 at 6x6, 20 at 7x7, 22 at 8x8). Starting lower would make
            // the first four new lines easier than the line before them and keep them that way for thirty-six
            // stations, which reads as the game going slack exactly where it should be opening up.
            if (station < 3) return (6, 22, Mathf.RoundToInt(Mathf.Lerp(13f, 20f, t)) + step);
            if (station < 6) return (7, 30, Mathf.RoundToInt(Mathf.Lerp(20f, 27f, t)) + step);
            return (8, 40, Mathf.RoundToInt(Mathf.Lerp(21f, 33f, t)) + step);
        }

        // ------------------------------------------------------------------ menu: levels

        [MenuItem("Window/TrainSudoku/Expand Network: Generate Lines")]
        public static void GenerateLines() => GenerateLines(true);

        /// <param name="showProgress">
        /// Off for a scripted run. The progress bar is the only thing here that touches the Editor's UI, and a
        /// context driving the Editor remotely cannot answer it.
        /// </param>
        public static void GenerateLines(bool showProgress)
        {
            var network = AssetDatabase.LoadAssetAtPath<NetworkDefinition>(NetworkPath);
            if (network == null)
            {
                Debug.LogError($"Expand Network: no network at {NetworkPath}.");
                return;
            }

            if (!PreflightNames(network, out var clash))
            {
                Debug.LogError($"Expand Network: {clash}\nNothing was generated.");
                return;
            }

            var report = new StringBuilder("Expand Network: generate lines");
            var lines = new List<LineDefinition>(network.Lines);
            var built = 0;

            try
            {
                for (var ordinal = 0; ordinal < NetworkLines.All.Length; ordinal++)
                {
                    var spec = NetworkLines.All[ordinal];
                    var existing = AssetDatabase.LoadAssetAtPath<LineDefinition>($"{LevelFolder}/Line_{spec.Code}.asset");
                    if (existing != null)
                    {
                        report.Append($"\n{spec.Name}: already built, skipped");
                        if (!lines.Contains(existing)) lines.Add(existing);
                        continue;
                    }

                    if (showProgress && EditorUtility.DisplayCancelableProgressBar(
                            "Expand Network",
                            $"{spec.Name} ({ordinal + 1} of {NetworkLines.All.Length})",
                            ordinal / (float)NetworkLines.All.Length))
                    {
                        report.Append("\nCancelled.");
                        break;
                    }

                    var line = BuildLine(spec, ordinal, report);
                    if (line == null)
                    {
                        report.Append($"\n{spec.Name}: ABANDONED — see the error above. Nothing further was built.");
                        break;
                    }

                    lines.Add(line);
                    built++;
                }
            }
            finally
            {
                if (showProgress) EditorUtility.ClearProgressBar();
            }

            Undo.RecordObject(network, "Expand network");
            network.SetLines(lines.ToArray());
            EditorUtility.SetDirty(network);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.Append($"\nBuilt {built} line(s). The network now carries {lines.Count}.");
            report.Append("\nRun 'Expand Network: Lay Out Map' next — a line with no map draws nothing.");
            Debug.Log(report.ToString());
        }

        /// <summary>
        /// Every station id and name across the whole network, checked before a single board is searched for. Ids are
        /// the save file's keys and must be unique; two names that differ only in punctuation collapse to one id.
        /// Half an hour of generation is a long time to find that out at the end.
        /// </summary>
        private static bool PreflightNames(NetworkDefinition network, out string clash)
        {
            var ids = new Dictionary<string, string>();
            var names = new HashSet<string>();

            foreach (var level in network.FlatLevels())
            {
                if (level == null) continue;
                ids[level.Id] = level.name;
                names.Add(level.DisplayName);
            }

            var codes = new HashSet<string>();
            for (var i = 0; i < network.LineCount; i++)
            {
                var line = network.Line(i);
                if (line != null) codes.Add(line.Code);
            }

            foreach (var spec in NetworkLines.All)
            {
                if (!codes.Add(spec.Code))
                {
                    clash = $"line code '{spec.Code}' is already in use.";
                    return false;
                }

                foreach (var station in spec.Stations)
                {
                    var id = LevelAuthoring.SuggestId(station);
                    if (ids.TryGetValue(id, out var owner))
                    {
                        clash = $"station '{station}' on {spec.Name} makes id '{id}', which {owner} already uses.";
                        return false;
                    }

                    if (!names.Add(station))
                    {
                        clash = $"station name '{station}' on {spec.Name} is used twice.";
                        return false;
                    }

                    ids[id] = station;
                }
            }

            clash = null;
            return true;
        }

        private static LineDefinition BuildLine(NetworkLines.LineSpec spec, int ordinal, StringBuilder report)
        {
            var collectionPath = $"{LevelFolder}/LevelCollection_{spec.Code}.asset";
            var collection = AssetDatabase.LoadAssetAtPath<LevelCollection>(collectionPath);
            if (collection == null)
            {
                collection = ScriptableObject.CreateInstance<LevelCollection>();
                AssetDatabase.CreateAsset(collection, collectionPath);
            }

            report.Append($"\n{spec.Name} ({spec.Code}):");
            for (var station = 0; station < spec.Stations.Length; station++)
            {
                var level = BuildStation(spec, ordinal, station, report);
                if (level == null)
                {
                    // A line short of a station renumbers nothing — the flat list is built from what is there — but
                    // it ships a map with nine stops and eight boards. Refuse it rather than write it.
                    Debug.LogError($"Expand Network: {spec.Name} station {station + 1} " +
                                   $"({spec.Stations[station]}) found no board. The line was not written.");
                    return null;
                }

                if (!collection.Contains(level)) collection.Add(level);
            }

            EditorUtility.SetDirty(collection);

            var line = ScriptableObject.CreateInstance<LineDefinition>();
            line.SetUp(spec.Id, spec.Code, spec.Name, spec.Colour, collection);
            AssetDatabase.CreateAsset(line, $"{LevelFolder}/Line_{spec.Code}.asset");
            EditorUtility.SetDirty(line);
            return line;
        }

        /// <summary>
        /// One station's board: the candidate whose deduction budget comes closest to what the ramp asked for.
        /// Re-running is free for a station that already exists, so a run that stops halfway can be resumed.
        /// </summary>
        private static LevelDefinition BuildStation(
            NetworkLines.LineSpec spec, int ordinal, int station, StringBuilder report)
        {
            var path = $"{LevelFolder}/{spec.Code}_{station + 1:00}_{Sanitise(spec.Stations[station])}.asset";
            var done = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            if (done != null) return done;

            var (size, route, wanted) = Target(ordinal, station);

            LevelData best = null;
            var bestDeduce = 0;
            var bestMiss = int.MaxValue;

            for (var attempt = 0; attempt < SeedAttempts && bestMiss > GoodEnough; attempt++)
            {
                var seed = unchecked(spec.Id.GetHashCode() * 397 + station * 7919 + attempt * 104729);
                var found = FinaleGenerator.SearchMinimal(size, size, route, RouteAttempts, seed, SolverBudget, WalkBudget, null);
                if (found == null) continue;

                // The two tunnel pieces come out of the route the search already has, so the board is anchored at
                // both ends without a second solve. They are pre-laid track like any other, so they have to be in
                // before the budget is measured.
                BakeTunnels(found.Level, found.Solution);

                var rails = 0;
                foreach (var clue in found.Level.RowClues) rails += clue;
                var deduce = rails - found.Level.FixedPieces.Count;
                var miss = Mathf.Abs(deduce - wanted);
                if (miss >= bestMiss) continue;

                best = found.Level;
                bestDeduce = deduce;
                bestMiss = miss;
            }

            if (best == null) return null;

            best.Name = spec.Stations[station];
            var total = 0;
            foreach (var clue in best.RowClues) total += clue;

            var asset = ScriptableObject.CreateInstance<LevelDefinition>();
            asset.SetFrom(best, LevelAuthoring.SuggestId(spec.Stations[station]));
            asset.SetStarTimes(
                Mathf.Round(total * ThreeStarPerRail),
                Mathf.Round(total * ThreeStarPerRail * TwoStarMultiple));
            AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);

            report.Append($"\n  {spec.Stations[station]}: {size}x{size}, {total} rails, " +
                          $"{best.FixedPieces.Count} pre-laid, deduce {bestDeduce} (wanted {wanted})");
            return asset;
        }

        /// <summary>
        /// Gives the entrance and exit cells a piece from the level's own solution, so a board never opens on two
        /// empty tunnel mouths. A piece from the single solution cannot add solutions, so uniqueness survives.
        /// </summary>
        private static void BakeTunnels(LevelData data, IReadOnlyList<FixedPiece> solution)
        {
            foreach (var tunnel in new[] { data.Entrance, data.Exit })
            {
                var x = tunnel.CellX(data.Width);
                var y = tunnel.CellY(data.Height);
                if (data.TryGetFixedPiece(x, y, out _)) continue;

                foreach (var piece in solution)
                {
                    if (piece.X != x || piece.Y != y) continue;
                    data.FixedPieces.Add(piece);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ menu: map

        /// <summary>
        /// Places every line that has stations but no map, and declares the interchange each one hangs off. Writes
        /// nothing unless the whole network validates, so a bad layout costs a message rather than a broken asset.
        /// </summary>
        [MenuItem("Window/TrainSudoku/Expand Network: Lay Out Map")]
        public static void LayOutMap()
        {
            var network = AssetDatabase.LoadAssetAtPath<NetworkDefinition>(NetworkPath);
            if (network == null)
            {
                Debug.LogError($"Expand Network: no network at {NetworkPath}.");
                return;
            }

            var targets = new List<int>();
            for (var i = 0; i < network.LineCount; i++)
            {
                var line = network.Line(i);
                if (line != null && line.HasContent && line.MapNodes.Count == 0) targets.Add(i);
            }

            if (targets.Count == 0)
            {
                Debug.Log("Expand Network: every line already has a map.");
                return;
            }

            var placed = NetworkMapLayout.Place(network, targets);
            if (placed.Count < targets.Count)
            {
                Debug.LogError($"Expand Network: only {placed.Count} of {targets.Count} lines found legal room on " +
                               "the map. Nothing was written.");
                return;
            }

            // Apply to the real assets, then ask the shipped validator whether the result is fit to ship. Writing
            // first and checking after is safe because nothing is saved until the check passes.
            var interchanges = new List<Interchange>(network.Interchanges);
            foreach (var entry in placed)
            {
                var line = network.Line(entry.Line);
                Undo.RecordObject(line, "Lay out network map");
                line.SetMap(MapShape.Route, entry.Nodes, NetworkMapLayout.StationNodes(entry.Nodes.Length));
                EditorUtility.SetDirty(line);
                interchanges.Add(new Interchange
                {
                    lineA = entry.ParentLine, nodeA = entry.ParentNode,
                    lineB = entry.Line, nodeB = 0,
                });
            }

            Undo.RecordObject(network, "Lay out network map");
            network.SetInterchanges(interchanges.ToArray());
            EditorUtility.SetDirty(network);

            var problems = LineMapValidation.ValidateNetwork(network);
            if (problems.Count > 0)
            {
                Undo.PerformUndo();
                Debug.LogError("Expand Network: the map does not validate, so it was rolled back:\n  " +
                               string.Join("\n  ", problems));
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Expand Network: laid out {placed.Count} line(s); the map validates clean.");
        }

        private static string Sanitise(string name) => name.Replace(" ", "");
    }
}
