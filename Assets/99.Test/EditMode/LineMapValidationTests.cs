using System.Collections.Generic;
using NUnit.Framework;
using TrainSudoku.Editor;
using TrainSudoku.Game;
using UnityEditor;
using UnityEngine;

namespace TrainSudoku.Tests
{
    /// <summary>
    /// The Line Map Editor's validation (M18). Every case builds a map that a hand-typed asset could plausibly hold
    /// and asserts the tool would have caught it.
    /// </summary>
    public class LineMapValidationTests
    {
        private const string LinePath = "Assets/03.Data/Levels/Line_TS.asset";
        private const string NetworkPath = "Assets/03.Data/Levels/Network.asset";

        private readonly List<Object> _created = new List<Object>();

        /// <summary>A clean four-node dog-leg: east, south-east, south. Three stations on it, in order.</summary>
        private static Vector2[] Straight() => new[]
        {
            new Vector2(0f, 0f), new Vector2(200f, 0f), new Vector2(400f, 200f), new Vector2(400f, 400f),
        };

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
                if (asset != null) Object.DestroyImmediate(asset);
            _created.Clear();
        }

        private LineDefinition Line(string code, Color colour, Vector2[] nodes, int[] stations)
        {
            var line = ScriptableObject.CreateInstance<LineDefinition>();
            line.hideFlags = HideFlags.HideAndDontSave;
            line.SetUp(code.ToLowerInvariant(), code, code, colour, null);
            line.SetMap(MapShape.Route, nodes, stations);
            _created.Add(line);
            return line;
        }

        private NetworkDefinition Network(params LineDefinition[] lines)
        {
            var network = ScriptableObject.CreateInstance<NetworkDefinition>();
            network.hideFlags = HideFlags.HideAndDontSave;
            network.SetLines(lines);
            _created.Add(network);
            return network;
        }

