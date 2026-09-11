using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrainSudoku.Game;
using UnityEditor;
using UnityEngine;

namespace TrainSudoku.Tests
{
    /// <summary>
    /// The reveal rule behind the overworld's pan-out: the map shows the lines the player has earned plus the one
    /// they are working towards, and nothing beyond. The framing is fitted to exactly that set, so this rule is what
    /// decides how far out the camera sits — which makes it worth a test even though the drawing itself is not.
    /// </summary>
    public class NetworkMapRevealTests
    {
        private const string NetworkPath = "Assets/03.Data/Levels/Network.asset";

        private static NetworkDefinition LoadNetwork()
        {
            var network = AssetDatabase.LoadAssetAtPath<NetworkDefinition>(NetworkPath);
            Assert.IsNotNull(network, $"Missing {NetworkPath}");
            return network;
        }

        /// <summary>The content-bearing lines, which are the only ones the map ever draws (D10).</summary>
        private static List<int> ContentLines(NetworkDefinition network)
        {
            var lines = new List<int>();
            for (var i = 0; i < network.LineCount; i++)
            {
                var line = network.Line(i);
                if (line != null && line.HasContent && line.MapNodes.Count > 1) lines.Add(i);
            }

            return lines;
        }

        private static IReadOnlyList<int> Reveal(NetworkDefinition network, int unlockedCount)
        {
            var map = new NetworkMapElement();
            map.SetNetwork(network, i => i < unlockedCount);
            map.Refresh();
            return map.VisibleLines;
        }

        [Test]
        public void AFreshPlayerSeesTheFirstLineAndTheOneAfterIt()
        {
            var network = LoadNetwork();
            var content = ContentLines(network);
            Assert.GreaterOrEqual(content.Count, 2, "this test needs a network of at least two content lines");

            // Nothing earned but line 0, which GameFlow always opens.
            var revealed = Reveal(network, 1);
            Assert.AreEqual(new[] { content[0], content[1] }, revealed,
                "a new player should see the line they are on and the one they are working towards, and no more");
        }

        [Test]
        public void EachLineEarnedRevealsExactlyOneMore()
        {
            var network = LoadNetwork();
            var content = ContentLines(network);

            for (var unlocked = 1; unlocked < content.Count; unlocked++)
            {
                var revealed = Reveal(network, unlocked);
                Assert.AreEqual(unlocked + 1, revealed.Count,
                    $"with {unlocked} line(s) open the map should show {unlocked + 1}");
            }
        }

        /// <summary>
        /// The whole point of the pan-out: the revealed set only ever grows, so the fit it is computed from only
        /// ever widens and the view never zooms back in on a player who has just earned something.
        /// </summary>
        [Test]
        public void TheRevealedSetOnlyEverGrows()
        {
            var network = LoadNetwork();
            var content = ContentLines(network);

            var previous = new List<int>(Reveal(network, 1));
            for (var unlocked = 2; unlocked <= content.Count; unlocked++)
            {
                var revealed = Reveal(network, unlocked);
                Assert.GreaterOrEqual(revealed.Count, previous.Count, "the map lost a line it had already shown");
                foreach (var line in previous)
                    Assert.IsTrue(revealed.Contains(line), $"line {line} was revealed and then hidden again");
                previous = new List<int>(revealed);
            }
        }

        [Test]
        public void EverythingIsRevealedOnceTheWholeNetworkIsOpen()
        {
            var network = LoadNetwork();
            var content = ContentLines(network);

            var revealed = Reveal(network, network.LineCount);
            Assert.AreEqual(content, revealed, "a finished player should see the whole network");
        }

        /// <summary>A preview or a tool with no unlock predicate gets the whole map, as it always did.</summary>
        [Test]
        public void WithNoUnlockRuleTheWholeNetworkIsDrawn()
        {
            var network = LoadNetwork();
            var map = new NetworkMapElement();
            map.SetNetwork(network);
            map.Refresh();
            Assert.AreEqual(ContentLines(network), map.VisibleLines);
        }
    }
}
