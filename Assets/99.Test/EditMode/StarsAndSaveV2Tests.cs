using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    /// <summary>
    /// The version 2 save format (M13): stars are stored, a version 1 file still loads, and a bad star entry can
    /// never cost the player a best time.
    /// </summary>
    public class StarsAndSaveV2Tests
    {
        private static readonly double[] Thresholds = { 30d, 60d };   // <=30s is 3 stars, <=60s is 2

        // ---- awarding ----

        [Test]
        public void StarsFollowTheThresholds()
        {
            Assert.AreEqual(3, ProgressTracker.StarsFor(29.9, Thresholds));
            Assert.AreEqual(3, ProgressTracker.StarsFor(30d, Thresholds), "the threshold itself earns the star");
            Assert.AreEqual(2, ProgressTracker.StarsFor(30.1, Thresholds));
            Assert.AreEqual(2, ProgressTracker.StarsFor(60d, Thresholds));
            Assert.AreEqual(1, ProgressTracker.StarsFor(60.1, Thresholds));
        }

        [Test]
        public void FinishingIsAlwaysWorthAtLeastOneStar()
        {
            Assert.AreEqual(1, ProgressTracker.StarsFor(9999d, Thresholds));
            Assert.AreEqual(1, ProgressTracker.StarsFor(1d, null), "an unauthored level costs the player nothing");
            Assert.AreEqual(1, ProgressTracker.StarsFor(1d, new[] { 0d, 0d }), "zero means unauthored, not unbeatable");
        }

        [Test]
        public void ASlowerRunKeepsTheStarsAlreadyEarned()
        {
            var tracker = new ProgressTracker(new InMemorySaveStore());

            var fast = tracker.RecordCompletion("a", 10d, Thresholds);
            Assert.AreEqual(3, fast.Stars);
            Assert.IsTrue(fast.IsNewBestStars);

            var slow = tracker.RecordCompletion("a", 90d, Thresholds);
            Assert.AreEqual(1, slow.Stars, "the run itself was worth one");
            Assert.IsFalse(slow.IsNewBestStars);
            Assert.IsFalse(slow.IsNewBest);
            Assert.AreEqual(3, tracker.GetStars("a"), "but the level keeps its three");
            Assert.AreEqual(10d, slow.BestTime);
        }

        [Test]
        public void StarsOnALineAddUp()
        {
            var tracker = new ProgressTracker(new InMemorySaveStore());
            tracker.RecordCompletion("a", 10d, Thresholds);   // 3
            tracker.RecordCompletion("b", 45d, Thresholds);   // 2
            Assert.AreEqual(5, tracker.StarsOnLine(new[] { "a", "b", "never-played" }));
        }

        // ---- line unlocking, always derived ----

        [Test]
        public void ALineOpensOnlyWhenEveryStationBeforeItIsCleared()
        {
            var tracker = new ProgressTracker(new InMemorySaveStore());
            var previous = new[] { "a", "b" };

            Assert.IsTrue(tracker.IsLineUnlocked(null, 1), "the first line has nothing before it");
            Assert.IsFalse(tracker.IsLineUnlocked(previous, 1));

            tracker.RecordCompletion("a", 90d, Thresholds);
            Assert.IsFalse(tracker.IsLineUnlocked(previous, 1), "one station short");

            tracker.RecordCompletion("b", 90d, Thresholds);
            Assert.IsTrue(tracker.IsLineUnlocked(previous, 1), "one star each is enough (D20)");
            Assert.IsFalse(tracker.IsLineUnlocked(previous, 2), "and tightening the rule needs no migration");
        }

        // ---- the v1 -> v2 migration ----

        [Test]
        public void AVersionOneFileLoadsAndItsStarsAreAwardedFromTheBestTimes()
        {
            const string v1 = "{\"version\":1,\"bestTimes\":{\"fast\":12.5,\"slow\":300},\"inProgress\":{}}";
            Assert.IsTrue(SaveJson.TryRead(v1, out var data));
            Assert.AreEqual(1, data.Version, "the file says what it is");
            Assert.AreEqual(2, data.BestTimes.Count);
            Assert.AreEqual(0, data.Stars.Count, "a v1 file carries no stars");

            var store = new InMemorySaveStore();
            store.SetBestTime("fast", data.BestTimes["fast"]);
            store.SetBestTime("slow", data.BestTimes["slow"]);
            var tracker = new ProgressTracker(store);

            Assert.IsTrue(tracker.AwardMissingStars("fast", Thresholds));
            Assert.IsTrue(tracker.AwardMissingStars("slow", Thresholds));
            Assert.AreEqual(3, tracker.GetStars("fast"));
            Assert.AreEqual(1, tracker.GetStars("slow"));

            Assert.IsFalse(tracker.AwardMissingStars("fast", Thresholds), "running it again changes nothing");
            Assert.IsFalse(tracker.AwardMissingStars("never-played", Thresholds), "no best time, no award");
        }

        [Test]
        public void MigrationNeverOverwritesAStarAlreadyEarned()
        {
            var store = new InMemorySaveStore();
            store.SetBestTime("a", 300d);
            store.SetStars("a", 3);
            var tracker = new ProgressTracker(store);

            Assert.IsFalse(tracker.AwardMissingStars("a", Thresholds));
            Assert.AreEqual(3, tracker.GetStars("a"), "a slow best time cannot demote an earned rating");
        }

        [Test]
        public void AVersionTwoFileRoundTrips()
        {
            var data = new SaveData();
            data.BestTimes["a"] = 12.5;
            data.Stars["a"] = 3;
            data.Stars["b"] = 1;

            var json = SaveJson.Write(data);
            StringAssert.Contains("\"version\": 2", json);
            Assert.IsTrue(SaveJson.TryRead(json, out var back), json);
            Assert.AreEqual(2, back.Stars.Count);
            Assert.AreEqual(3, back.Stars["a"]);
            Assert.AreEqual(1, back.Stars["b"]);
            Assert.AreEqual(12.5, back.BestTimes["a"]);
        }

        [Test]
        public void ReadingAVersionOneFileAndWritingItBackProducesVersionTwo()
        {
            const string v1 = "{\"version\":1,\"bestTimes\":{\"a\":12.5},\"inProgress\":{}}";
            Assert.IsTrue(SaveJson.TryRead(v1, out var data));
            data.Stars["a"] = 3;

            var json = SaveJson.Write(data);
            StringAssert.Contains("\"version\": 2", json);
            Assert.IsTrue(SaveJson.TryRead(json, out var back));
            Assert.AreEqual(3, back.Stars["a"]);
            Assert.AreEqual(12.5, back.BestTimes["a"]);
        }

        // ---- a bad star entry is skipped, never fatal ----

        [TestCase("\"a\": \"three\"", TestName = "star value is a string")]
        [TestCase("\"a\": 4", TestName = "star value is out of range")]
        [TestCase("\"a\": 0", TestName = "star value is zero")]
        [TestCase("\"a\": 2.5", TestName = "star value is fractional")]
        [TestCase("\"a\": null", TestName = "star value is null")]
        public void AMalformedStarEntryIsSkippedAndTheBestTimesSurvive(string badEntry)
        {
            var json = "{\"version\":2,\"bestTimes\":{\"a\":12.5},\"stars\":{" + badEntry + ",\"b\":2},\"inProgress\":{}}";

            Assert.IsTrue(SaveJson.TryRead(json, out var data), "a bad star entry must never fail the file");
            Assert.AreEqual(12.5, data.BestTimes["a"], "the best time is what must survive");
            Assert.IsFalse(data.Stars.ContainsKey("a"), "the bad entry is dropped");
            Assert.AreEqual(2, data.Stars["b"], "its neighbours are kept");
        }

        [Test]
        public void AStarsBlockOfTheWrongShapeIsIgnoredEntirely()
        {
            const string json = "{\"version\":2,\"bestTimes\":{\"a\":12.5},\"stars\":[1,2,3],\"inProgress\":{}}";
            Assert.IsTrue(SaveJson.TryRead(json, out var data));
            Assert.AreEqual(12.5, data.BestTimes["a"]);
            Assert.AreEqual(0, data.Stars.Count);
        }

        // ---- the store contract ----

        [Test]
        public void TheInMemoryStoreRemembersStars()
        {
            ISaveStore store = new InMemorySaveStore();
            Assert.IsFalse(store.TryGetStars("a", out _));
            store.SetStars("a", 2);
            Assert.IsTrue(store.TryGetStars("a", out var stars));
            Assert.AreEqual(2, stars);
        }
    }
}
