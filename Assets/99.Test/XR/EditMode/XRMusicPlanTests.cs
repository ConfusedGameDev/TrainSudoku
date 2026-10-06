using System;
using NUnit.Framework;
using TrainSudoku.Core;
using TrainSudoku.XR.Rules;

namespace TrainSudoku.XR.Tests
{
    /// <summary>XR-PRD 9: which loop each flow state plays, and when the line's jingle starts and fades.</summary>
    public class XRMusicPlanTests
    {
        [Test]
        public void TheMapsPlayTheConcourseLoop()
        {
            Assert.AreEqual(XRMusicLoop.Concourse, XRMusicPlan.Loop(GameState.MainMenu));
            Assert.AreEqual(XRMusicLoop.Concourse, XRMusicPlan.Loop(GameState.Network));
            Assert.AreEqual(XRMusicLoop.Concourse, XRMusicPlan.Loop(GameState.LevelSelect));
        }

        [Test]
        public void ThePauseKeepsPlaysLoopAndTurnsItDown()
        {
            Assert.AreEqual(XRMusicLoop.Platform, XRMusicPlan.Loop(GameState.Play));
            Assert.AreEqual(XRMusicPlan.Loop(GameState.Play), XRMusicPlan.Loop(GameState.Pause));
            Assert.IsTrue(XRMusicPlan.Ducked(GameState.Pause));
            Assert.IsFalse(XRMusicPlan.Ducked(GameState.Play));
        }

        [Test]
        public void TheTrainRunAndTheArrivalHaveNoLoop()
        {
            Assert.AreEqual(XRMusicLoop.None, XRMusicPlan.Loop(GameState.TrainRun));
            Assert.AreEqual(XRMusicLoop.None, XRMusicPlan.Loop(GameState.Win));
        }

        [Test]
        public void TheJinglePlaysOnlyOverTheTrainRun()
        {
            foreach (GameState state in Enum.GetValues(typeof(GameState)))
            {
                Assert.AreEqual(state == GameState.TrainRun, XRMusicPlan.StartsJingle(state), state.ToString());
                Assert.AreEqual(state != GameState.TrainRun, XRMusicPlan.FadesJingle(state), state.ToString());
            }
        }

        [Test]
        public void EveryLineHasAJingleAndLaterLinesReuseThemInOrder()
        {
            Assert.AreEqual(0, XRMusicPlan.Jingle(0));
            Assert.AreEqual(XRMusicPlan.JingleCount - 1, XRMusicPlan.Jingle(XRMusicPlan.JingleCount - 1));
            Assert.AreEqual(0, XRMusicPlan.Jingle(XRMusicPlan.JingleCount));
            Assert.AreEqual(-1, XRMusicPlan.Jingle(-1));
        }
    }
}
