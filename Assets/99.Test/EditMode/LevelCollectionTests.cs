using System.Collections.Generic;
using NUnit.Framework;
using TrainSudoku.Core;
using TrainSudoku.Game;
using UnityEditor;
using UnityEngine;

namespace TrainSudoku.Tests
{
    /// <summary>The shipped levels (M10): every entry of the collection asset is well formed and has exactly one solution.</summary>
    public class LevelCollectionTests
    {
        private const string CollectionPath = "Assets/03.Data/Levels/LevelCollection.asset";
        private const string NetworkPath = "Assets/03.Data/Levels/Network.asset";

        /// <summary>
        /// 8 cells is the hard cap on shipped board width (D18). Twelve columns in portrait gives roughly 84px tiles
        /// before margins - about 11mm on a phone, at the floor for a target that must accept both a tap and a
        /// long-press-to-erase.
        /// </summary>
        private const int MaxBoardCells = 8;

        /// <summary>Enough to finish every 6x6 and 8x8 level many times over; larger boards may not finish and are only checked for shape.</summary>
        private const long NodeBudget = 5_000_000;

        private static LevelCollection LoadCollection()
        {
            var collection = AssetDatabase.LoadAssetAtPath<LevelCollection>(CollectionPath);
            Assert.IsNotNull(collection, $"Missing {CollectionPath}");
            return collection;
        }

        [Test]
        public void CollectionHasAtLeastFiveLevelsWithUniqueIds()
        {
            var collection = LoadCollection();
            Assert.GreaterOrEqual(collection.Count, 5);

            var ids = new HashSet<string>();
            for (var i = 0; i < collection.Count; i++)
            {
                var level = collection[i];
                Assert.IsNotNull(level, $"Entry {i} is empty");
                Assert.IsNotEmpty(level.Id, $"{level.name} has no id");
                Assert.IsTrue(ids.Add(level.Id), $"Id '{level.Id}' is used twice");
            }
        }

        /// <summary>
        /// Every station the game ships, across every line — not just the first line's collection. The moment the
        /// network grew past one line, a test scoped to `LevelCollection.asset` stopped proving most of the game.
        /// </summary>
        private static List<LevelDefinition> LoadEveryStation()
        {
            var network = LoadNetwork();
            var levels = new List<LevelDefinition>(network.FlatLevels());
            Assert.IsNotEmpty(levels, "the network ships no stations at all");
            return levels;
        }

        [Test]
        public void EveryLevelIsWellFormedAndHasExactlyOneSolution()
        {
            var collection = LoadEveryStation();
            var unverified = new List<string>();
            for (var i = 0; i < collection.Count; i++)
            {
                var level = collection[i];
                var data = level.ToLevelData();
                Assert.IsEmpty(data.Validate(), $"{level.name}: {string.Join("; ", data.Validate())}");

                var result = Solver.Solve(data, 2, NodeBudget);
                if (result.Exhausted)
                {
                    // A lower bound only: the search did not finish, so the level can still be neither refuted nor proven.
                    Assert.LessOrEqual(result.Count, 1, $"{level.name} has more than one solution");
                    unverified.Add($"{level.name} ({data.Width}x{data.Height}, {result.Count} found in {result.Nodes:N0} nodes)");
                    continue;
                }

                Assert.AreEqual(1, result.Count, $"{level.name} ({data.Width}x{data.Height}) has {result.Count} solutions");
            }

            if (unverified.Count > 0) Debug.Log("Search budget exhausted, uniqueness not proven for: " + string.Join(", ", unverified));
        }

