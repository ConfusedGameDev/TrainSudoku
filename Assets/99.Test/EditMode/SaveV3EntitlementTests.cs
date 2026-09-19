using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    /// <summary>
    /// The version 3 save format: <c>purchasedFullVersion</c>, the flag that lets a future freemium build recognise
    /// someone who bought the paid one.
    /// </summary>
    /// <remarks>
    /// Every test here is really one assertion wearing different clothes: <b>a save file can never lose an
    /// entitlement it once implied</b>. Apple does not allow taking content from a player who paid for it, and by the
    /// time that matters the paid build will be long gone — so these are the tests that have to still be green years
    /// from now, when the code they cover looks pointless.
    /// </remarks>
    public class SaveV3EntitlementTests
    {
        [Test]
        public void ANewSaveOwnsEverything()
        {
            Assert.IsTrue(new SaveData().PurchasedFullVersion, "the shipping build is paid, so its saves are owners");
            Assert.IsTrue(new InMemorySaveStore().PurchasedFullVersion);
            Assert.IsTrue(new ProgressTracker(new InMemorySaveStore()).PurchasedFullVersion);
        }

        [Test]
        public void AVersionOneFileIsTreatedAsAPaidPlayer()
        {
            // The whole point of the flag: this file was written before it existed, by a build that could only have
            // been bought. Absence is evidence of purchase, not of its lack.
            const string v1 = "{\"version\":1,\"bestTimes\":{\"a\":12.5},\"inProgress\":{}}";

            Assert.IsTrue(SaveJson.TryRead(v1, out var data));
            Assert.AreEqual(1, data.Version);
            Assert.IsTrue(data.PurchasedFullVersion, "an early adopter keeps all 216 stations");
        }

        [Test]
        public void AVersionTwoFileIsTreatedAsAPaidPlayer()
        {
            const string v2 = "{\"version\":2,\"bestTimes\":{\"a\":12.5},\"stars\":{\"a\":3},\"inProgress\":{}}";

            Assert.IsTrue(SaveJson.TryRead(v2, out var data));
            Assert.IsTrue(data.PurchasedFullVersion);
        }

        [Test]
        public void TheFlagRoundTrips()
        {
            var owner = new SaveData();
            owner.BestTimes["a"] = 12.5;
            var ownerJson = SaveJson.Write(owner);
            StringAssert.Contains("\"purchasedFullVersion\": true", ownerJson);
            Assert.IsTrue(SaveJson.TryRead(ownerJson, out var ownerBack), ownerJson);
            Assert.IsTrue(ownerBack.PurchasedFullVersion);

            // The state no build writes yet, but the format has to carry it or the freemium build cannot start.
            var freePlayer = new SaveData { PurchasedFullVersion = false };
            var freeJson = SaveJson.Write(freePlayer);
            StringAssert.Contains("\"purchasedFullVersion\": false", freeJson);
            Assert.IsTrue(SaveJson.TryRead(freeJson, out var freeBack), freeJson);
            Assert.IsFalse(freeBack.PurchasedFullVersion, "and false must survive the round trip, or it is not a flag");
        }

        [TestCase("\"purchasedFullVersion\": \"yes\"", TestName = "value is a string")]
        [TestCase("\"purchasedFullVersion\": 1", TestName = "value is a number")]
        [TestCase("\"purchasedFullVersion\": null", TestName = "value is null")]
        [TestCase("\"purchasedFullVersion\": {}", TestName = "value is an object")]
        public void AMalformedFlagFailsOpen(string badEntry)
        {
            var json = "{\"version\":3," + badEntry + ",\"bestTimes\":{\"a\":12.5},\"stars\":{},\"inProgress\":{}}";

            Assert.IsTrue(SaveJson.TryRead(json, out var data), "a bad flag must never fail the file");
            Assert.IsTrue(data.PurchasedFullVersion,
                "an unreadable entitlement resolves in the player's favour: granting wrongly is generous, " +
                "revoking wrongly is unrecoverable");
            Assert.AreEqual(12.5, data.BestTimes["a"], "and the best times are untouched either way");
        }

        [Test]
        public void TheFlagSurvivesAMigratingWrite()
        {
            const string v1 = "{\"version\":1,\"bestTimes\":{\"a\":12.5},\"inProgress\":{}}";
            Assert.IsTrue(SaveJson.TryRead(v1, out var data));

            var json = SaveJson.Write(data);
            StringAssert.Contains("\"version\": 3", json);
            Assert.IsTrue(SaveJson.TryRead(json, out var back));
            Assert.IsTrue(back.PurchasedFullVersion, "reading a v1 file and saving it writes the entitlement down");
        }

        [Test]
        public void TheStoreRemembersTheFlag()
        {
            ISaveStore store = new InMemorySaveStore();
            Assert.IsTrue(store.PurchasedFullVersion);

            store.PurchasedFullVersion = false;
            Assert.IsFalse(store.PurchasedFullVersion);
        }
    }
}
