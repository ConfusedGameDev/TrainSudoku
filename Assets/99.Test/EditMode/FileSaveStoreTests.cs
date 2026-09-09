using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class FileSaveStoreTests
    {
        private string _directory;
        private string _path;
        private List<string> _log;

        [SetUp]
        public void CreateTempDirectory()
        {
            _directory = Path.Combine(Path.GetTempPath(), "TrainSudokuTests", Path.GetRandomFileName());
            _path = Path.Combine(_directory, "nested", "save.json");
            _log = new List<string>();
        }

        [TearDown]
        public void DeleteTempDirectory()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        private FileSaveStore Open() => new FileSaveStore(_path, _log.Add);

        [Test]
        public void StartsEmptyWhenThereIsNoFile()
        {
            var store = Open();
            Assert.AreEqual(0, store.Count);
            Assert.IsFalse(store.TryGetBestTime("a", out _));
            Assert.IsFalse(store.LoadedFromCorruptFile);
            Assert.IsFalse(File.Exists(_path), "nothing is written until there is progress");
        }

        [Test]
        public void PersistsAcrossInstances()
        {
            var store = Open();
            store.SetBestTime("first", 42.5);
            store.SetBestTime("second", 7);
            Assert.IsTrue(File.Exists(_path), "the directory is created and the file written");

            var again = Open();
            Assert.AreEqual(2, again.Count);
            Assert.IsTrue(again.TryGetBestTime("first", out var first));
            Assert.AreEqual(42.5, first);
            Assert.IsTrue(again.TryGetBestTime("second", out var second));
            Assert.AreEqual(7, second);
            Assert.IsEmpty(_log);
        }

        [Test]
        public void OverwritesAndLeavesNoTemporaryFile()
        {
            var store = Open();
            store.SetBestTime("first", 42.5);
            store.SetBestTime("first", 30);
            Assert.IsTrue(Open().TryGetBestTime("first", out var best));
            Assert.AreEqual(30, best);
            Assert.IsFalse(File.Exists(_path + ".tmp"));
        }

        [Test]
        public void WorksWithProgressTrackerAndGameFlow()
        {
            var ids = new[] { "one", "two" };
            var flow = new GameFlow(ids, Open());
            flow.ShowLevelSelect();
            flow.StartLevel(0);
            flow.BoardTouched();
            flow.Tick(9);
            flow.CompleteLevel();

            var restarted = new GameFlow(ids, Open());
            Assert.IsTrue(restarted.IsUnlocked(1), "unlocks survive a restart");
            Assert.IsTrue(restarted.TryGetBestTime(0, out var best));
            Assert.AreEqual(9, best, 1e-9);
        }

        [Test]
        public void InProgressSnapshotsPersistAcrossInstances()
        {
            var store = Open();
            store.SetProgress("half", new LevelProgress(40.25, new[] { new PlacedPiece(1, 0, PieceKey.NE) }));
            Assert.IsTrue(File.Exists(_path));

            var again = Open();
            Assert.IsTrue(again.TryGetProgress("half", out var progress));
            Assert.AreEqual(40.25, progress.Elapsed);
            CollectionAssert.AreEqual(new[] { new PlacedPiece(1, 0, PieceKey.NE) }, progress.Pieces);
            Assert.IsFalse(again.TryGetProgress("other", out _));
            Assert.IsFalse(again.TryGetProgress(null, out _));

            again.ClearProgress("half");
            Assert.IsFalse(Open().TryGetProgress("half", out _));
            Assert.IsEmpty(_log);
        }

        [Test]
        public void OldFilesWithoutSnapshotsStillLoad()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, "{\"version\":1,\"bestTimes\":{\"a\":2}}");
            var store = Open();
            Assert.IsFalse(store.LoadedFromCorruptFile);
            Assert.IsTrue(store.TryGetBestTime("a", out _));
            Assert.IsFalse(store.TryGetProgress("a", out _));
        }

        [Test]
        public void AnUnfinishedLevelIsContinuedAfterARestart()
        {
            var ids = new[] { "one", "two" };
            var flow = new GameFlow(ids, Open());
            flow.ShowLevelSelect();
            flow.StartLevel(0);
            flow.BoardTouched();
            flow.Tick(6);
            var board = TestLevels.Corridor();
            TestLevels.Place(board, 0, 1, PieceKey.EW);
            flow.SaveProgress(board);

            LevelProgress resumed = null;
            var restarted = new GameFlow(ids, Open());
            restarted.LevelStarted += (_, progress) => resumed = progress;
            Assert.IsTrue(restarted.HasInProgress(0));
            restarted.ShowLevelSelect();
            restarted.StartLevel(0);
            Assert.IsNotNull(resumed);
            Assert.AreEqual(6, restarted.Timer.Elapsed, 1e-9);
            CollectionAssert.AreEqual(new[] { new PlacedPiece(0, 1, PieceKey.EW) }, resumed.Pieces);
        }

        [Test]
        public void CorruptFileIsSetAsideAndProgressStartsFresh()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, "{ this is not json");

            var store = Open();
            Assert.IsTrue(store.LoadedFromCorruptFile);
            Assert.AreEqual(0, store.Count);
            Assert.IsFalse(File.Exists(_path));
            Assert.IsTrue(File.Exists(_path + ".corrupt"));
            Assert.IsNotEmpty(_log);

            store.SetBestTime("a", 1);
            Assert.IsTrue(Open().TryGetBestTime("a", out _), "saving works again afterwards");
        }

        [Test]
        public void ClearDeletesTheFile()
        {
            var store = Open();
            store.SetBestTime("a", 1);
            store.Clear();
            Assert.AreEqual(0, store.Count);
            Assert.IsFalse(File.Exists(_path));
            Assert.AreEqual(0, Open().Count);
        }

        [Test]
        public void ReloadPicksUpExternalEdits()
        {
            var store = Open();
            store.SetBestTime("a", 5);
            File.WriteAllText(_path, "{\"version\":1,\"bestTimes\":{\"a\":2,\"b\":3}}");
            store.Reload();
            Assert.IsTrue(store.TryGetBestTime("a", out var a));
            Assert.AreEqual(2, a);
            Assert.IsTrue(store.TryGetBestTime("b", out _));
        }
    }
}
