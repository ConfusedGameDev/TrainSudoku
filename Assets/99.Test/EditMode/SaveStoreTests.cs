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
    }
}
