using System.Collections.Generic;
using NUnit.Framework;
using TrainSudoku.Editor;
using TrainSudoku.Game;
using UnityEditor;
using UnityEngine;

namespace TrainSudoku.Tests
{
    /// <summary>
    /// <see cref="NetworkMapLayout"/> against the real shipped network. The map has to survive
    /// <see cref="LineMapValidation.ValidateNetwork"/> — strokes that run over each other, nodes in the same place
    /// with no interchange, and a node claimed twice are all rejected — and twenty freehand polylines in one plane
    /// would trip every one of those. This is the test that says the construction actually avoids them.
    /// </summary>
    public class NetworkMapLayoutTests
    {
        private const string NetworkPath = "Assets/03.Data/Levels/Network.asset";
        private const int NewLines = 20;

        /// <summary>The shipped network with the new lines appended, mapless, and then laid out — the real flow.</summary>
        private sealed class Laid
        {
            public NetworkDefinition Network;
            public List<LineDefinition> Lines;
            public List<int> Targets;
            public List<NetworkMapLayout.PlacedLine> Placed;
            public int CoreCount;
        }

        private static Laid LayOut()
        {
            var source = AssetDatabase.LoadAssetAtPath<NetworkDefinition>(NetworkPath);
            Assert.IsNotNull(source, $"Missing {NetworkPath}");

            var lines = new List<LineDefinition>();
            for (var i = 0; i < source.LineCount; i++) lines.Add(source.Line(i));
            var core = lines.Count;

            var targets = new List<int>();
            for (var i = 0; i < NewLines; i++)
            {
                var line = ScriptableObject.CreateInstance<LineDefinition>();
                line.SetUp($"probe{i}", $"P{i}", $"Probe {i}", Color.cyan, null);
                targets.Add(lines.Count);
                lines.Add(line);
            }

            var network = ScriptableObject.CreateInstance<NetworkDefinition>();
            network.SetLines(lines.ToArray());
            network.SetInterchanges(new List<Interchange>(source.Interchanges).ToArray());

            var placed = NetworkMapLayout.Place(network, targets);

            var interchanges = new List<Interchange>(source.Interchanges);
            foreach (var entry in placed)
            {
                lines[entry.Line].SetMap(
                    MapShape.Route, entry.Nodes, NetworkMapLayout.StationNodes(entry.Nodes.Length));
                interchanges.Add(new Interchange
                {
                    lineA = entry.ParentLine, nodeA = entry.ParentNode,
                    lineB = entry.Line, nodeB = 0,
                });
            }

            network.SetInterchanges(interchanges.ToArray());
            return new Laid
            {
                Network = network, Lines = lines, Targets = targets, Placed = placed, CoreCount = core,
            };
        }

        [Test]
        public void EveryNewLineFindsLegalRoom()
        {
            var laid = LayOut();
            Assert.AreEqual(NewLines, laid.Placed.Count,
                "the layout ran out of room; a line with no map draws nothing, so a short result is a failure");
        }

        /// <summary>The gate. Everything else in this file is a more specific way of saying why it passes.</summary>
        [Test]
        public void TheLaidOutNetworkPassesTheValidator()
        {
            var laid = LayOut();
            var problems = LineMapValidation.ValidateNetwork(laid.Network);
            Assert.IsEmpty(problems, "map problems:\n  " + string.Join("\n  ", problems));
        }

        /// <summary>
        /// The network is a tree rooted in the lines that already shipped: one interchange per new line, hanging off
        /// a line laid before it. That is what gives every line an anchor for the opening animation to run out from,
        /// and it is what keeps the map growing outward rather than sprouting islands.
        /// </summary>
        [Test]
        public void EveryNewLineHangsOffALineLaidBeforeIt()
        {
            var laid = LayOut();
            var laidSoFar = new HashSet<int>();
            for (var i = 0; i < laid.CoreCount; i++) laidSoFar.Add(i);

            foreach (var entry in laid.Placed)
            {
                Assert.IsTrue(laidSoFar.Contains(entry.ParentLine),
                    $"line {entry.Line} hangs off line {entry.ParentLine}, which is not on the map yet");
                laidSoFar.Add(entry.Line);
            }
        }

        /// <summary>Node 0 is the shared node, so it must actually sit on the parent's node it claims.</summary>
        [Test]
        public void EveryInterchangeJoinsTwoNodesInTheSamePlace()
        {
            var laid = LayOut();
            foreach (var entry in laid.Placed)
            {
                var parent = laid.Lines[entry.ParentLine].MapNodes[entry.ParentNode];
                Assert.IsTrue(LineMapValidation.SamePoint(parent, entry.Nodes[0]),
                    $"line {entry.Line} node 0 at {entry.Nodes[0]} does not meet its parent at {parent}");
            }
        }

