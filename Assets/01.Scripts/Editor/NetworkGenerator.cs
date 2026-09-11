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
    /// Builds three more lines of nine stations each, and registers them on the network. This is the window
    /// <see cref="FinaleGenerator"/>'s remark asked for: that search authored one board by hand-run command, and this
    /// runs it twenty-seven times against a table of names, sizes and seeds.
    /// </summary>
    /// <remarks>
    /// <b>Difficulty is the board.</b> Each line ramps three 6x6, then three 7x7, then three 8x8 — D18 caps a shipped
    /// board at 8 — with the route-length floor rising with it, so a later station fills more of a bigger grid. The
    /// generator's own reveal ladder then decides how much pre-laid track each one needs to be provably unique, which
    /// means a hard board is not merely bigger: it is bigger with a longer route through it.
    ///
    /// <b>The seeds are in the table.</b> The search is deterministic, so a row of this table is the provenance of its
    /// asset the way <c>Search(7, 7, 29, 60, 20260910, ...)</c> is the provenance of Ravensmoor. Re-running the menu
    /// item rebuilds exactly the same twenty-seven boards; it refuses to overwrite assets that already exist, so it is
    /// safe to run twice.
    ///
    /// <b>What it cannot author</b> is the star thresholds. D12 wants them seeded from real play and
    /// <see cref="LevelDefinition"/> says plainly that solver node count measures search-tree size rather than how
    /// long a person takes. The numbers written here are a placeholder ramp off the rail count — they satisfy the
    /// "two usable thresholds" test and nothing more, and every one of them should be replaced once someone has
    /// actually played these boards.
    /// </remarks>
    public static class NetworkGenerator
    {
        private const string LevelFolder = "Assets/03.Data/Levels";
        private const string NetworkPath = "Assets/03.Data/Levels/Network.asset";

        /// <summary>Search effort per board. The probe that sized these ran 6x6 in 0.2 s, 7x7 in 2.2 s, 8x8 in 3.6 s.</summary>
        private const long SolverBudget = 2_000_000;
        private const long WalkBudget = 300_000;
        private const int Attempts = 60;

        /// <summary>Seconds per rail for three stars, and the multiple of that for two. A placeholder ramp; see the remarks.</summary>
        private const float ThreeStarPerRail = 4f;
        private const float TwoStarMultiple = 2f;

        private sealed class LineSpec
        {
            public string Id;
            public string Code;
            public string Name;
            public Color Colour;

            /// <summary>Where this line's route sits in network space; every line keeps its own column.</summary>
            public float MapX;

            public string[] Stations;
        }

        private static readonly LineSpec[] Lines =
        {
            new LineSpec
            {
                Id = "cw", Code = "CW", Name = "Calderwyke", Colour = new Color32(0x3A, 0x7B, 0xD5, 0xFF), MapX = 1150f,
                Stations = new[]
                {
                    "Marlbrook", "Netherby", "Glasswater",
                    "Fenwick Cross", "Coldharbour", "Stillmarsh",
                    "Brackenmere", "Tarnhow", "Winterlode",
                },
            },
            new LineSpec
            {
                Id = "rm", Code = "RM", Name = "Rosemere", Colour = new Color32(0xE8, 0x6A, 0xA6, 0xFF), MapX = 1750f,
                Stations = new[]
                {
                    "Appleford", "Petrivale", "Blushmoor",
                    "Harrowgate", "Silkstone", "Mallowfield",
                    "Quillhaven", "Roseacre", "Vermilion Park",
                },
            },
            new LineSpec
            {
                Id = "ef", Code = "EF", Name = "Emberfell", Colour = new Color32(0xF0, 0x8A, 0x24, 0xFF), MapX = 2350f,
                Stations = new[]
                {
                    "Kilnwick", "Ashcombe", "Forgebridge",
                    "Copperdown", "Smeltham", "Ironhaugh",
                    "Cindermoor", "Bellowgate", "Emberfell Central",
                },
            },
        };

        /// <summary>The three tiers, in station order: three easy, three medium, three hard.</summary>
        private static (int Size, int Route) Tier(int station) =>
            station < 3 ? (6, 16) : station < 6 ? (7, 24) : (8, 30);

        [MenuItem("Window/TrainSudoku/Generate Lines")]
        public static void Generate()
        {
            var report = new StringBuilder("Generate Lines");
            var network = AssetDatabase.LoadAssetAtPath<NetworkDefinition>(NetworkPath);
            if (network == null)
            {
                Debug.LogError($"Generate Lines: no network at {NetworkPath}.");
                return;
            }

            var lines = new List<LineDefinition>(network.Lines);
            foreach (var spec in Lines)
            {
                var existing = AssetDatabase.LoadAssetAtPath<LineDefinition>($"{LevelFolder}/Line_{spec.Code}.asset");
                if (existing != null)
                {
                    report.Append($"\n{spec.Name}: already built, skipped");
                    if (!lines.Contains(existing)) lines.Add(existing);
                    continue;
                }

                var line = BuildLine(spec, report);
                if (line != null) lines.Add(line);
            }

            Undo.RecordObject(network, "Generate lines");
            network.SetLines(lines.ToArray());
            EditorUtility.SetDirty(network);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.Append($"\nThe network now carries {lines.Count} lines.");
            Debug.Log(report.ToString());
        }

        private static LineDefinition BuildLine(LineSpec spec, StringBuilder report)
        {
            var collection = ScriptableObject.CreateInstance<LevelCollection>();
            AssetDatabase.CreateAsset(collection, $"{LevelFolder}/LevelCollection_{spec.Code}.asset");

            for (var station = 0; station < spec.Stations.Length; station++)
            {
                var level = BuildStation(spec, station, report);
                if (level == null) continue;
                collection.Add(level);
            }

            EditorUtility.SetDirty(collection);

            var line = ScriptableObject.CreateInstance<LineDefinition>();
            line.SetUp(spec.Id, spec.Code, spec.Name, spec.Colour, collection);
            line.SetMap(MapShape.Route, RouteNodes(spec.MapX), StationNodes(spec.Stations.Length));
            AssetDatabase.CreateAsset(line, $"{LevelFolder}/Line_{spec.Code}.asset");
            EditorUtility.SetDirty(line);
            report.Append($"\n{spec.Name} ({spec.Code}): {collection.Count} stations");
            return line;
        }

        private static LevelDefinition BuildStation(LineSpec spec, int station, StringBuilder report)
        {
            var (size, route) = Tier(station);
            // One seed per station, from the line and its position, so a rebuild is the same board.
            var seed = unchecked(spec.Id.GetHashCode() * 397 + station * 7919);
            var found = FinaleGenerator.Search(size, size, route, Attempts, seed, SolverBudget, WalkBudget, null);
            if (found == null)
            {
                report.Append($"\n  {spec.Stations[station]}: no unique board found");
                return null;
            }

            var data = found.Level;
            data.Name = spec.Stations[station];

            var rails = 0;
            foreach (var clue in data.RowClues) rails += clue;

            var asset = ScriptableObject.CreateInstance<LevelDefinition>();
            asset.SetFrom(data, LevelAuthoring.SuggestId(spec.Stations[station]));
            asset.SetStarTimes(Mathf.Round(rails * ThreeStarPerRail), Mathf.Round(rails * ThreeStarPerRail * TwoStarMultiple));
            AssetDatabase.CreateAsset(asset, $"{LevelFolder}/{spec.Code}_{station + 1:00}_{Sanitise(spec.Stations[station])}.asset");
            EditorUtility.SetDirty(asset);

            report.Append($"\n  {spec.Stations[station]}: {size}x{size}, {rails} rails, {found.FixedCount} pre-laid");
            return asset;
        }

        /// <summary>
        /// A line's route down its own column of network space, jogging at 45 degrees so it reads as a route rather
        /// than a bare vertical. Every segment is axis-aligned or diagonal, which is what the map validator asks of a
        /// <see cref="MapShape.Route"/>, and the columns are 600 apart so no two lines' nodes can be mistaken for an
        /// undeclared interchange.
        /// </summary>
        private static Vector2[] RouteNodes(float x)
        {
            var nodes = new Vector2[9];
            for (var i = 0; i < nodes.Length; i++)
            {
                // Out to the right for the middle of each pair, back for the next: a gentle zigzag on the lattice.
                var offset = i % 4 == 2 || i % 4 == 3 ? 150f : 0f;
                nodes[i] = new Vector2(x + offset, 300f + i * 150f);
            }

            return nodes;
        }

        private static int[] StationNodes(int count)
        {
            var indices = new int[count];
            for (var i = 0; i < count; i++) indices[i] = i;
            return indices;
        }

        private static string Sanitise(string name) => name.Replace(" ", "");
    }
}
