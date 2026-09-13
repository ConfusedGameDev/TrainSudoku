using NUnit.Framework;
using TrainSudoku.Core;
using TrainSudoku.XR.Rules;

namespace TrainSudoku.XR.Tests
{
    /// <summary>XR-PRD 4.1: the tray's six slots, where it docks, handedness, and moving between edges.</summary>
    public class TrayDockTests
    {
        private static readonly (int Width, int Height)[] Sizes = { (6, 6), (7, 7), (8, 8), (6, 8), (8, 6) };
        private static readonly Hand[] Hands = { Hand.Left, Hand.Right };

        /// <summary>How far a clue sign reaches past the grid: its anchor beyond the tunnel ring plus the chip's radius.</summary>
        private const double ClueReach = BoardLayout.ClueOffset - 0.5 + 0.38;

        private static (double X, double Z) Step(Direction edge) => BoardLayout.Step(edge);
        private static (double X, double Z) Right(Direction edge) => (-Step(edge).Z, Step(edge).X);
        private static double Dot((double X, double Z) a, (double X, double Z) b) => a.X * b.X + a.Z * b.Z;
        private static int Slot(int row, int column) => row * TrayDock.Columns + column;

        [Test]
        public void EveryKeyHasExactlyOneSlot()
        {
            foreach (var key in PieceKeys.All) Assert.AreEqual(key, TrayDock.KeyAt(TrayDock.SlotOf(key)));
            var seen = new System.Collections.Generic.HashSet<PieceKey>();
            for (var slot = 0; slot < TrayDock.SlotCount; slot++) Assert.IsTrue(seen.Add(TrayDock.KeyAt(slot)), $"slot {slot}");
            Assert.AreEqual(6, seen.Count);
        }

        [Test]
        public void FromTheSouthEdgeEveryPieceSitsWhereItPoints()
        {
            Assert.AreEqual(PieceKey.SW, TrayDock.KeyAt(Slot(0, 0)));
            Assert.AreEqual(PieceKey.SE, TrayDock.KeyAt(Slot(0, 1)));
            Assert.AreEqual(PieceKey.NW, TrayDock.KeyAt(Slot(2, 0)));
            Assert.AreEqual(PieceKey.NE, TrayDock.KeyAt(Slot(2, 1)));
        }

        [Test]
        public void StartsAtTheSouthEdgeForTheRightHand()
        {
            var dock = new TrayDock();
            Assert.AreEqual(Direction.South, dock.Edge);
            Assert.AreEqual(Hand.Right, dock.DominantHand);
        }

        [Test]
        public void SouthRightHandTrayStandsOffTheRightOfTheNearEnd()
        {
            foreach (var (width, height) in Sizes)
            {
                var hw = BoardLayout.HalfWidth(width);
                var hd = BoardLayout.HalfDepth(height);
                for (var slot = 0; slot < TrayDock.SlotCount; slot++)
                {
                    var (x, z) = TrayDock.SlotCentre(slot, Direction.South, Hand.Right, width, height);
                    Assert.Greater(x - 0.5, hw, $"{width}x{height} slot {slot} overlaps the platform");
                    Assert.Less(z, -hd + TrayDock.Rows * TrayDock.Pitch, $"{width}x{height} slot {slot} runs past the near end");
                    Assert.Greater(z, -hd, $"{width}x{height} slot {slot} stands in front of the near edge");
                }
            }
        }

        [Test]
        public void OnTheEdgeItWasPlacedFromTheTrayKeepsItsPlaceFromLevelToLevel()
        {
            // The board grows away from its near edge (X17), so measure from that edge: nothing there may move.
            for (var slot = 0; slot < TrayDock.SlotCount; slot++)
            foreach (var hand in Hands)
            {
                var (x0, z0) = TrayDock.SlotCentre(slot, Direction.South, hand, 6, 6);
                var near0 = z0 + BoardLayout.HalfDepth(6);
                foreach (var (width, height) in Sizes)
                {
                    var (x, z) = TrayDock.SlotCentre(slot, Direction.South, hand, width, height);
                    Assert.AreEqual(x0, x, 1e-9, $"{width}x{height} {hand} slot {slot}");
                    Assert.AreEqual(near0, z + BoardLayout.HalfDepth(height), 1e-9, $"{width}x{height} {hand} slot {slot}");
                }
            }
        }

