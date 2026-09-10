using NUnit.Framework;
using TrainSudoku.Editor;
using UnityEngine;

namespace TrainSudoku.Tests
{
    /// <summary>The 45-degree snap grid the Line Map Editor drags nodes on (M18).</summary>
    public class MapGridTests
    {
        private const float Grid = 50f;

        [Test]
        public void SnapToGridRoundsToTheNearestLatticePoint()
        {
            Assert.AreEqual(new Vector2(100f, 150f), MapGrid.SnapToGrid(new Vector2(113f, 131f), Grid));
            Assert.AreEqual(new Vector2(-50f, 0f), MapGrid.SnapToGrid(new Vector2(-41f, -12f), Grid));
        }

        [Test]
        public void AlignedAcceptsTheEightDirectionsAndNothingElse()
        {
            var origin = new Vector2(100f, 100f);
            Assert.IsTrue(MapGrid.IsAligned(origin, new Vector2(400f, 100f)), "east");
            Assert.IsTrue(MapGrid.IsAligned(origin, new Vector2(100f, -200f)), "north");
            Assert.IsTrue(MapGrid.IsAligned(origin, new Vector2(300f, 300f)), "south-east");
            Assert.IsTrue(MapGrid.IsAligned(origin, new Vector2(-50f, 250f)), "south-west");
            Assert.IsFalse(MapGrid.IsAligned(origin, new Vector2(300f, 200f)), "a 2:1 run is not a diagram segment");
        }

        [Test]
        public void AZeroLengthSegmentIsNotAligned()
        {
            Assert.IsFalse(MapGrid.IsAligned(new Vector2(100f, 100f), new Vector2(100f, 100f)));
        }

        [Test]
        public void WithoutAlignmentTheNodeOnlyLandsOnTheGrid()
        {
            var previous = new Vector2(0f, 0f);
            var snapped = MapGrid.Snap(new Vector2(312f, 91f), previous, null, Grid, false);
            Assert.AreEqual(new Vector2(300f, 100f), snapped);
        }

        [Test]
        public void AlignmentPullsTheNodeOntoADiagonalOutOfItsNeighbour()
        {
            // Dragged towards, but not onto, the 45-degree ray leaving (0, 0).
            var snapped = MapGrid.Snap(new Vector2(312f, 291f), Vector2.zero, null, Grid, true);
            Assert.IsTrue(MapGrid.IsAligned(Vector2.zero, snapped), $"{snapped} is not on a compass direction");
            Assert.AreEqual(new Vector2(300f, 300f), snapped);
        }

        [Test]
        public void AlignmentFindsTheCornerThatSatisfiesBothNeighbours()
        {
            // A node between a run coming from the west and one leaving to the south: the corner is (600, 200).
            var previous = new Vector2(100f, 200f);
            var next = new Vector2(600f, 700f);
            var snapped = MapGrid.Snap(new Vector2(571f, 233f), previous, next, Grid, true);

            Assert.IsTrue(MapGrid.IsAligned(previous, snapped), "the segment in is not aligned");
            Assert.IsTrue(MapGrid.IsAligned(next, snapped), "the segment out is not aligned");
            Assert.AreEqual(new Vector2(600f, 200f), snapped);
        }

        [Test]
        public void AlignmentHoldsEvenWhenThePointerIsNowhereNearALegalPoint()
        {
            // The whole point of the toggle: a free drag cannot author a segment the validator would then reject.
            var previous = new Vector2(0f, 0f);
            var snapped = MapGrid.Snap(new Vector2(1000f, 375f), previous, null, Grid, true);
            Assert.IsTrue(MapGrid.IsAligned(previous, snapped), $"{snapped} is not on a compass direction");
            Assert.AreEqual(snapped, MapGrid.SnapToGrid(snapped, Grid), "and it left the grid");
        }

        [Test]
        public void EveryDragOfEveryShapeLandsSomewhereLegal()
        {
            var previous = new Vector2(200f, 400f);
            var next = new Vector2(800f, 900f);
            for (var x = -400f; x <= 1400f; x += 37f)
                for (var y = -400f; y <= 1400f; y += 41f)
                {
                    var snapped = MapGrid.Snap(new Vector2(x, y), previous, next, Grid, true);
                    Assert.IsTrue(MapGrid.IsAligned(previous, snapped) || MapGrid.IsAligned(next, snapped),
                        $"a drag to ({x}, {y}) landed at {snapped}, which is on neither neighbour's compass");
                }
        }

        [Test]
        public void ANodeNeverSnapsOnTopOfItsNeighbour()
        {
            var previous = new Vector2(200f, 200f);
            var snapped = MapGrid.Snap(new Vector2(206f, 197f), previous, null, Grid, true);
            Assert.AreNotEqual(previous, snapped);
        }

        [Test]
        public void TheShippedLineIsAlreadyOnTheGridTheToolAuthorsOn()
        {
            var nodes = new[]
            {
                new Vector2(100f, 200f), new Vector2(400f, 200f), new Vector2(600f, 400f), new Vector2(600f, 700f),
                new Vector2(400f, 900f), new Vector2(400f, 1150f), new Vector2(650f, 1400f), new Vector2(900f, 1400f),
                new Vector2(900f, 1150f),
            };

            foreach (var node in nodes) Assert.AreEqual(node, MapGrid.SnapToGrid(node, Grid), $"{node} is off the grid");
            for (var i = 1; i < nodes.Length; i++)
                Assert.IsTrue(MapGrid.IsAligned(nodes[i - 1], nodes[i]), $"segment {i - 1} is not a diagram segment");
        }
    }
}