        /// <summary>
        /// A closed line's padlock is drawn at its last node. A branch taken from there would sit under the badge,
        /// so the layout never offers a terminus as a parent.
        /// </summary>
        [Test]
        public void NoLineBranchesFromAnotherLinesTerminus()
        {
            var laid = LayOut();
            foreach (var entry in laid.Placed)
            {
                var parent = laid.Lines[entry.ParentLine];
                Assert.AreNotEqual(parent.MapNodes.Count - 1, entry.ParentNode,
                    $"line {entry.Line} branches from the terminus of line {entry.ParentLine}, under its lock badge");
            }
        }

        [Test]
        public void EveryLineCarriesAStationOnEveryNode()
        {
            var laid = LayOut();
            foreach (var entry in laid.Placed)
            {
                Assert.AreEqual(NetworkMapLayout.NodesPerLine, entry.Nodes.Length,
                    $"line {entry.Line} has {entry.Nodes.Length} nodes");
                var stations = NetworkMapLayout.StationNodes(entry.Nodes.Length);
                for (var i = 0; i < stations.Length; i++) Assert.AreEqual(i, stations[i]);
            }
        }

        /// <summary>
        /// Every segment on one of the eight compass directions. The validator checks this too, but failing here
        /// says "the shape repertoire is wrong" rather than "the network is invalid somewhere".
        /// </summary>
        [Test]
        public void EverySegmentIsAxisAlignedOrDiagonal()
        {
            var laid = LayOut();
            foreach (var entry in laid.Placed)
                for (var i = 0; i + 1 < entry.Nodes.Length; i++)
                    Assert.IsTrue(MapGrid.IsAligned(entry.Nodes[i], entry.Nodes[i + 1]),
                        $"line {entry.Line} segment {i} runs {entry.Nodes[i]} to {entry.Nodes[i + 1]}");
        }

        /// <summary>
        /// The map as it actually ships is fit to be shipped. Every other test here places <i>probe</i> lines to
        /// prove the algorithm still works; this one reads the asset, because that is what the player sees.
        /// </summary>
        [Test]
        public void TheShippedNetworkMapValidates()
        {
            var network = AssetDatabase.LoadAssetAtPath<NetworkDefinition>(NetworkPath);
            var problems = LineMapValidation.ValidateNetwork(network);
            Assert.IsEmpty(problems, "map problems:\n  " + string.Join("\n  ", problems));
        }

        /// <summary>Every line on the map is drawn, and a line with no nodes draws nothing.</summary>
        [Test]
        public void EveryShippedLineWithStationsHasAMap()
        {
            var network = AssetDatabase.LoadAssetAtPath<NetworkDefinition>(NetworkPath);
            for (var i = 0; i < network.LineCount; i++)
            {
                var line = network.Line(i);
                if (line == null || !line.HasContent) continue;
                Assert.Greater(line.MapNodes.Count, 1, $"{line.Code} has stations but no map to draw them on");
                Assert.AreEqual(line.StationCount, line.StationNodeIndices.Count,
                    $"{line.Code} has {line.StationCount} stations against {line.StationNodeIndices.Count} nodes");
            }
        }

        /// <summary>
        /// The framing the player ends up with. The map fits the whole network into the portrait strip below the
        /// HUD, and if the layout sprawls the fully-revealed map stops being readable — stations end up closer
        /// together than the stroke that joins them.
        /// </summary>
        [Test]
        public void TheFullyRevealedMapIsStillLegible()
        {
            // The shipped network, not a probe layout: this is about what the last unlock actually looks like.
            var network = AssetDatabase.LoadAssetAtPath<NetworkDefinition>(NetworkPath);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < network.LineCount; i++)
                foreach (var node in network.Line(i).MapNodes)
                {
                    min = Vector2.Min(min, node);
                    max = Vector2.Max(max, node);
                }

            var span = max - min;
            // The map element in a 1080x1920 portrait panel, less the signage band, legend and back button.
            var available = new Vector2(1080f - 144f, 1218f - 144f);
            var scale = Mathf.Min(available.x / span.x, available.y / span.y);

            var pitch = NetworkMapLayout.Grid * scale;
            Assert.Greater(pitch, 24f,
                $"the whole network fits at {scale:0.000}, putting neighbouring stations {pitch:0.0}px apart");
        }
    }
}
