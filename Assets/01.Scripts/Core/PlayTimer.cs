namespace TrainSudoku.Core
{
    public enum TimerState
    {
        /// <summary>Level loaded, the player has not touched the board yet.</summary>
        Idle,
        Running,
        Paused,
        /// <summary>Level won; the elapsed time is final.</summary>
        Stopped,
    }

    /// <summary>Per-level clock (PRD section 6). Starts on the first board tap, pauses with the pause menu, stops on the win.</summary>
    public sealed class PlayTimer
    {
        public double Elapsed { get; private set; }
        public TimerState State { get; private set; } = TimerState.Idle;
        public bool IsRunning => State == TimerState.Running;

        /// <summary>First tap on the board. Ignored unless the timer is idle.</summary>
        public void Start()
        {
            if (State == TimerState.Idle) State = TimerState.Running;
        }

        public void Pause()
        {
            if (State == TimerState.Running) State = TimerState.Paused;
        }

        public void Resume()
        {
            if (State == TimerState.Paused) State = TimerState.Running;
        }

        /// <summary>Freezes the elapsed time. Ignored once stopped.</summary>
        public void Stop()
        {
            if (State != TimerState.Stopped) State = TimerState.Stopped;
        }

        public void Reset()
        {
            Elapsed = 0;
            State = TimerState.Idle;
        }

        public void Tick(double deltaSeconds)
        {
            if (IsRunning && deltaSeconds > 0) Elapsed += deltaSeconds;
        }
    }
}
