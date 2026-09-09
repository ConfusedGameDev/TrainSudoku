using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class PieceKeyTests
    {
        [TestCase(PieceKey.NS, Direction.North, Direction.South)]
        [TestCase(PieceKey.EW, Direction.East, Direction.West)]
        [TestCase(PieceKey.NW, Direction.North, Direction.West)]
        [TestCase(PieceKey.NE, Direction.North, Direction.East)]
        [TestCase(PieceKey.SW, Direction.South, Direction.West)]
        [TestCase(PieceKey.SE, Direction.South, Direction.East)]
        public void ConnectionsMatchTheKeyName(PieceKey key, Direction a, Direction b)
        {
            Assert.IsTrue(PieceKeys.Has(key, a));
            Assert.IsTrue(PieceKeys.Has(key, b));
            Assert.AreEqual(b, PieceKeys.Other(key, a));
            Assert.AreEqual(a, PieceKeys.Other(key, b));
            foreach (var direction in DirectionExtensions.All)
                if (direction != a && direction != b)
                    Assert.IsFalse(PieceKeys.Has(key, direction), $"{key} should not connect {direction}");
        }

        [Test]
        public void EveryPairOfDistinctDirectionsMapsToAKeyInEitherOrder()
        {
            foreach (var key in PieceKeys.All)
            {
                var (a, b) = PieceKeys.Connections(key);
                Assert.IsTrue(PieceKeys.TryFromDirections(a, b, out var fromAb));
                Assert.IsTrue(PieceKeys.TryFromDirections(b, a, out var fromBa));
                Assert.AreEqual(key, fromAb);
                Assert.AreEqual(key, fromBa);
            }
        }

        [Test]
        public void SameDirectionTwiceIsNotAKey()
        {
            foreach (var direction in DirectionExtensions.All)
                Assert.IsFalse(PieceKeys.TryFromDirections(direction, direction, out _));
        }

        [Test]
        public void ParseRoundTripsAndRejectsGarbage()
        {
            foreach (var key in PieceKeys.All)
            {
                Assert.IsTrue(PieceKeys.TryParse(key.ToString(), out var parsed));
                Assert.AreEqual(key, parsed);
            }

            Assert.IsTrue(PieceKeys.TryParse(" ne ", out var lower));
            Assert.AreEqual(PieceKey.NE, lower);
            Assert.IsFalse(PieceKeys.TryParse("0", out _));
            Assert.IsFalse(PieceKeys.TryParse("NN", out _));
            Assert.IsFalse(PieceKeys.TryParse(".", out _));
            Assert.IsFalse(PieceKeys.TryParse(null, out _));
        }

        [Test]
        public void OppositeDirections()
        {
            Assert.AreEqual(Direction.South, Direction.North.Opposite());
            Assert.AreEqual(Direction.North, Direction.South.Opposite());
            Assert.AreEqual(Direction.West, Direction.East.Opposite());
            Assert.AreEqual(Direction.East, Direction.West.Opposite());
        }
    }
}
