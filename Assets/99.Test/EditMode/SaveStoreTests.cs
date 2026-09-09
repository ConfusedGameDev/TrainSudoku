using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class SaveStoreTests
    {
        [Test]
        public void InMemoryStoreRoundTrips()
        {
            ISaveStore store = new InMemorySaveStore();
            Assert.IsFalse(store.TryGetBestTime("level-1", out _));

            store.SetBestTime("level-1", 42.5);
            Assert.IsTrue(store.TryGetBestTime("level-1", out var seconds));
            Assert.AreEqual(42.5, seconds);
            Assert.IsFalse(store.TryGetBestTime("level-2", out _));

            store.SetBestTime("level-1", 30);
            Assert.IsTrue(store.TryGetBestTime("level-1", out seconds));
            Assert.AreEqual(30, seconds);
        }

        [Test]
        public void InMemoryStoreKeepsInProgressSnapshotsPerLevel()
        {
            ISaveStore store = new InMemorySaveStore();
            Assert.IsFalse(store.TryGetProgress("level-1", out _));

            var first = new LevelProgress(5, new[] { new PlacedPiece(0, 0, PieceKey.EW) });
            var second = new LevelProgress(9, null);
            store.SetProgress("level-1", first);
            store.SetProgress("level-2", second);
            Assert.IsTrue(store.TryGetProgress("level-1", out var progress));
            Assert.AreSame(first, progress);
            Assert.IsTrue(store.TryGetProgress("level-2", out progress));
            Assert.AreSame(second, progress);

            store.ClearProgress("level-1");
            store.ClearProgress("never-saved");
            Assert.IsFalse(store.TryGetProgress("level-1", out _));
            Assert.IsTrue(store.TryGetProgress("level-2", out _));
        }
    }
}
