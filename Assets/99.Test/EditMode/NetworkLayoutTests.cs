using System;
using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    /// <summary>
    /// How the flat level list divides into lines (M13). The flat index stays the save-file identity, so these tests
    /// are really about one promise: adding a line must not renumber the ones before it.
    /// </summary>
    public class NetworkLayoutTests
    {
        private static readonly string[] Ids = { "a", "b", "c", "d", "e" };

        [Test]
        public void ASingleLineHoldsEveryLevel()
        {
            var layout = NetworkLayout.Single(5);
            Assert.AreEqual(1, layout.LineCount);
            Assert.AreEqual(5, layout.StationCount(0));
            Assert.AreEqual(5, layout.StationTotal);
            Assert.AreEqual(0, layout.LineOf(4));
            Assert.AreEqual(4, layout.StationOf(4));
            Assert.IsTrue(layout.IsTerminus(4));
            Assert.IsFalse(layout.IsTerminus(3));
        }

        [Test]
        public void StationsMapBothWays()
        {
            var layout = new NetworkLayout(new[] { 2, 3 });
            Assert.AreEqual(5, layout.StationTotal);

            Assert.AreEqual(0, layout.FlatIndex(0, 0));
            Assert.AreEqual(1, layout.FlatIndex(0, 1));
            Assert.AreEqual(2, layout.FlatIndex(1, 0));
            Assert.AreEqual(4, layout.FlatIndex(1, 2));

            for (var flat = 0; flat < 5; flat++)
                Assert.AreEqual(flat, layout.FlatIndex(layout.LineOf(flat), layout.StationOf(flat)), $"flat {flat}");
        }

        [Test]
        public void EachLineHasItsOwnTerminus()
        {
            var layout = new NetworkLayout(new[] { 2, 3 });
            Assert.IsTrue(layout.IsTerminus(1), "last station of line 0");
            Assert.IsFalse(layout.IsTerminus(2), "first station of line 1");
            Assert.IsTrue(layout.IsTerminus(4), "last station of line 1");
        }

        [Test]
        public void AddingALineDoesNotRenumberTheOneBeforeIt()
        {
            var before = NetworkLayout.Single(2);
            var after = new NetworkLayout(new[] { 2, 3 });
            for (var flat = 0; flat < 2; flat++)
            {
                Assert.AreEqual(before.LineOf(flat), after.LineOf(flat), $"flat {flat} changed line");
                Assert.AreEqual(before.StationOf(flat), after.StationOf(flat), $"flat {flat} changed station");
            }
        }

        [Test]
        public void LineIdsAreTheLineSlice()
        {
            var layout = new NetworkLayout(new[] { 2, 3 });
            CollectionAssert.AreEqual(new[] { "a", "b" }, layout.LineIds(Ids, 0));
            CollectionAssert.AreEqual(new[] { "c", "d", "e" }, layout.LineIds(Ids, 1));
            CollectionAssert.IsEmpty(layout.LineIds(Ids, 2));
        }

        [Test]
        public void OutOfRangeAsksAnswerRatherThanThrow()
        {
            var layout = new NetworkLayout(new[] { 2, 3 });
            Assert.AreEqual(-1, layout.FlatIndex(0, 5));
            Assert.AreEqual(-1, layout.FlatIndex(9, 0));
            Assert.AreEqual(-1, layout.LineOf(-1));
            Assert.AreEqual(-1, layout.LineOf(99));
            Assert.AreEqual(-1, layout.StationOf(99));
            Assert.AreEqual(0, layout.StationCount(9));
            Assert.IsFalse(layout.IsTerminus(99));
        }

        [Test]
        public void AnEmptyLineIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new NetworkLayout(new[] { 2, 0 }));
            Assert.Throws<ArgumentNullException>(() => new NetworkLayout(null));
        }
    }
}