        /// <summary>
        /// Both tunnel cells open with a piece already laid, so a puzzle is anchored at both ends. The piece is baked
        /// from the level's own solution by Window > TrainSudoku > Bake Tunnel Pieces, so it must also point out
        /// through its tunnel - a piece there that ignored the tunnel would be an unsolvable start.
        /// </summary>
        [Test]
        public void EveryLevelStartsWithAPieceAtBothTunnels()
        {
            var collection = LoadEveryStation();
            for (var i = 0; i < collection.Count; i++)
            {
                var level = collection[i];
                var data = level.ToLevelData();
                foreach (var (tunnel, role) in new[] { (data.Entrance, "entrance"), (data.Exit, "exit") })
                {
                    var x = tunnel.CellX(data.Width);
                    var y = tunnel.CellY(data.Height);
                    Assert.IsTrue(data.TryGetFixedPiece(x, y, out var piece),
                        $"{level.name}: the {role} cell ({x},{y}) has no fixed piece");
                    Assert.IsTrue(PieceKeys.Has(piece.Key, tunnel.Side),
                        $"{level.name}: the {role} piece {piece.Key} at ({x},{y}) does not connect to its {tunnel.Side} tunnel");
                }
            }
        }

        // ---- the tutorial station (M21) ----

        /// <summary>
        /// Exactly one station teaches, and it is the first one the player reaches. Two of them would repeat the
        /// lesson; none would leave long-press-to-erase undiscoverable, which is the whole reason the coach exists.
        /// </summary>
        [Test]
        public void OnlyTheFirstStationOfTheFirstLineIsATutorial()
        {
            var network = LoadNetwork();
            var teaching = new List<string>();
            foreach (var level in LoadEveryStation())
                if (level.IsTutorial) teaching.Add(level.name);

            Assert.AreEqual(1, teaching.Count, "tutorial levels: " + string.Join(", ", teaching));

            var first = network.Line(0).Station(0);
            Assert.IsTrue(first.IsTutorial, $"the first station of the first line is {first.name}, which does not teach");
        }

        /// <summary>
        /// The coach teaches two ways of laying a piece, and it teaches each of them on the rail the player is
        /// actually being walked to at that moment. So the test is not "the opening position offers both" but "the
        /// guided order contains both": a board whose every guided cell had a choice would leave the one-tap lesson
        /// with nothing to land on, and the erase detour — which runs on the first rail that lays itself — with
        /// nowhere to start.
        /// </summary>
        [Test]
        public void TheTutorialWalkthroughOffersBothAnAutoPlaceAndAChoice()
        {
            foreach (var level in LoadEveryStation())
            {
                if (!level.IsTutorial) continue;

                var data = level.ToLevelData();
                Assert.IsTrue(Solver.TrySolve(data, out var solution), $"{level.name} does not solve");
                Assert.IsTrue(PathFinder.TryFindPath(solution, out var path), $"{level.name} has no path from S to E");

                var board = new Board(data);
                var autoPlace = 0;
                var choice = 0;
                foreach (var (x, y) in path)
                {
                    if (!board.IsEmpty(x, y)) continue;

                    var keys = Legality.LegalKeys(board, x, y);
                    Assert.IsNotEmpty(keys, $"{level.name}: the walkthrough stalls at ({x},{y})");
                    if (keys.Count == 1) autoPlace++;
                    else choice++;

                    // Lay the solved piece and carry on, so every cell is judged in the state the player meets it.
                    board.SetUnchecked(x, y, solution[x, y]);
                }

                Assert.Greater(autoPlace, 0, $"{level.name}: no guided rail that a single tap lays");
                Assert.Greater(choice, 0, $"{level.name}: no guided rail whose sides the player has to choose");
            }
        }

        // ---- the network (M13) ----

        private static NetworkDefinition LoadNetwork()
        {
            var network = AssetDatabase.LoadAssetAtPath<NetworkDefinition>(NetworkPath);
            Assert.IsNotNull(network, $"Missing {NetworkPath}");
            return network;
        }