        [Test]
        public void SlotsStandClearOfThePlatformAndTheClueSigns()
        {
            foreach (var (width, height) in Sizes)
            foreach (var edge in DirectionExtensions.All)
            foreach (var hand in Hands)
            {
                var reachX = width / 2.0 + ClueReach;
                var reachZ = height / 2.0 + ClueReach;
                for (var slot = 0; slot < TrayDock.SlotCount; slot++)
                {
                    var (x, z) = TrayDock.SlotCentre(slot, edge, hand, width, height);
                    var clear = System.Math.Abs(x) - 0.5 > reachX || System.Math.Abs(z) - 0.5 > reachZ;
                    Assert.IsTrue(clear, $"{width}x{height} {edge} {hand} slot {slot} at ({x:F2},{z:F2})");
                }
            }
        }

        [Test]
        public void SlotsNeverOverlapEachOther()
        {
            foreach (var edge in DirectionExtensions.All)
            foreach (var hand in Hands)
                for (var a = 0; a < TrayDock.SlotCount; a++)
                for (var b = a + 1; b < TrayDock.SlotCount; b++)
                {
                    var pa = TrayDock.SlotCentre(a, edge, hand, 7, 7);
                    var pb = TrayDock.SlotCentre(b, edge, hand, 7, 7);
                    var apart = System.Math.Max(System.Math.Abs(pa.X - pb.X), System.Math.Abs(pa.Z - pb.Z));
                    Assert.GreaterOrEqual(apart, TrayDock.Pitch - 1e-9, $"{edge} {hand} slots {a} and {b}");
                }
        }

        [Test]
        public void RowsRunInwardFromTheDockedEdge()
        {
            foreach (var edge in DirectionExtensions.All)
            foreach (var hand in Hands)
                for (var column = 0; column < TrayDock.Columns; column++)
                for (var row = 1; row < TrayDock.Rows; row++)
                {
                    var nearer = TrayDock.SlotCentre(Slot(row - 1, column), edge, hand, 6, 8);
                    var further = TrayDock.SlotCentre(Slot(row, column), edge, hand, 6, 8);
                    Assert.Greater(Dot(nearer, Step(edge)), Dot(further, Step(edge)), $"{edge} {hand} row {row}");
                }
        }

        [Test]
        public void ColumnsRunLeftToRightAsThePlayerSeesThem()
        {
            foreach (var edge in DirectionExtensions.All)
            foreach (var hand in Hands)
                for (var row = 0; row < TrayDock.Rows; row++)
                {
                    var left = TrayDock.SlotCentre(Slot(row, 0), edge, hand, 8, 6);
                    var right = TrayDock.SlotCentre(Slot(row, 1), edge, hand, 8, 6);
                    Assert.Greater(Dot(right, Right(edge)), Dot(left, Right(edge)), $"{edge} {hand} row {row}");
                }
        }

        [Test]
        public void TheTraySitsOnTheDominantHandSide()
        {
            foreach (var edge in DirectionExtensions.All)
                for (var slot = 0; slot < TrayDock.SlotCount; slot++)
                {
                    Assert.Greater(Dot(TrayDock.SlotCentre(slot, edge, Hand.Right, 7, 6), Right(edge)), 0, $"{edge} right, slot {slot}");
                    Assert.Less(Dot(TrayDock.SlotCentre(slot, edge, Hand.Left, 7, 6), Right(edge)), 0, $"{edge} left, slot {slot}");
                }
        }

        [Test]
        public void TheLeftHandTrayMirrorsTheRightHandOne()
        {
            foreach (var edge in DirectionExtensions.All)
                for (var row = 0; row < TrayDock.Rows; row++)
                for (var column = 0; column < TrayDock.Columns; column++)
                {
                    // Mirrored across the line through the centre that runs out through the docked edge.
                    var right = TrayDock.SlotCentre(Slot(row, TrayDock.Columns - 1 - column), edge, Hand.Right, 6, 7);
                    var left = TrayDock.SlotCentre(Slot(row, column), edge, Hand.Left, 6, 7);
                    var n = Step(edge);
                    var along = 2 * Dot(right, n);
                    Assert.AreEqual(along * n.X - right.X, left.X, 1e-9, $"{edge} row {row} column {column}");
                    Assert.AreEqual(along * n.Z - right.Z, left.Z, 1e-9, $"{edge} row {row} column {column}");
                }
        }

