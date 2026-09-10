using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class SaveJsonTests
    {
        [Test]
        public void RoundTripsBestTimes()
        {
            var data = new SaveData();
            data.BestTimes["first"] = 12.5;
            data.BestTimes["plan-example"] = 0.1;
            data.BestTimes["weird \"id\" \\ with \n newline"] = 3;

            var json = SaveJson.Write(data);
            Assert.IsTrue(SaveJson.TryRead(json, out var back), json);
            Assert.AreEqual(SaveData.CurrentVersion, back.Version);
            Assert.AreEqual(3, back.BestTimes.Count);
            Assert.AreEqual(12.5, back.BestTimes["first"]);
            Assert.AreEqual(0.1, back.BestTimes["plan-example"]);
            Assert.AreEqual(3, back.BestTimes["weird \"id\" \\ with \n newline"]);
        }

        [Test]
        public void WritesTheDocumentedShapeWithSortedIds()
        {
            var data = new SaveData();
            data.BestTimes["b"] = 2;
            data.BestTimes["a"] = 1.25;
            var json = SaveJson.Write(data);
            Assert.AreEqual("{\n  \"version\": 2,\n  \"bestTimes\": {\n    \"a\": 1.25,\n    \"b\": 2\n  },\n  \"stars\": {},\n  \"inProgress\": {}\n}\n", json);
        }

        [Test]
        public void WritesInProgressSnapshotsInTheDocumentedShape()
        {
            var data = new SaveData();
            data.InProgress["b"] = new LevelProgress(3, null);
            data.InProgress["a"] = new LevelProgress(40.25, new[] { new PlacedPiece(1, 0, PieceKey.NE), new PlacedPiece(2, 5, PieceKey.SW) });
            var json = SaveJson.Write(data);
            Assert.AreEqual(
                "{\n  \"version\": 2,\n  \"bestTimes\": {},\n  \"stars\": {},\n  \"inProgress\": {\n" +
                "    \"a\": {\n      \"elapsed\": 40.25,\n      \"pieces\": [\n" +
                "        { \"x\": 1, \"y\": 0, \"key\": \"NE\" },\n" +
                "        { \"x\": 2, \"y\": 5, \"key\": \"SW\" }\n      ]\n    },\n" +
                "    \"b\": {\n      \"elapsed\": 3,\n      \"pieces\": []\n    }\n  }\n}\n",
                json);
        }

        [Test]
        public void RoundTripsInProgressSnapshots()
        {
            var data = new SaveData();
            data.BestTimes["done"] = 9;
            data.InProgress["half"] = new LevelProgress(40.25, new[] { new PlacedPiece(1, 0, PieceKey.NE), new PlacedPiece(2, 5, PieceKey.SW) });

            Assert.IsTrue(SaveJson.TryRead(SaveJson.Write(data), out var back));
            Assert.AreEqual(9, back.BestTimes["done"]);
            var progress = back.InProgress["half"];
            Assert.AreEqual(40.25, progress.Elapsed);
            CollectionAssert.AreEqual(data.InProgress["half"].Pieces, progress.Pieces);
        }

        [TestCase("{\"inProgress\": {\"a\": {\"elapsed\": -1, \"pieces\": []}}}")]
        [TestCase("{\"inProgress\": {\"a\": {\"elapsed\": \"soon\", \"pieces\": []}}}")]
        [TestCase("{\"inProgress\": {\"a\": {\"pieces\": []}}}")]
        [TestCase("{\"inProgress\": {\"a\": {\"elapsed\": 1, \"pieces\": {}}}}")]
        [TestCase("{\"inProgress\": {\"a\": {\"elapsed\": 1, \"pieces\": [{\"x\": 1, \"y\": 0, \"key\": \"XX\"}]}}}")]
        [TestCase("{\"inProgress\": {\"a\": {\"elapsed\": 1, \"pieces\": [{\"x\": 1.5, \"y\": 0, \"key\": \"NE\"}]}}}")]
        [TestCase("{\"inProgress\": {\"a\": {\"elapsed\": 1, \"pieces\": [{\"y\": 0, \"key\": \"NE\"}]}}}")]
        [TestCase("{\"inProgress\": {\"a\": {\"elapsed\": 1, \"pieces\": [7]}}}")]
        [TestCase("{\"inProgress\": {\"a\": 5}}")]
        public void MalformedSnapshotsAreSkippedWithoutFailingTheFile(string json)
        {
            var withBest = json.Replace("{\"inProgress\"", "{\"bestTimes\": {\"done\": 2}, \"inProgress\"");
            Assert.IsTrue(SaveJson.TryRead(withBest, out var data), withBest);
            Assert.AreEqual(2, data.BestTimes["done"], "best times survive a bad snapshot");
            Assert.IsEmpty(data.InProgress);
        }

        [Test]
        public void SnapshotWithoutPiecesIsAnEmptyBoard()
        {
            Assert.IsTrue(SaveJson.TryRead("{\"inProgress\": {\"a\": {\"elapsed\": 2}}}", out var data));
            Assert.AreEqual(2, data.InProgress["a"].Elapsed);
            Assert.IsEmpty(data.InProgress["a"].Pieces);
        }

        [Test]
        public void EmptyDataWritesAnEmptyObject()
        {
            var json = SaveJson.Write(new SaveData());
            Assert.AreEqual("{\n  \"version\": 2,\n  \"bestTimes\": {},\n  \"stars\": {},\n  \"inProgress\": {}\n}\n", json);
            Assert.IsTrue(SaveJson.TryRead(json, out var back));
            Assert.IsEmpty(back.BestTimes);
            Assert.IsEmpty(back.InProgress);
        }

        [Test]
        public void ReadsHandEditedAndForeignJson()
        {
            const string json = "  {\"bestTimes\" : { \"x\" : 1e1 , \"y\":0 }, \"version\":2, \"extra\": [1, true, null, {\"k\": \"v\\u0041\"}] } ";
            Assert.IsTrue(SaveJson.TryRead(json, out var data));
            Assert.AreEqual(2, data.Version);
            Assert.AreEqual(10, data.BestTimes["x"]);
            Assert.AreEqual(0, data.BestTimes["y"]);
        }

        [Test]
        public void MissingBestTimesMeansNoProgress()
        {
            Assert.IsTrue(SaveJson.TryRead("{}", out var data));
            Assert.IsEmpty(data.BestTimes);
            Assert.IsEmpty(data.InProgress);
            Assert.AreEqual(SaveData.CurrentVersion, data.Version);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not json")]
        [TestCase("{\"bestTimes\": 5}")]
        [TestCase("{\"bestTimes\": {\"a\": \"fast\"}}")]
        [TestCase("{\"bestTimes\": {\"a\": -1}}")]
        [TestCase("{\"bestTimes\": {\"a\": 1}")]
        [TestCase("{\"bestTimes\": {\"a\": 1}} trailing")]
        [TestCase("{\"bestTimes\": {\"a\": 1,}}")]
        [TestCase("[1, 2]")]
        [TestCase("{\"inProgress\": []}")]
        [TestCase("{\"inProgress\": 3}")]
        public void RejectsInvalidOrMisshapenInput(string json)
        {
            Assert.IsFalse(SaveJson.TryRead(json, out var data));
            Assert.IsNull(data);
        }
    }
}
