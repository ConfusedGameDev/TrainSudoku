using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class ProgressTrackerTests
    {
        private static readonly string[] Ids = { "a", "b", "c" };

        [Test]
        public void OnlyTheFirstLevelStartsUnlocked()
        {
            var progress = new ProgressTracker(new InMemorySaveStore());
            Assert.IsTrue(progress.IsUnlocked(Ids, 0));
            Assert.IsFalse(progress.IsUnlocked(Ids, 1));
            Assert.IsFalse(progress.IsUnlocked(Ids, 2));
            Assert.IsFalse(progress.IsUnlocked(Ids, -1));
            Assert.IsFalse(progress.IsUnlocked(Ids, 3));
        }

        [Test]
        public void CompletingALevelUnlocksTheNextOne()
        {
            var progress = new ProgressTracker(new InMemorySaveStore());
            progress.RecordCompletion("a", 30);
            Assert.IsTrue(progress.IsUnlocked(Ids, 1));
            Assert.IsFalse(progress.IsUnlocked(Ids, 2));
        }

        [Test]
        public void FirstCompletionIsANewBest()
        {
            var progress = new ProgressTracker(new InMemorySaveStore());
            var result = progress.RecordCompletion("a", 30);
            Assert.AreEqual(30, result.Time);
            Assert.AreEqual(30, result.BestTime);
            Assert.IsTrue(result.IsNewBest);
            Assert.IsTrue(progress.TryGetBestTime("a", out var best));
            Assert.AreEqual(30, best);
        }

        [Test]
        public void SlowerRunKeepsTheOldBest()
        {
            var progress = new ProgressTracker(new InMemorySaveStore());
            progress.RecordCompletion("a", 30);
            var result = progress.RecordCompletion("a", 45);
            Assert.AreEqual(45, result.Time);
            Assert.AreEqual(30, result.BestTime);
            Assert.IsFalse(result.IsNewBest);
        }

        [Test]
        public void EqualRunIsNotANewBest()
        {
            var progress = new ProgressTracker(new InMemorySaveStore());
            progress.RecordCompletion("a", 30);
            Assert.IsFalse(progress.RecordCompletion("a", 30).IsNewBest);
        }

        [Test]
        public void FasterRunReplacesTheBest()
        {
            var store = new InMemorySaveStore();
            var progress = new ProgressTracker(store);
            progress.RecordCompletion("a", 30);
            var result = progress.RecordCompletion("a", 12.5);
            Assert.IsTrue(result.IsNewBest);
            Assert.AreEqual(12.5, result.BestTime);
            Assert.IsTrue(store.TryGetBestTime("a", out var stored));
            Assert.AreEqual(12.5, stored);
        }

        [TestCase(0, "0:00.0")]
        [TestCase(0.04, "0:00.0")]
        [TestCase(0.1, "0:00.1")]
        [TestCase(5.96, "0:05.9")]
        [TestCase(65.3, "1:05.3")]
        [TestCase(3725.0, "62:05.0")]
        [TestCase(-3, "0:00.0")]
        public void FormatsMinutesSecondsAndTenths(double seconds, string expected)
        {
            Assert.AreEqual(expected, ProgressTracker.FormatTime(seconds));
        }
    }
}
