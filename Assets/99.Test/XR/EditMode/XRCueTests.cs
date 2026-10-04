using System;
using NUnit.Framework;
using TrainSudoku.Core;
using TrainSudoku.XR.Rules;

namespace TrainSudoku.XR.Tests
{
    /// <summary>XR's cue vocabulary (XR-PRD 9): its append-only numbering, and which cue every grab and release plays.</summary>
    public class XRCueTests
    {
        /// <summary>The library asset stores slots by number: these must never move. Append new cues at the end.</summary>
        [Test]
        public void CueNumbersAreAppendOnly()
        {
            var expected = new[]
            {
                "None", "Grab", "Place", "Move", "Replace", "Lift", "Error", "Steered", "Fixed", "ReturnLand", "Puff", "Burst",
                "Whistle", "LineCleared", "FinalPiece", "TrainStart", "TrainLoop", "TrainHurry", "UiPress", "UiBack", "WristOpen",
                "MapSelect", "MapClosed", "BoardPlaced", "Star", "Stamp", "FanfareOne", "FanfareTwo", "FanfareThree", "TutorialNote",
            };

            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], ((XRCue)i).ToString(), $"cue {i}");
        }

        [Test]
        public void EveryLandingPlaysItsOwnCue()
        {
            var drop = new PieceDrop(XRTestBoards.Corridor());
            Assert.AreEqual(XRCue.Grab, XRCueMap.For(drop.GrabFromTray(Hand.Right, PieceKey.EW)));
            Assert.AreEqual(XRCue.Place, XRCueMap.For(drop.Release(Hand.Right, (1, 1), false)));
            Assert.AreEqual(XRCue.Lift, XRCueMap.For(drop.GrabFromCell(Hand.Right, 1, 1)));
            // Onto the exit's cell, then replaced there by another piece that also meets the exit.
            Assert.AreEqual(XRCue.Move, XRCueMap.For(drop.Release(Hand.Right, (2, 1), false)));
            drop.GrabFromTray(Hand.Right, PieceKey.SE);
            Assert.AreEqual(XRCue.Replace, XRCueMap.For(drop.Release(Hand.Right, (2, 1), false)));
        }

        [Test]
        public void AnIllegalDropPlaysTheErrorCueButASteeredOneDoesNot()
        {
            var drop = new PieceDrop(XRTestBoards.Corridor());
            drop.GrabFromTray(Hand.Right, PieceKey.NS);
            Assert.AreEqual(XRCue.Error, XRCueMap.For(drop.Release(Hand.Right, (0, 1), false)));

            drop.Gate = (x, y, key) => false;
            drop.GrabFromTray(Hand.Right, PieceKey.EW);
            Assert.AreEqual(XRCue.Steered, XRCueMap.For(drop.Release(Hand.Right, (1, 1), false)));
        }

        [Test]
        public void AFixedPieceRefusesWithItsNoteAndOtherRefusalsAreSilent()
        {
            var board = XRTestBoards.PlanExample();
            var drop = new PieceDrop(board);
            Assert.AreEqual(XRCue.Fixed, XRCueMap.For(drop.GrabFromCell(Hand.Left, 1, 2)));
            Assert.AreEqual(XRCue.None, XRCueMap.For(drop.GrabFromCell(Hand.Left, 0, 0)), "an empty cell");
            Assert.AreEqual(XRCue.None, XRCueMap.For(drop.Release(Hand.Left, (0, 0), false)), "nothing held");
        }

        [Test]
        public void LettingGoOffThePlatformIsHeardWhenThePieceComesToRest()
        {
            var drop = new PieceDrop(XRTestBoards.Corridor());
            drop.GrabFromTray(Hand.Right, PieceKey.EW);
            Assert.AreEqual(XRCue.None, XRCueMap.For(drop.Release(Hand.Right, null, false)));
            drop.GrabFromTray(Hand.Right, PieceKey.EW);
            Assert.AreEqual(XRCue.None, XRCueMap.For(drop.Release(Hand.Right, (1, 1), true)));
        }

        [Test]
        public void EveryOutcomeHasAnAnswer()
        {
            // No outcome may throw or fall through: a new DropOutcome must be given a cue (or a deliberate None) here.
            foreach (DropOutcome outcome in Enum.GetValues(typeof(DropOutcome)))
                Assert.That(Enum.IsDefined(typeof(DropOutcome), outcome));
            Assert.AreEqual(9, Enum.GetValues(typeof(DropOutcome)).Length, "a new outcome needs a line in XRCueMap.For");
        }

        [Test]
        public void TheFanfareFollowsTheStars()
        {
            Assert.AreEqual(XRCue.FanfareOne, XRCueMap.Fanfare(1));
            Assert.AreEqual(XRCue.FanfareTwo, XRCueMap.Fanfare(2));
            Assert.AreEqual(XRCue.FanfareThree, XRCueMap.Fanfare(3));
        }

        [Test]
        public void OnlyWhatAHandDidIsFelt()
        {
            Assert.IsTrue(XRCueMap.IsFelt(XRCue.Place));
            Assert.IsTrue(XRCueMap.IsFelt(XRCue.UiPress));
            Assert.IsFalse(XRCueMap.IsFelt(XRCue.TrainLoop));
            Assert.IsFalse(XRCueMap.IsFelt(XRCue.FanfareThree));
        }
    }
}
