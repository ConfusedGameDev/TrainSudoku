using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class PlayTimerTests
    {
        [Test]
        public void DoesNotCountBeforeTheFirstTap()
        {
            var timer = new PlayTimer();
            timer.Tick(5);
            Assert.AreEqual(TimerState.Idle, timer.State);
            Assert.AreEqual(0, timer.Elapsed);

            timer.Start();
            timer.Tick(1.5);
            Assert.AreEqual(TimerState.Running, timer.State);
            Assert.AreEqual(1.5, timer.Elapsed, 1e-9);
        }

        [Test]
        public void PauseAndResumeOnlyAffectARunningTimer()
        {
            var timer = new PlayTimer();
            timer.Pause();
            Assert.AreEqual(TimerState.Idle, timer.State, "Pausing before the first tap changes nothing");
            timer.Resume();
            Assert.AreEqual(TimerState.Idle, timer.State);

            timer.Start();
            timer.Tick(1);
            timer.Pause();
            timer.Tick(10);
            Assert.AreEqual(TimerState.Paused, timer.State);
            Assert.AreEqual(1, timer.Elapsed, 1e-9);

            timer.Resume();
            timer.Tick(2);
            Assert.AreEqual(3, timer.Elapsed, 1e-9);
        }

        [Test]
        public void StopFreezesTheTimeAndStartCannotRestartIt()
        {
            var timer = new PlayTimer();
            timer.Start();
            timer.Tick(4);
            timer.Stop();
            timer.Tick(4);
            timer.Start();
            timer.Resume();
            timer.Tick(4);
            Assert.AreEqual(TimerState.Stopped, timer.State);
            Assert.AreEqual(4, timer.Elapsed, 1e-9);
        }

        [Test]
        public void StopWorksFromIdleAndPaused()
        {
            var idle = new PlayTimer();
            idle.Stop();
            Assert.AreEqual(TimerState.Stopped, idle.State);
            Assert.AreEqual(0, idle.Elapsed);

            var paused = new PlayTimer();
            paused.Start();
            paused.Tick(2);
            paused.Pause();
            paused.Stop();
            Assert.AreEqual(TimerState.Stopped, paused.State);
            Assert.AreEqual(2, paused.Elapsed, 1e-9);
        }

        [Test]
        public void ResetReturnsToIdleWithZero()
        {
            var timer = new PlayTimer();
            timer.Start();
            timer.Tick(7);
            timer.Stop();
            timer.Reset();
            Assert.AreEqual(TimerState.Idle, timer.State);
            Assert.AreEqual(0, timer.Elapsed);
            timer.Start();
            timer.Tick(1);
            Assert.AreEqual(1, timer.Elapsed, 1e-9);
        }

        [Test]
        public void NegativeDeltasAreIgnored()
        {
            var timer = new PlayTimer();
            timer.Start();
            timer.Tick(2);
            timer.Tick(-5);
            Assert.AreEqual(2, timer.Elapsed, 1e-9);
        }
    }
}