        /// <summary>The asset has no setter for interchanges, so the tests write them the way an inspector would.</summary>
        private static void SetInterchanges(NetworkDefinition network, params Interchange[] interchanges)
        {
            var serialized = new SerializedObject(network);
            var array = serialized.FindProperty("interchanges");
            array.arraySize = interchanges.Length;
            for (var i = 0; i < interchanges.Length; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("lineA").intValue = interchanges[i].lineA;
                element.FindPropertyRelative("nodeA").intValue = interchanges[i].nodeA;
                element.FindPropertyRelative("lineB").intValue = interchanges[i].lineB;
                element.FindPropertyRelative("nodeB").intValue = interchanges[i].nodeB;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssertClean(List<string> problems) =>
            Assert.IsEmpty(problems, problems.Count > 0 ? string.Join(" / ", problems) : "");

        private static void AssertReports(List<string> problems, string fragment) =>
            Assert.IsTrue(problems.Exists(p => p.Contains(fragment)),
                $"expected a problem mentioning '{fragment}', got: {string.Join(" / ", problems)}");

        // ------------------------------------------------------------------ one line

        [Test]
        public void AWellFormedMapHasNoProblems()
        {
            AssertClean(LineMapValidation.ValidateLine(Straight(), new[] { 0, 2, 3 }, 3, MapShape.Route));
        }

        [Test]
        public void AMapNeedsTwoNodes()
        {
            AssertReports(
                LineMapValidation.ValidateLine(new[] { Vector2.zero }, new[] { 0 }, 1, MapShape.Route),
                "at least two nodes");
        }

        [Test]
        public void ASegmentOffTheFortyFiveDegreeGridIsReported()
        {
            var nodes = Straight();
            nodes[2] = new Vector2(430f, 200f);   // the diagonal now runs 23:10
            AssertReports(LineMapValidation.ValidateLine(nodes, new[] { 0, 2, 3 }, 3, MapShape.Route), "45 degrees");
        }

        [Test]
        public void AStrokeThatDoublesBackOverItselfIsReported()
        {
            var nodes = new[]
            {
                new Vector2(0f, 0f), new Vector2(400f, 0f), new Vector2(200f, 0f), new Vector2(200f, 300f),
            };

            AssertReports(LineMapValidation.ValidateLine(nodes, new[] { 0, 1, 3 }, 3, MapShape.Route), "run over each other");
        }

        [Test]
        public void AStraightRunSplitIntoTwoSegmentsIsNotAnOverlap()
        {
            var nodes = new[] { new Vector2(0f, 0f), new Vector2(200f, 0f), new Vector2(400f, 0f) };
            AssertClean(LineMapValidation.ValidateLine(nodes, new[] { 0, 1, 2 }, 3, MapShape.Route));
        }

        [Test]
        public void TwoNodesOnTheSamePointAreReported()
        {
            var nodes = new[] { new Vector2(0f, 0f), new Vector2(200f, 0f), new Vector2(200f, 0f) };
            AssertReports(LineMapValidation.ValidateLine(nodes, new[] { 0, 1, 2 }, 3, MapShape.Route), "same point");
        }

        [Test]
        public void AStationOffTheEndOfTheNodesIsReported()
        {
            AssertReports(
                LineMapValidation.ValidateLine(Straight(), new[] { 0, 2, 9 }, 3, MapShape.Route), "not on a node");
        }

        [Test]
        public void TwoStationsOnOneNodeAreReported()
        {
            AssertReports(
                LineMapValidation.ValidateLine(Straight(), new[] { 0, 2, 2 }, 3, MapShape.Route), "share node");
        }

        [Test]
        public void FewerAssignmentsThanStationsIsReported()
        {
            AssertReports(LineMapValidation.ValidateLine(Straight(), new[] { 0, 2 }, 3, MapShape.Route), "node assignment");
        }

        [Test]
        public void StationsThatRunBackwardsAlongTheRouteAreReported()
        {
            AssertReports(
                LineMapValidation.ValidateLine(Straight(), new[] { 3, 2, 0 }, 3, MapShape.Route), "does not follow the route");
        }

        [Test]
        public void ALoopClosesBackToItsFirstNode()
        {
            var nodes = new[]
            {
                new Vector2(0f, 0f), new Vector2(300f, 0f), new Vector2(300f, 300f), new Vector2(0f, 300f),
            };

            AssertClean(LineMapValidation.ValidateLine(nodes, new[] { 0, 1, 2, 3 }, 4, MapShape.Loop));
        }

        // ------------------------------------------------------------------ the network

        [Test]
        public void TwoLinesSharingANodeNeedAnInterchange()
        {
            var first = Line("AA", Color.red, Straight(), new[] { 0, 2, 3 });
            var second = Line("BB", Color.blue,
                new[] { new Vector2(400f, 400f), new Vector2(700f, 400f) }, new[] { 0, 1 });

            var network = Network(first, second);
            AssertReports(LineMapValidation.ValidateNetwork(network), "no interchange between them");

            SetInterchanges(network, new Interchange { lineA = 0, nodeA = 3, lineB = 1, nodeB = 0 });
            AssertClean(LineMapValidation.ValidateNetwork(network));
        }

        [Test]
        public void AnInterchangeBetweenNodesInDifferentPlacesIsReported()
        {
            var first = Line("AA", Color.red, Straight(), new[] { 0, 2, 3 });
            var second = Line("BB", Color.blue,
                new[] { new Vector2(900f, 900f), new Vector2(1200f, 900f) }, new[] { 0, 1 });

            var network = Network(first, second);
            SetInterchanges(network, new Interchange { lineA = 0, nodeA = 3, lineB = 1, nodeB = 0 });
            AssertReports(LineMapValidation.ValidateNetwork(network), "not in the same place");
        }

        [Test]
        public void AnInterchangeSharedByMoreThanTwoLinesIsReported()
        {
            var meeting = new Vector2(400f, 400f);
            var first = Line("AA", Color.red, Straight(), new[] { 0, 2, 3 });
            var second = Line("BB", Color.blue, new[] { meeting, new Vector2(700f, 400f) }, new[] { 0, 1 });
            var third = Line("CC", Color.green, new[] { meeting, new Vector2(400f, 700f) }, new[] { 0, 1 });

            var network = Network(first, second, third);
            SetInterchanges(network,
                new Interchange { lineA = 0, nodeA = 3, lineB = 1, nodeB = 0 },
                new Interchange { lineA = 0, nodeA = 3, lineB = 2, nodeB = 0 });

            AssertReports(LineMapValidation.ValidateNetwork(network), "reuses");
        }

        [Test]
        public void TwoLinesRunningDownTheSameCorridorAreReported()
        {
            var first = Line("AA", Color.red,
                new[] { new Vector2(0f, 0f), new Vector2(400f, 0f) }, new[] { 0, 1 });
            var second = Line("BB", Color.blue,
                new[] { new Vector2(100f, 0f), new Vector2(300f, 0f) }, new[] { 0, 1 });

            AssertReports(LineMapValidation.ValidateNetwork(Network(first, second)), "runs over");
        }

        // ------------------------------------------------------------------ what ships

        [Test]
        public void TheShippedNetworkValidates()
        {
            var network = AssetDatabase.LoadAssetAtPath<NetworkDefinition>(NetworkPath);
            Assert.IsNotNull(network, $"Missing {NetworkPath}");
            AssertClean(LineMapValidation.ValidateNetwork(network));
        }

        [Test]
        public void TheShippedLineValidates()
        {
            var line = AssetDatabase.LoadAssetAtPath<LineDefinition>(LinePath);
            Assert.IsNotNull(line, $"Missing {LinePath}");
            AssertClean(LineMapValidation.ValidateLine(line));
        }
    }
}
