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
            Assert.AreEqual("{\n  \"version\": 1,\n  \"bestTimes\": {\n    \"a\": 1.25,\n    \"b\": 2\n  }\n}\n", json);
        }

        [Test]
        public void EmptyDataWritesAnEmptyObject()
        {
            var json = SaveJson.Write(new SaveData());
            Assert.AreEqual("{\n  \"version\": 1,\n  \"bestTimes\": {}\n}\n", json);
            Assert.IsTrue(SaveJson.TryRead(json, out var back));
            Assert.IsEmpty(back.BestTimes);
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
        public void RejectsInvalidOrMisshapenInput(string json)
        {
            Assert.IsFalse(SaveJson.TryRead(json, out var data));
            Assert.IsNull(data);
        }
    }
}
