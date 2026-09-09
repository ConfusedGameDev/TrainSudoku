using System;
using System.Collections.Generic;
using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class GameFlowTests
    {
        private static readonly string[] Ids = { "one", "two", "three" };

        private InMemorySaveStore _store;
        private GameFlow _flow;
        private List<(GameState From, GameState To)> _changes;
        private List<int> _started;

        [SetUp]
        public void CreateFlow()
        {
            _store = new InMemorySaveStore();
            _flow = new GameFlow(Ids, _store);
            _changes = new List<(GameState, GameState)>();
            _started = new List<int>();
            _flow.StateChanged += (from, to) => _changes.Add((from, to));
            _flow.LevelStarted += _started.Add;
        }

        private void PlayLevel(int index)
        {
            _flow.ShowLevelSelect();
            _flow.StartLevel(index);
        }

        [Test]
        public void StartsInTheMainMenuWithNoLevel()
        {
            Assert.AreEqual(GameState.MainMenu, _flow.State);
            Assert.AreEqual(-1, _flow.CurrentLevelIndex);
            Assert.IsNull(_flow.CurrentLevelId);
            Assert.IsFalse(_flow.HasNextLevel);
            Assert.IsNull(_flow.LastResult);
            Assert.AreEqual(3, _flow.LevelCount);
        }

        [Test]
        public void MenuAndLevelSelectNavigateBothWays()
        {
            _flow.ShowLevelSelect();
            Assert.AreEqual(GameState.LevelSelect, _flow.State);
            _flow.ShowMainMenu();
            Assert.AreEqual(GameState.MainMenu, _flow.State);
            CollectionAssert.AreEqual(
                new[] { (GameState.MainMenu, GameState.LevelSelect), (GameState.LevelSelect, GameState.MainMenu) },
                _changes);
        }

        [Test]
        public void StartingALevelEntersPlayWithAFreshTimer()
        {
            PlayLevel(0);
            Assert.AreEqual(GameState.Play, _flow.State);
            Assert.AreEqual(0, _flow.CurrentLevelIndex);
            Assert.AreEqual("one", _flow.CurrentLevelId);
            Assert.IsTrue(_flow.HasNextLevel);
            Assert.AreEqual(TimerState.Idle, _flow.Timer.State);
            CollectionAssert.AreEqual(new[] { 0 }, _started);
        }

        [Test]
        public void LockedLevelsCannotBeStarted()
        {
            _flow.ShowLevelSelect();
            Assert.IsFalse(_flow.IsUnlocked(1));
            Assert.Throws<InvalidOperationException>(() => _flow.StartLevel(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _flow.StartLevel(3));
            Assert.AreEqual(GameState.LevelSelect, _flow.State);
        }

        [Test]
        public void UnlockAllOpensEveryLevel()
        {
            _flow.UnlockAll = true;
            Assert.IsTrue(_flow.IsUnlocked(2));
            Assert.IsFalse(_flow.IsUnlocked(3));
            PlayLevel(2);
            Assert.AreEqual(2, _flow.CurrentLevelIndex);
            Assert.IsFalse(_flow.HasNextLevel);
        }

        [Test]
        public void TimerStartsOnTheFirstBoardTouchAndPausesWithTheMenu()
        {
            PlayLevel(0);
            _flow.Tick(5);
            Assert.AreEqual(0, _flow.Timer.Elapsed, "The clock waits for the first tap");

            _flow.BoardTouched();
            _flow.Tick(2);
            _flow.PauseGame();
            Assert.AreEqual(GameState.Pause, _flow.State);
            _flow.Tick(10);
            Assert.AreEqual(2, _flow.Timer.Elapsed, 1e-9);

            _flow.ResumeGame();
            Assert.AreEqual(GameState.Play, _flow.State);
            _flow.Tick(1);
            Assert.AreEqual(3, _flow.Timer.Elapsed, 1e-9);
        }

        [Test]
        public void CompletingALevelStopsTheClockRecordsTheBestAndRunsTheTrain()
        {
            PlayLevel(0);
            _flow.BoardTouched();
            _flow.Tick(12.5);
            _flow.CompleteLevel();

            Assert.AreEqual(GameState.TrainRun, _flow.State);
            Assert.AreEqual(TimerState.Stopped, _flow.Timer.State);
            _flow.Tick(100);
            Assert.AreEqual(12.5, _flow.Timer.Elapsed, 1e-9);

            var result = _flow.LastResult.Value;
            Assert.AreEqual(12.5, result.Time, 1e-9);
            Assert.AreEqual(12.5, result.BestTime, 1e-9);
            Assert.IsTrue(result.IsNewBest);
            Assert.IsTrue(_store.TryGetBestTime("one", out var stored));
            Assert.AreEqual(12.5, stored, 1e-9);
            Assert.IsTrue(_flow.IsUnlocked(1), "Finishing level one unlocks level two");

            _flow.FinishTrainRun();
            Assert.AreEqual(GameState.Win, _flow.State);
        }

        [Test]
        public void NextLevelFromTheWinScreenStartsTheFollowingLevel()
        {
            PlayLevel(0);
            _flow.CompleteLevel();
            _flow.FinishTrainRun();
            _flow.NextLevel();

            Assert.AreEqual(GameState.Play, _flow.State);
            Assert.AreEqual(1, _flow.CurrentLevelIndex);
            Assert.AreEqual(TimerState.Idle, _flow.Timer.State);
            Assert.AreEqual(0, _flow.Timer.Elapsed);
            CollectionAssert.AreEqual(new[] { 0, 1 }, _started);
        }

        [Test]
        public void NextIsRefusedOnTheLastLevel()
        {
            _flow.UnlockAll = true;
            PlayLevel(2);
            _flow.CompleteLevel();
            _flow.FinishTrainRun();
            Assert.IsFalse(_flow.HasNextLevel);
            Assert.Throws<InvalidOperationException>(() => _flow.NextLevel());
            Assert.AreEqual(GameState.Win, _flow.State);
        }

        [Test]
        public void RetryFromPauseResetsTheClockAndReloadsTheLevel()
        {
            PlayLevel(0);
            _flow.BoardTouched();
            _flow.Tick(3);
            _flow.PauseGame();
            _flow.Retry();

            Assert.AreEqual(GameState.Play, _flow.State);
            Assert.AreEqual(0, _flow.CurrentLevelIndex);
            Assert.AreEqual(TimerState.Idle, _flow.Timer.State);
            Assert.AreEqual(0, _flow.Timer.Elapsed);
            CollectionAssert.AreEqual(new[] { 0, 0 }, _started);
        }

        [Test]
        public void RetryFromWinKeepsTheRecordedBest()
        {
            PlayLevel(0);
            _flow.BoardTouched();
            _flow.Tick(20);
            _flow.CompleteLevel();
            _flow.FinishTrainRun();
            _flow.Retry();
            _flow.BoardTouched();
            _flow.Tick(30);
            _flow.CompleteLevel();

            var result = _flow.LastResult.Value;
            Assert.AreEqual(30, result.Time, 1e-9);
            Assert.AreEqual(20, result.BestTime, 1e-9);
            Assert.IsFalse(result.IsNewBest);
        }

        [Test]
        public void ExitFromPauseAndMenuFromWinReturnToLevelSelect()
        {
            PlayLevel(0);
            _flow.PauseGame();
            _flow.ShowLevelSelect();
            Assert.AreEqual(GameState.LevelSelect, _flow.State);

            _flow.StartLevel(0);
            _flow.CompleteLevel();
            _flow.FinishTrainRun();
            _flow.ShowLevelSelect();
            Assert.AreEqual(GameState.LevelSelect, _flow.State);
        }

        [Test]
        public void BestTimesAreReadableByLevelIndex()
        {
            Assert.IsFalse(_flow.TryGetBestTime(0, out _));
            PlayLevel(0);
            _flow.BoardTouched();
            _flow.Tick(9);
            _flow.CompleteLevel();
            Assert.IsTrue(_flow.TryGetBestTime(0, out var best));
            Assert.AreEqual(9, best, 1e-9);
            Assert.IsFalse(_flow.TryGetBestTime(1, out _));
            Assert.IsFalse(_flow.TryGetBestTime(-1, out _));
        }

        [Test]
        public void TransitionsOutsideTheDiagramThrowAndLeaveTheStateAlone()
        {
            Assert.Throws<InvalidOperationException>(() => _flow.PauseGame());
            Assert.Throws<InvalidOperationException>(() => _flow.ResumeGame());
            Assert.Throws<InvalidOperationException>(() => _flow.CompleteLevel());
            Assert.Throws<InvalidOperationException>(() => _flow.FinishTrainRun());
            Assert.Throws<InvalidOperationException>(() => _flow.Retry());
            Assert.Throws<InvalidOperationException>(() => _flow.NextLevel());
            Assert.Throws<InvalidOperationException>(() => _flow.BoardTouched());
            Assert.Throws<InvalidOperationException>(() => _flow.ShowMainMenu());
            Assert.Throws<InvalidOperationException>(() => _flow.StartLevel(0));
            Assert.AreEqual(GameState.MainMenu, _flow.State);
            Assert.IsEmpty(_changes);
        }
    }
}
