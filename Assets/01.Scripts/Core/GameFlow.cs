using System;
using System.Collections.Generic;

namespace TrainSudoku.Core
{
    public enum GameState
    {
        MainMenu,
        LevelSelect,
        Play,
        Pause,
        TrainRun,
        Win,
    }

    /// <summary>
    /// The screen flow of PRD section 5 as a plain state machine, together with the timer and progress rules it drives.
    /// The Unity layer shows one panel per state and forwards button presses to these methods. Invalid transitions throw,
    /// because the UI only offers buttons that are valid in the current state.
    /// </summary>
    public sealed class GameFlow
    {
        private readonly IReadOnlyList<string> _levelIds;

        public GameState State { get; private set; } = GameState.MainMenu;
        public PlayTimer Timer { get; } = new PlayTimer();
        public ProgressTracker Progress { get; }

        /// <summary>Index into the level list, or -1 before any level has been started.</summary>
        public int CurrentLevelIndex { get; private set; } = -1;

        public int LevelCount => _levelIds.Count;
        public string CurrentLevelId => CurrentLevelIndex >= 0 ? _levelIds[CurrentLevelIndex] : null;
        public bool HasNextLevel => CurrentLevelIndex >= 0 && CurrentLevelIndex + 1 < LevelCount;

        /// <summary>Editor-only testing aid (PRD section 5): every level counts as unlocked.</summary>
        public bool UnlockAll { get; set; }

        /// <summary>Outcome of the most recent completed run; null until a level has been won.</summary>
        public CompletionResult? LastResult { get; private set; }

        /// <summary>Raised after every state change with (previous, current).</summary>
        public event Action<GameState, GameState> StateChanged;

        /// <summary>Raised when a level should be (re)loaded onto the board: start, retry and next.</summary>
        public event Action<int> LevelStarted;

        public GameFlow(IReadOnlyList<string> levelIds, ISaveStore store)
        {
            _levelIds = levelIds ?? throw new ArgumentNullException(nameof(levelIds));
            Progress = new ProgressTracker(store);
        }

        public bool IsUnlocked(int index) => UnlockAll ? index >= 0 && index < LevelCount : Progress.IsUnlocked(_levelIds, index);

        public bool TryGetBestTime(int index, out double seconds)
        {
            seconds = 0;
            return index >= 0 && index < LevelCount && Progress.TryGetBestTime(_levelIds[index], out seconds);
        }

        public void Tick(double deltaSeconds) => Timer.Tick(deltaSeconds);

        // ---- Main menu

        public void ShowLevelSelect()
        {
            Require(GameState.MainMenu, GameState.Pause, GameState.Win);
            Transition(GameState.LevelSelect);
        }

        public void ShowMainMenu()
        {
            Require(GameState.LevelSelect);
            Transition(GameState.MainMenu);
        }

        // ---- Level select

        public void StartLevel(int index)
        {
            Require(GameState.LevelSelect);
            if (index < 0 || index >= LevelCount)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"There are {LevelCount} levels.");
            if (!IsUnlocked(index)) throw new InvalidOperationException($"Level {index} is locked.");
            Begin(index);
        }

        // ---- Play

        /// <summary>The player's first interaction with the board starts the clock.</summary>
        public void BoardTouched()
        {
            Require(GameState.Play);
            Timer.Start();
        }

        public void PauseGame()
        {
            Require(GameState.Play);
            Timer.Pause();
            Transition(GameState.Pause);
        }

        /// <summary>The board reports a win: the clock stops, the result is recorded and the train run begins.</summary>
        public void CompleteLevel()
        {
            Require(GameState.Play);
            Timer.Stop();
            LastResult = Progress.RecordCompletion(CurrentLevelId, Timer.Elapsed);
            Transition(GameState.TrainRun);
        }

        // ---- Pause

        public void ResumeGame()
        {
            Require(GameState.Pause);
            Timer.Resume();
            Transition(GameState.Play);
        }

        /// <summary>Clears the board and the clock and plays the same level again.</summary>
        public void Retry()
        {
            Require(GameState.Pause, GameState.Win);
            Begin(CurrentLevelIndex);
        }

        // ---- Train run

        /// <summary>Called when the animation ends or the player taps to skip it.</summary>
        public void FinishTrainRun()
        {
            Require(GameState.TrainRun);
            Transition(GameState.Win);
        }

        // ---- Win

        public void NextLevel()
        {
            Require(GameState.Win);
            if (!HasNextLevel) throw new InvalidOperationException("This is the last level.");
            Begin(CurrentLevelIndex + 1);
        }

        // ---- internals

        private void Begin(int index)
        {
            CurrentLevelIndex = index;
            Timer.Reset();
            Transition(GameState.Play);
            LevelStarted?.Invoke(index);
        }

        private void Transition(GameState next)
        {
            var previous = State;
            State = next;
            StateChanged?.Invoke(previous, next);
        }

        private void Require(params GameState[] allowed)
        {
            if (Array.IndexOf(allowed, State) >= 0) return;
            throw new InvalidOperationException($"Not allowed in state {State}; expected {string.Join(" or ", allowed)}.");
        }
    }
}
