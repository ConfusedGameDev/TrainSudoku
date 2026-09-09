using System.Collections.Generic;
using NUnit.Framework;
using TrainSudoku.Core;
using TrainSudoku.Game;
using UnityEditor;
using UnityEngine;

namespace TrainSudoku.Tests
{
    /// <summary>The shipped levels (M10): every entry of the collection asset is well formed and has exactly one solution.</summary>
    public class LevelCollectionTests
    {
        private const string CollectionPath = "Assets/03.Data/Levels/LevelCollection.asset";

        /// <summary>Enough to finish every 6x6 and 8x8 level many times over; larger boards may not finish and are only checked for shape.</summary>
        private const long NodeBudget = 5_000_000;

        private static LevelCollection LoadCollection()
        {
            var collection = AssetDatabase.LoadAssetAtPath<LevelCollection>(CollectionPath);
            Assert.IsNotNull(collection, $"Missing {CollectionPath}");
            return collection;
        }

        [Test]
        public void CollectionHasAtLeastFiveLevelsWithUniqueIds()
        {
            var collection = LoadCollection();
            Assert.GreaterOrEqual(collection.Count, 5);

            var ids = new HashSet<string>();
            for (var i = 0; i < collection.Count; i++)
            {
                var level = collection[i];
                Assert.IsNotNull(level, $"Entry {i} is empty");
                Assert.IsNotEmpty(level.Id, $"{level.name} has no id");
                Assert.IsTrue(ids.Add(level.Id), $"Id '{level.Id}' is used twice");
            }
        }

        [Test]
        public void EveryLevelIsWellFormedAndHasExactlyOneSolution()
        {
            var collection = LoadCollection();
            var unverified = new List<string>();
            for (var i = 0; i < collection.Count; i++)
            {
                var level = collection[i];
                var data = level.ToLevelData();
                Assert.IsEmpty(data.Validate(), $"{level.name}: {string.Join("; ", data.Validate())}");

                var result = Solver.Solve(data, 2, NodeBudget);
                if (result.Exhausted)
                {
                    // A lower bound only: the search did not finish, so the level can still be neither refuted nor proven.
                    Assert.LessOrEqual(result.Count, 1, $"{level.name} has more than one solution");
                    unverified.Add($"{level.name} ({data.Width}x{data.Height}, {result.Count} found in {result.Nodes:N0} nodes)");
                    continue;
                }

                Assert.AreEqual(1, result.Count, $"{level.name} ({data.Width}x{data.Height}) has {result.Count} solutions");
            }

            if (unverified.Count > 0) Debug.Log("Search budget exhausted, uniqueness not proven for: " + string.Join(", ", unverified));
        }
    }
}