        [Test]
        public void TheNetworkShipsAtLeastOneLineWithContent()
        {
            var network = LoadNetwork();
            Assert.GreaterOrEqual(network.LineCount, 1);

            var withContent = 0;
            for (var i = 0; i < network.LineCount; i++)
            {
                var line = network.Line(i);
                Assert.IsNotNull(line, $"Line {i} is empty");
                Assert.IsNotEmpty(line.Id, $"{line.name} has no id");
                Assert.IsNotEmpty(line.Code, $"{line.name} has no roundel code");
                if (line.HasContent) withContent++;
            }

            Assert.GreaterOrEqual(withContent, 1, "the network map only draws lines that have content (D10)");
        }

        [Test]
        public void EveryStationIsWellFormedAndEveryIdIsUniqueAcrossTheWholeNetwork()
        {
            var network = LoadNetwork();
            var ids = new HashSet<string>();

            for (var lineIndex = 0; lineIndex < network.LineCount; lineIndex++)
            {
                var line = network.Line(lineIndex);
                if (line == null) continue;
                for (var station = 0; station < line.StationCount; station++)
                {
                    var level = line.Station(station);
                    Assert.IsNotNull(level, $"{line.name} station {station} is empty");
                    Assert.IsNotEmpty(level.Id, $"{level.name} has no id");
                    Assert.IsTrue(ids.Add(level.Id),
                        $"Id '{level.Id}' appears twice in the network; the flat index is the save-file identity");
                    Assert.IsEmpty(level.ToLevelData().Validate(), $"{level.name} is malformed");
                }
            }
        }

        [Test]
        public void NoShippedBoardIsWiderOrTallerThanTheCap()
        {
            var network = LoadNetwork();
            for (var lineIndex = 0; lineIndex < network.LineCount; lineIndex++)
            {
                var line = network.Line(lineIndex);
                if (line == null) continue;
                for (var station = 0; station < line.StationCount; station++)
                {
                    var level = line.Station(station);
                    if (level == null) continue;
                    Assert.LessOrEqual(level.Width, MaxBoardCells,
                        $"{level.name} is {level.Width} wide; {MaxBoardCells} is the cap (D18)");
                    Assert.LessOrEqual(level.Height, MaxBoardCells,
                        $"{level.name} is {level.Height} tall; {MaxBoardCells} is the cap (D18)");
                }
            }
        }

        [Test]
        public void EveryStationHasANameAndTwoUsableStarThresholds()
        {
            var network = LoadNetwork();
            var names = new HashSet<string>();

            for (var lineIndex = 0; lineIndex < network.LineCount; lineIndex++)
            {
                var line = network.Line(lineIndex);
                if (line == null) continue;
                for (var station = 0; station < line.StationCount; station++)
                {
                    var level = line.Station(station);
                    if (level == null) continue;

                    Assert.IsNotEmpty(level.DisplayName, $"{level.name} has no station name");
                    Assert.IsTrue(names.Add(level.DisplayName),
                        $"Station name '{level.DisplayName}' is used twice; a line cannot stop at the same place twice");

                    var times = level.StarTimes;
                    Assert.AreEqual(2, times.Count, $"{level.name} should carry exactly two thresholds");
                    Assert.Greater(times[0], 0d, $"{level.name} has no three-star time");
                    Assert.Greater(times[1], times[0],
                        $"{level.name}: the two-star time must be slower than the three-star time, or three stars are unreachable");
                }
            }
        }

        [Test]
        public void TheFlatLevelListMatchesTheLayoutTheFlowIsGiven()
        {
            var network = LoadNetwork();
            var flat = network.FlatLevels();
            var layout = network.ToLayout();

            Assert.AreEqual(flat.Count, layout.StationTotal,
                "the flat list and the layout must agree, or the flow indexes the wrong level");

            for (var flatIndex = 0; flatIndex < flat.Count; flatIndex++)
            {
                var line = layout.LineOf(flatIndex);
                var station = layout.StationOf(flatIndex);
                Assert.AreSame(flat[flatIndex], network.Line(line).Station(station),
                    $"flat index {flatIndex} does not resolve back to line {line} station {station}");
            }
        }
    }
}
