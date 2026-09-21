using NUnit.Framework;
using TrainSudoku.Game;
using UnityEditor;
using UnityEngine;

namespace TrainSudoku.Tests
{
    /// <summary>
    /// Tapping an interchange ring. The ring marks a junction between two lines, and both of them have a node in
    /// exactly the same place — so the map's ordinary nearest-node test cannot tell them apart and resolves the tie
    /// towards the lower index, which is always the parent. The ring therefore used to open the line the player was
    /// already looking at. It now opens the line it leads to.
    /// </summary>
    public class NetworkMapHitTests
    {
        private const string NetworkPath = "Assets/03.Data/Levels/Network.asset";

        private static NetworkDefinition LoadNetwork()
        {
            var network = AssetDatabase.LoadAssetAtPath<NetworkDefinition>(NetworkPath);
            Assert.IsNotNull(network, $"Missing {NetworkPath}");
            return network;
        }

        /// <summary>
        /// A map with the first <paramref name="unlockedCount"/> lines open. The fit is passed to the hit test as
        /// the identity, so a ring's centre is its own map node and no panel or layout is needed.
        /// </summary>
        private static NetworkMapElement Map(NetworkDefinition network, int unlockedCount)
        {
            var map = new NetworkMapElement();
            map.SetNetwork(network, i => i < unlockedCount);
            map.Refresh();
            return map;
        }

        /// <summary>The map node an interchange's ring is drawn on.</summary>
        private static Vector2 RingCentre(NetworkDefinition network, Interchange interchange) =>
            network.Line(interchange.lineA).MapNodes[interchange.nodeA];

        [Test]
        public void TheShippedNetworkAlwaysNamesTheNewerLineSecond()
        {
            var network = LoadNetwork();
            Assert.Greater(network.Interchanges.Count, 0, "the shipped network is a tree of joined lines");

            foreach (var interchange in network.Interchanges)
            {
                Assert.Greater(interchange.lineB, interchange.lineA,
                    "the layout hangs each new line off an older one, so lineB is the line the ring leads to");
                Assert.AreEqual(0, interchange.nodeB, "and it joins at the new line's first node");
            }
        }

        [Test]
        public void TappingARingOpensTheLineItLeadsToNotTheOneItSitsOn()
        {
            var network = LoadNetwork();
            var interchange = network.Interchanges[0];

            // Both of the ring's lines open, so the line it leads to is a legal destination.
            var map = Map(network, interchange.lineB + 1);
            var hit = map.InterchangeAt(RingCentre(network, interchange), Vector2.zero, 1f);

            Assert.AreEqual(interchange.lineB, hit, "the ring should change onto the newer line");
            Assert.AreNotEqual(interchange.lineA, hit, "and never back onto the line it is drawn on");
        }

        /// <summary>
        /// A ring is drawn as soon as both its lines are revealed, and the revealed set runs one line past the last
        /// unlocked one — so a ring pointing at a line the player has not earned is a legitimate thing to see. It
        /// must decline the tap rather than swallow it, leaving the ordinary node test to answer.
        /// </summary>
        [Test]
        public void ARingPointingAtALockedLineDeclinesTheTap()
        {
            var network = LoadNetwork();
            var interchange = network.Interchanges[0];

            // Only the parent line is open, so the ring is drawn but its destination is still closed.
            var map = Map(network, interchange.lineA + 1);
            var hit = map.InterchangeAt(RingCentre(network, interchange), Vector2.zero, 1f);

            Assert.AreEqual(-1, hit, "a locked destination falls through to the ordinary line hit test");
        }

        [Test]
        public void ATapAwayFromEveryRingHitsNothing()
        {
            var network = LoadNetwork();
            var interchange = network.Interchanges[0];
            var map = Map(network, network.LineCount);

            var far = RingCentre(network, interchange) + new Vector2(10000f, 10000f);
            Assert.AreEqual(-1, map.InterchangeAt(far, Vector2.zero, 1f));
        }
    }
}
