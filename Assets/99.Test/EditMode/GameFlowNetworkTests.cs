using System;
using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    /// <summary>
    /// The network state and line-aware flow added in M13. Two lines of two stations each, so a terminus, a locked
    /// line and a line that opens are all reachable.
    /// </summary>
    public class GameFlowNetworkTests
    {
        private static readonly string[] Ids = { "a1", "a2", "b1", "b2" };
        private static readonly double[] Thresholds = { 30d, 60d };

        private InMemorySaveStore _store;
        private GameFlow _flow;

        [SetUp]
        public void CreateFlow()
        {
            _store = new InMemorySaveStore();
            _flow = new GameFlow(Ids, _store, new NetworkLayout(new[] { 2, 2 }));
            _flow.StarTimesForLevel = _ => Thresholds;
        }

        /// <summary>Menu to a level on a given line, the way the screens do it.</summary>
        private void PlayStation(int line, int station)
        {
            _flow.UnlockAll = true;
            if (_flow.State == GameState.MainMenu) _flow.ShowNetwork();
            _flow.ShowLineMap(line);
            _flow.StartLevel(_flow.Layout.FlatIndex(line, station));
        }

        // ---- transitions ----

        [Test]
        public void TheNetworkIsReachableFromTheMenuTheLineMapAndAWin()
        {
            _flow.ShowNetwork();
            Assert.AreEqual(GameState.Network, _flow.State);

            _flow.ShowMainMenu();
            Assert.AreEqual(GameState.MainMenu, _flow.State, "and back again");

            _flow.ShowLevelSelect();
            _flow.ShowNetwork();
            Assert.AreEqual(GameState.Network, _flow.State);
        }

        [Test]
        public void OpeningALineGoesToItsMapAndRemembersWhichLine()
        {
            _flow.ShowNetwork();
            _flow.ShowLineMap(0);
            Assert.AreEqual(GameState.LevelSelect, _flow.State);
            Assert.AreEqual(0, _flow.SelectedLineIndex);
        }

        [Test]
        public void IllegalTransitionsStillThrow()
        {
            // ShowLineMap is only reachable from the network.
            Assert.Throws<InvalidOperationException>(() => _flow.ShowLineMap(0));

            _flow.ShowNetwork();
            Assert.Throws<ArgumentOutOfRangeException>(() => _flow.ShowLineMap(9));
            Assert.Throws<InvalidOperationException>(() => _flow.ShowLineMap(1), "line 1 is locked");

            // Network is not reachable from Play.
            PlayStation(0, 0);
            Assert.Throws<InvalidOperationException>(() => _flow.ShowNetwork());
        }

        // ---- line awareness ----

        [Test]
        public void TheCurrentLevelKnowsItsLineAndStation()
        {
            PlayStation(1, 1);
            Assert.AreEqual(3, _flow.CurrentLevelIndex, "the flat index is still the identity");
            Assert.AreEqual("b2", _flow.CurrentLevelId);
            Assert.AreEqual(1, _flow.CurrentLineIndex);
            Assert.AreEqual(1, _flow.CurrentStationIndex);
        }

        [Test]
        public void NextGoesAlongTheLineThenBackToTheNetwork()
        {
            PlayStation(0, 0);
            Assert.IsTrue(_flow.HasNextStation);
            Assert.IsFalse(_flow.IsLineComplete);

            _flow.CompleteLevel();
            _flow.FinishTrainRun();
            _flow.NextLevel();
            Assert.AreEqual(GameState.Play, _flow.State);
            Assert.AreEqual(1, _flow.CurrentLevelIndex, "the next station on the same line");

            // a2 is line 0's terminus.
            Assert.IsFalse(_flow.HasNextStation);
            Assert.IsTrue(_flow.IsLineComplete);
            _flow.CompleteLevel();
            _flow.FinishTrainRun();
            _flow.NextLevel();
            Assert.AreEqual(GameState.Network, _flow.State, "the line is done, so the run returns to the map");
        }

        // ---- unlocking, always derived from stars ----

        [Test]
        public void TheFirstLineIsAlwaysOpenAndTheSecondNeedsTheFirstCleared()
        {
            // Deliberately no UnlockAll: that editor aid reports every line open, which is what it is for.
            Assert.IsTrue(_flow.IsLineUnlocked(0));
            Assert.IsFalse(_flow.IsLineUnlocked(1));
            Assert.IsFalse(_flow.IsLineUnlocked(2), "there is no line 2");

            _flow.ShowNetwork();
            _flow.ShowLineMap(0);
            _flow.StartLevel(0);
            _flow.CompleteLevel();
            Assert.IsFalse(_flow.IsLineUnlocked(1), "one station short");

            _flow.FinishTrainRun();
            _flow.NextLevel();
            _flow.CompleteLevel();
            Assert.IsTrue(_flow.IsLineUnlocked(1), "one star on every station opens the next line (D20)");
        }

        [Test]
        public void UnlockAllOpensEveryLine()
        {
            Assert.IsFalse(_flow.IsLineUnlocked(1));
            _flow.UnlockAll = true;
            Assert.IsTrue(_flow.IsLineUnlocked(1), "the editor testing aid covers lines as well as stations");
            Assert.IsFalse(_flow.IsLineUnlocked(2), "but it cannot invent a line that does not exist");
        }

        /// <summary>
        /// The per-line testing aid behind the Game inspector's "Debug: line unlocks", which is how the network map
        /// is looked at at a later stage of progress without playing every board up to it.
        /// </summary>
        [Test]
        public void TheLineUnlockOverrideOpensJustTheLinesItNames()
        {
            Assert.IsFalse(_flow.IsLineUnlocked(1));
            _flow.LineUnlockOverride = line => line == 1;
            Assert.IsTrue(_flow.IsLineUnlocked(1));
            Assert.IsFalse(_flow.IsLineUnlocked(2), "it cannot invent a line that does not exist");
        }

        /// <summary>
        /// It may only ever open a line. A line the player has genuinely earned stays open however the override
        /// answers, so the aid cannot fake a regression the real game has no way to produce.
        /// </summary>
        [Test]
        public void TheLineUnlockOverrideCannotCloseAnEarnedLine()
        {
            // Star every station of line 0, the way the screens do it.
            _flow.ShowNetwork();
            _flow.ShowLineMap(0);
            _flow.StartLevel(0);
            _flow.CompleteLevel();
            _flow.FinishTrainRun();
            _flow.NextLevel();
            _flow.CompleteLevel();
            Assert.IsTrue(_flow.IsLineUnlocked(1), "earned by starring every station of line 0");

            _flow.LineUnlockOverride = _ => false;
            Assert.IsTrue(_flow.IsLineUnlocked(1), "the override adds unlocks; it must not take one away");
        }

        [Test]
        public void CompletingALevelAwardsStarsFromItsThresholds()
        {
            PlayStation(0, 0);
            _flow.BoardTouched();
            _flow.Tick(10d);
            _flow.CompleteLevel();

            Assert.IsNotNull(_flow.LastResult);
            Assert.AreEqual(3, _flow.LastResult.Value.Stars);
            Assert.IsTrue(_flow.LastResult.Value.IsNewBestStars);
            Assert.AreEqual(3, _flow.Progress.GetStars("a1"));
            Assert.AreEqual(3, _flow.StarsOnLine(0));
        }

        [Test]
        public void StarsAreOneWhenNoThresholdsAreSupplied()
        {
            _flow.StarTimesForLevel = null;
            PlayStation(0, 0);
            _flow.BoardTouched();
            _flow.Tick(10d);
            _flow.CompleteLevel();
            Assert.AreEqual(1, _flow.LastResult.Value.Stars);
        }

        [Test]
        public void MissingStarsAreAwardedForLevelsThatOnlyHaveATime()
        {
            // Exactly the shape a version 1 save leaves behind.
            _store.SetBestTime("a1", 10d);
            _store.SetBestTime("a2", 300d);

            Assert.AreEqual(2, _flow.AwardMissingStars());
            Assert.AreEqual(3, _flow.Progress.GetStars("a1"));
            Assert.AreEqual(1, _flow.Progress.GetStars("a2"));
            Assert.AreEqual(0, _flow.AwardMissingStars(), "and it is idempotent");
        }

        [Test]
        public void WithNoLayoutEveryLevelIsOneLine()
        {
            var flow = new GameFlow(Ids, new InMemorySaveStore());
            Assert.AreEqual(1, flow.LineCount);
            Assert.AreEqual(4, flow.Layout.StationCount(0));
            Assert.IsTrue(flow.IsLineUnlocked(0));
        }
    }
}