        [Test]
        public void OnASquareBoardEveryEdgeIsTheSouthLayoutTurned()
        {
            // South to east to north to west: a quarter turn anticlockwise, seen from above, each time.
            var edges = new[] { Direction.East, Direction.North, Direction.West };
            for (var turns = 1; turns <= edges.Length; turns++)
            foreach (var hand in Hands)
                for (var slot = 0; slot < TrayDock.SlotCount; slot++)
                {
                    var (x, z) = TrayDock.SlotCentre(slot, Direction.South, hand, 7, 7);
                    for (var i = 0; i < turns; i++) (x, z) = (-z, x);
                    var expected = TrayDock.SlotCentre(slot, edges[turns - 1], hand, 7, 7);
                    Assert.AreEqual(expected.X, x, 1e-9, $"{edges[turns - 1]} {hand}, slot {slot}");
                    Assert.AreEqual(expected.Z, z, 1e-9, $"{edges[turns - 1]} {hand}, slot {slot}");
                }
        }

        [Test]
        public void NearestEdgeIsTheOneThePlayerStandsBy()
        {
            Assert.AreEqual(Direction.South, TrayDock.NearestEdge(0, -10, 6, 6));
            Assert.AreEqual(Direction.North, TrayDock.NearestEdge(1, 10, 6, 6));
            Assert.AreEqual(Direction.East, TrayDock.NearestEdge(10, -1, 6, 6));
            Assert.AreEqual(Direction.West, TrayDock.NearestEdge(-10, 2, 6, 6));
            // Leaning over the board, nearer its east side than its south one.
            Assert.AreEqual(Direction.East, TrayDock.NearestEdge(3.5, -2, 6, 6));
            // Past a corner, the edge the player is squarer to wins.
            Assert.AreEqual(Direction.South, TrayDock.NearestEdge(5, -9, 6, 6));
        }

        [Test]
        public void MovesOnceThePlayerHasStoodByAnotherEdgeForTheDwell()
        {
            var dock = new TrayDock();
            Assert.IsFalse(dock.Update(10, 0, 6, 6, 1.4, false));
            Assert.AreEqual(Direction.South, dock.Edge);
            Assert.IsTrue(dock.Update(10, 0, 6, 6, 0.2, false));
            Assert.AreEqual(Direction.East, dock.Edge);
            Assert.IsFalse(dock.Update(10, 0, 6, 6, 5, false), "reports a move only once");
        }

        [Test]
        public void NeverMovesWhileAPieceIsHeld()
        {
            var dock = new TrayDock();
            for (var i = 0; i < 30; i++) Assert.IsFalse(dock.Update(10, 0, 6, 6, 0.1, true));
            Assert.AreEqual(Direction.South, dock.Edge);
            // Holding restarts the wait: the full dwell runs again from letting go.
            Assert.IsFalse(dock.Update(10, 0, 6, 6, 1.0, false));
            Assert.AreEqual(Direction.South, dock.Edge);
            Assert.IsTrue(dock.Update(10, 0, 6, 6, 0.6, false));
        }

        [Test]
        public void SteppingBackToTheDockedEdgeRestartsTheWait()
        {
            var dock = new TrayDock();
            dock.Update(10, 0, 6, 6, 1.0, false);
            dock.Update(0, -10, 6, 6, 0.1, false);
            Assert.IsFalse(dock.Update(10, 0, 6, 6, 1.0, false));
            Assert.AreEqual(Direction.South, dock.Edge);
        }

        [Test]
        public void ChangingToAThirdEdgeRestartsTheWait()
        {
            var dock = new TrayDock();
            dock.Update(10, 0, 6, 6, 1.0, false);
            Assert.IsFalse(dock.Update(0, 10, 6, 6, 1.0, false));
            Assert.AreEqual(Direction.South, dock.Edge);
            Assert.IsTrue(dock.Update(0, 10, 6, 6, 0.6, false));
            Assert.AreEqual(Direction.North, dock.Edge);
        }

        [Test]
        public void DockAtMovesStraightAway()
        {
            var dock = new TrayDock(Hand.Left);
            dock.DockAt(Direction.West);
            Assert.AreEqual(Direction.West, dock.Edge);
            Assert.AreEqual(dock.SlotCentre(3, 6, 6), TrayDock.SlotCentre(3, Direction.West, Hand.Left, 6, 6));
        }

        [Test]
        public void ASlotOutOfRangeThrows()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => TrayDock.SlotCentre(6, Direction.South, Hand.Right, 6, 6));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => TrayDock.KeyAt(-1));
        }
    }
}
