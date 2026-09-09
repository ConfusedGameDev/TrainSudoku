using NUnit.Framework;
using TrainSudoku.Core;
using TrainSudoku.Game;
using UnityEngine;

namespace TrainSudoku.Tests
{
    public class LevelDefinitionTests
    {
        private LevelDefinition _asset;

        [SetUp]
        public void CreateAsset() => _asset = ScriptableObject.CreateInstance<LevelDefinition>();

        [TearDown]
        public void DestroyAsset() => Object.DestroyImmediate(_asset);

        [Test]
        public void RoundTripsThroughTheAsset()
        {
            var original = TestLevels.PlanExample();
            _asset.SetFrom(original, "plan-example");

            Assert.AreEqual("plan-example", _asset.Id);
            Assert.AreEqual("Plan Example", _asset.DisplayName);
            Assert.AreEqual(6, _asset.Width);
            Assert.AreEqual(6, _asset.Height);
            TestLevels.AssertEqual(original, _asset.ToLevelData());
        }

        [Test]
        public void SetFromKeepsTheIdUnlessGivenANewOne()
        {
            _asset.SetFrom(TestLevels.PlanExample(), "keep-me");
            _asset.SetFrom(LevelText.Parse(TestLevels.LoopText));
            Assert.AreEqual("keep-me", _asset.Id);
            Assert.AreEqual(3, _asset.Width);
        }

        [Test]
        public void ToLevelDataIsIndependentOfTheAsset()
        {
            _asset.SetFrom(TestLevels.PlanExample(), "plan-example");
            var first = _asset.ToLevelData();
            first.ColumnClues[0] = 99;
            first.FixedPieces.Clear();
            TestLevels.AssertEqual(TestLevels.PlanExample(), _asset.ToLevelData());
        }

        [Test]
        public void FreshAssetConvertsToAValidEmptyLevel()
        {
            var level = _asset.ToLevelData();
            Assert.AreEqual(6, level.Width);
            Assert.AreEqual(6, level.Height);
            Assert.IsEmpty(level.FixedPieces);
            Assert.AreEqual(new Tunnel(Direction.West, 0), level.Entrance);
            Assert.AreEqual(new Tunnel(Direction.East, 0), level.Exit);
            Assert.IsEmpty(level.Validate());
        }

        [Test]
        public void CollectionAddsEachLevelOnce()
        {
            var collection = ScriptableObject.CreateInstance<LevelCollection>();
            try
            {
                Assert.IsTrue(collection.Add(_asset));
                Assert.IsFalse(collection.Add(_asset));
                Assert.IsFalse(collection.Add(null));
                Assert.AreEqual(1, collection.Count);
                Assert.AreSame(_asset, collection[0]);
                Assert.IsTrue(collection.Contains(_asset));
                Assert.AreEqual(0, collection.IndexOf(_asset));
                Assert.IsTrue(collection.Remove(_asset));
                Assert.AreEqual(0, collection.Count);
            }
            finally
            {
                Object.DestroyImmediate(collection);
            }
        }
    }
}
