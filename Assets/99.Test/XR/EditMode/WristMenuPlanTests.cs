using System;
using NUnit.Framework;
using TrainSudoku.Core;
using TrainSudoku.XR.Rules;

namespace TrainSudoku.XR.Tests
{
    /// <summary>XR-PRD 6.2 and 6.4: what the wrist menu offers in each state, and what its button does.</summary>
    public class WristMenuPlanTests
    {
        [Test]
        public void TheNetworkOffersSettings()
        {
            CollectionAssert.AreEqual(new[] { WristItem.Settings, WristItem.Close }, WristMenuPlan.Items(GameState.Network));
        }

        [Test]
        public void TheLineMapOffersTheWayBackToTheNetwork()
        {
            CollectionAssert.AreEqual(new[] { WristItem.BackToNetwork, WristItem.Settings, WristItem.Close }, WristMenuPlan.Items(GameState.LevelSelect));
        }

        [Test]
        public void PlayAndPauseOfferThePauseMenu()
        {
            var pause = new[] { WristItem.Resume, WristItem.Retry, WristItem.BackToMap, WristItem.Settings };
            CollectionAssert.AreEqual(pause, WristMenuPlan.Items(GameState.Play));
            CollectionAssert.AreEqual(pause, WristMenuPlan.Items(GameState.Pause));
        }

        [TestCase(GameState.TrainRun)]
        [TestCase(GameState.Win)]
        [TestCase(GameState.MainMenu)]
        public void NoMenuWhileTheTrainRunsOrAtTheArrival(GameState state)
        {
            Assert.IsFalse(WristMenuPlan.Offered(state));
            Assert.IsEmpty(WristMenuPlan.Items(state));
        }

        [Test]
        public void OpeningItInPlayPauses()
        {
            Assert.AreEqual(WristToggle.OpenAndPause, WristMenuPlan.Toggle(GameState.Play, false));
        }

        [TestCase(GameState.Network)]
        [TestCase(GameState.LevelSelect)]
        [TestCase(GameState.Pause)]
        public void OpeningItElsewhereOnlyOpens(GameState state)
        {
            Assert.AreEqual(WristToggle.Open, WristMenuPlan.Toggle(state, false));
        }

        [Test]
        public void ClosingItWhilePausedKeepsThePause()
        {
            Assert.AreEqual(WristToggle.Close, WristMenuPlan.Toggle(GameState.Pause, true));
        }

        [TestCase(GameState.Network)]
        [TestCase(GameState.LevelSelect)]
        public void ClosingItOnAMapOnlyCloses(GameState state)
        {
            Assert.AreEqual(WristToggle.Close, WristMenuPlan.Toggle(state, true));
        }

        [Test]
        public void AStateWithNoMenuClosesAnOpenOne()
        {
            Assert.AreEqual(WristToggle.Close, WristMenuPlan.Toggle(GameState.TrainRun, true));
            Assert.AreEqual(WristToggle.Nothing, WristMenuPlan.Toggle(GameState.TrainRun, false));
        }

        [Test]
        public void OnlyPlayPausesOnAFocusLoss()
        {
            foreach (GameState state in Enum.GetValues(typeof(GameState)))
                Assert.AreEqual(state == GameState.Play, WristMenuPlan.PausesOnFocusLoss(state), state.ToString());
        }
    }
}
