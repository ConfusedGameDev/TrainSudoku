using System;
using System.Collections.Generic;
using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    /// <summary>
    /// The decision half of the audio layer (M22): which track should be playing, and the live star tier it rides on.
    /// Playback, crossfades and the mixer are Unity-side and can only be judged by ear.
    /// </summary>
    public class AudioLayerTests
    {
        private static readonly double[] Times = { 120, 240 };

        // ---- StarLadder

        [Test]
        public void LadderStartsAtThree()
        {
            var ladder = new StarLadder();
            Assert.AreEqual(3, ladder.Tier);
            ladder.Begin(Times);
            Assert.AreEqual(3, ladder.Tier);
        }

        [Test]
        public void LadderDescendsPastEachThreshold()
        {
            var ladder = new StarLadder();
            ladder.Begin(Times);

            Assert.IsFalse(ladder.Tick(119), "Still inside the three-star time.");
            Assert.AreEqual(3, ladder.Tier);

            Assert.IsTrue(ladder.Tick(120.5), "Past the three-star time.");
            Assert.AreEqual(2, ladder.Tier);

            Assert.IsFalse(ladder.Tick(200), "Still inside the two-star time.");
            Assert.AreEqual(2, ladder.Tier);

            Assert.IsTrue(ladder.Tick(240.5), "Past the two-star time.");
            Assert.AreEqual(1, ladder.Tier);
        }

        [Test]
        public void LadderReportsEachDescentExactlyOnce()
        {
            var ladder = new StarLadder();
            ladder.Begin(Times);

            var descents = 0;
            for (var t = 0.0; t <= 400; t += 0.5)
                if (ladder.Tick(t)) descents++;

            Assert.AreEqual(2, descents, "Three stars to two to one is two descents, however often it is ticked.");
            Assert.AreEqual(1, ladder.Tier);
        }

        [Test]
        public void LadderNeverClimbs()
        {
            var ladder = new StarLadder();
            ladder.Begin(Times);
            ladder.Tick(300);
            Assert.AreEqual(1, ladder.Tier);

            // A clock that jumps backwards must not put a star back: only Begin raises the tier.
            Assert.IsFalse(ladder.Tick(0));
            Assert.AreEqual(1, ladder.Tier);
        }

        [Test]
        public void BeginResetsTheLadder()
        {
            var ladder = new StarLadder();
            ladder.Begin(Times);
            ladder.Tick(300);
            Assert.AreEqual(1, ladder.Tier);

            ladder.Begin(Times);
            Assert.AreEqual(3, ladder.Tier, "Retry starts the run again at the top.");
        }

        [Test]
        public void BeginSeedsTheTierFromElapsed()
        {
            var ladder = new StarLadder();

            // A continued attempt opens on the right rung rather than opening on three and dropping a frame later.
            ladder.Begin(Times, 400);
            Assert.AreEqual(1, ladder.Tier);

            ladder.Begin(Times, 200);
            Assert.AreEqual(2, ladder.Tier);
        }

        [Test]
        public void UnauthoredThresholdsHoldTheTopTrack()
        {
            // The one place the ladder deliberately disagrees with StarsFor, which returns the floor of 1 here.
            // Opening on the one-star track would tell the player they had already failed before they touched
            // the board.
            foreach (var times in new IReadOnlyList<double>[] { null, new double[] { 120 }, new double[] { 0, 0 } })
            {
                var ladder = new StarLadder();
                ladder.Begin(times);
                Assert.AreEqual(3, ladder.Tier);
                Assert.IsFalse(ladder.Tick(10_000));
                Assert.AreEqual(3, ladder.Tier);
            }
        }

        [Test]
        public void PartiallyAuthoredThresholdsStillTrackStarsFor()
        {
            var onlyThree = new StarLadder();
            onlyThree.Begin(new double[] { 120, 0 });
            Assert.AreEqual(3, onlyThree.Tier);
            onlyThree.Tick(130);
            Assert.AreEqual(1, onlyThree.Tier, "With no two-star time, past three-star drops straight to one.");

            var onlyTwo = new StarLadder();
            onlyTwo.Begin(new double[] { 0, 240 });
            Assert.AreEqual(2, onlyTwo.Tier, "Three stars are unreachable, so the run opens on two.");
        }

        /// <summary>
        /// The load-bearing test. The track the player hears and the number the arrival screen prints are the same
        /// fact, so the ladder may never disagree with the scoring rule on a level that has thresholds.
        /// </summary>
        [Test]
        public void LadderAgreesWithStarsForOnAuthoredLevels()
        {
            var tables = new[]
            {
                new double[] { 120, 240 },
                new double[] { 180, 300 },
                new double[] { 27, 54 },
                new double[] { 216, 432 },
            };

            foreach (var times in tables)
                foreach (var t in Sweep(times))
                    Assert.AreEqual(ProgressTracker.StarsFor(t, times), StarLadder.TierFor(t, times[0], times[1]),
                        $"Disagreed at {t}s against {times[0]}/{times[1]}.");
        }

        private static IEnumerable<double> Sweep(IReadOnlyList<double> times)
        {
            foreach (var edge in new[] { 0.0, times[0], times[1] })
                foreach (var delta in new[] { -1.0, -0.001, 0.0, 0.001, 1.0 })
                {
                    var t = edge + delta;
                    if (t >= 0) yield return t;
                }

            yield return times[1] * 2;
        }

        // ---- MusicPlan

        [Test]
        public void EveryStateHasADefinedTrack()
        {
            foreach (GameState state in Enum.GetValues(typeof(GameState)))
                for (var tier = 1; tier <= 3; tier++)
                    Assert.IsTrue(Enum.IsDefined(typeof(MusicTrack), MusicPlan.For(state, tier)),
                        $"{state} at tier {tier} fell through to an undefined track.");
        }

        [Test]
        public void PauseKeepsThePlayTrack()
        {
            // What makes a pause a Pause/UnPause on the source rather than a crossfade out to silence and back.
            for (var tier = 1; tier <= 3; tier++)
                Assert.AreEqual(MusicPlan.For(GameState.Play, tier), MusicPlan.For(GameState.Pause, tier));
        }

        [Test]
        public void ScreensMapToTheirTracks()
        {
            Assert.AreEqual(MusicTrack.MainMenu, MusicPlan.For(GameState.MainMenu, 3));
            Assert.AreEqual(MusicTrack.Map, MusicPlan.For(GameState.Network, 3));
            Assert.AreEqual(MusicTrack.Map, MusicPlan.For(GameState.LevelSelect, 3),
                "Both maps share a track, so crossing between them does not interrupt it.");

            Assert.AreEqual(MusicTrack.PlayThreeStar, MusicPlan.For(GameState.Play, 3));
            Assert.AreEqual(MusicTrack.PlayTwoStar, MusicPlan.For(GameState.Play, 2));
            Assert.AreEqual(MusicTrack.PlayOneStar, MusicPlan.For(GameState.Play, 1));

            // The win fades the music out; the train and the fanfare carry that beat.
            Assert.AreEqual(MusicTrack.None, MusicPlan.For(GameState.TrainRun, 3));
            Assert.AreEqual(MusicTrack.None, MusicPlan.For(GameState.Win, 3));
        }

        [Test]
        public void TiersOutsideTheRangeClamp()
        {
            Assert.AreEqual(MusicTrack.PlayOneStar, MusicPlan.For(GameState.Play, 0));
            Assert.AreEqual(MusicTrack.PlayOneStar, MusicPlan.For(GameState.Play, -1));
            Assert.AreEqual(MusicTrack.PlayThreeStar, MusicPlan.For(GameState.Play, 4));
        }

        [Test]
        public void NoneIsZeroSoAnEmptyRowIsSilent()
        {
            Assert.AreEqual(0, (int)MusicTrack.None);
        }

        // ---- GameFlow's live tier

        private static GameFlow FlowWith(IReadOnlyList<double> times, out List<int> tiers)
        {
            var flow = new GameFlow(new[] { "one", "two" }, new InMemorySaveStore())
            {
                StarTimesForLevel = _ => times,
                UnlockAll = true,
            };
            var seen = new List<int>();
            flow.StarTierChanged += seen.Add;
            tiers = seen;
            return flow;
        }

        [Test]
        public void FlowReportsTheTierDescending()
        {
            var flow = FlowWith(Times, out var tiers);
            flow.ShowLevelSelect();
            flow.StartLevel(0);
            Assert.AreEqual(3, flow.StarTier);

            flow.BoardTouched();
            for (var i = 0; i < 300; i++) flow.Tick(1.0);

            CollectionAssert.AreEqual(new[] { 2, 1 }, tiers);
            Assert.AreEqual(1, flow.StarTier);
        }

        [Test]
        public void TheClockMustBeRunningForTheTierToMove()
        {
            var flow = FlowWith(Times, out var tiers);
            flow.ShowLevelSelect();
            flow.StartLevel(0);

            // Idle: the level is loaded but the board has not been touched.
            for (var i = 0; i < 300; i++) flow.Tick(1.0);
            CollectionAssert.IsEmpty(tiers);
            Assert.AreEqual(3, flow.StarTier);

            flow.BoardTouched();
            flow.PauseGame();
            for (var i = 0; i < 300; i++) flow.Tick(1.0);
            CollectionAssert.IsEmpty(tiers, "A paused clock does not spend the player's stars.");
        }

        [Test]
        public void RetryPutsTheTierBack()
        {
            var flow = FlowWith(Times, out _);
            flow.ShowLevelSelect();
            flow.StartLevel(0);
            flow.BoardTouched();
            for (var i = 0; i < 300; i++) flow.Tick(1.0);
            Assert.AreEqual(1, flow.StarTier);

            flow.PauseGame();
            flow.Retry();
            Assert.AreEqual(3, flow.StarTier);
        }

        [Test]
        public void ContinuingOpensOnTheRightRung()
        {
            var store = new InMemorySaveStore();
            var flow = new GameFlow(new[] { "one", "two" }, store)
            {
                StarTimesForLevel = _ => Times,
                UnlockAll = true,
            };

            store.SetProgress("one", new LevelProgress(400, Array.Empty<PlacedPiece>()));
            flow.ShowLevelSelect();
            flow.StartLevel(0);

            Assert.AreEqual(1, flow.StarTier, "A continued attempt past both thresholds opens on the one-star track.");
        }

        /// <summary>
        /// Direct regression test on the allocation trap: <c>LevelDefinition.StarTimes</c> builds a fresh array on
        /// every call, so the thresholds must be read once per level start and never per frame.
        /// </summary>
        [Test]
        public void ThresholdsAreReadOncePerLevelStart()
        {
            var reads = 0;
            var flow = new GameFlow(new[] { "one", "two" }, new InMemorySaveStore())
            {
                StarTimesForLevel = _ => { reads++; return Times; },
                UnlockAll = true,
            };

            flow.ShowLevelSelect();
            flow.StartLevel(0);
            flow.BoardTouched();
            var afterStart = reads;

            for (var i = 0; i < 10_000; i++) flow.Tick(0.016);
            Assert.AreEqual(afterStart, reads, "Ticking must never reach a LevelDefinition.");
        }
    }
}
