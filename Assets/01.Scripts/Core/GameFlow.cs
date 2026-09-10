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

        /// <summary>The map of every line with content. Sits between the menu and a single line's map.</summary>
        Network,
    }

    /// <summary>
    /// The screen flow of PRD section 5 as a plain state machine, together with the timer and progress rules it drives.
    /// The Unity layer shows one panel per state and forwards button presses to these methods. Invalid transitions throw,
    /// because the UI only offers buttons that are valid in the current state.
    /// </summary>
    public sealed class GameFlow
    {
        private readonly IReadOnlyList<string> _levelIds;
        private readonly NetworkLayout _layout;

        /// <summary>Stars required on every station of a line before the next one opens (D20).</summary>
        public const int MinStarsToOpenNextLine = 1;

        public GameState State { get; private set; } = GameState.MainMenu;
        public PlayTimer Timer { get; } = new PlayTimer();
        public ProgressTracker Progress { get; }

        /// <summary>
        /// Index into the flat level list, or -1 before any level has been started. This is still the save-file
        /// identity; the line and station below are a view over it, never a replacement.
        /// </summary>
        public int CurrentLevelIndex { get; private set; } = -1;

        public int LevelCount => _levelIds.Count;
        public string CurrentLevelId => CurrentLevelIndex >= 0 ? _levelIds[CurrentLevelIndex] : null;

        public NetworkLayout Layout => _layout;
        public int LineCount => _layout.LineCount;

        /// <summary>The line the current level sits on, or -1.</summary>
        public int CurrentLineIndex => _layout.LineOf(CurrentLevelIndex);

        /// <summary>The current level's position along its own line, or -1.</summary>
        public int CurrentStationIndex => _layout.StationOf(CurrentLevelIndex);

        /// <summary>The line whose map was last opened. Set by <see cref="ShowLineMap"/>, and by starting a level.</summary>
        public int SelectedLineIndex { get; private set; }

        /// <summary>True when another station follows on the *same* line.</summary>
        public bool HasNextStation => CurrentLevelIndex >= 0 && !_layout.IsTerminus(CurrentLevelIndex);

        /// <summary>True when the current level is its line's terminus, so finishing it completes the line.</summary>
        public bool IsLineComplete => CurrentLevelIndex >= 0 && _layout.IsTerminus(CurrentLevelIndex);

        /// <summary>
        /// Kept as "is there a next level anywhere in the flat list". Screens should ask <see cref="HasNextStation"/>
        /// instead; this remains because unlocking still walks the flat list.
        /// </summary>
        public bool HasNextLevel => CurrentLevelIndex >= 0 && CurrentLevelIndex + 1 < LevelCount;

        /// <summary>Editor-only testing aid (PRD section 5): every level counts as unlocked.</summary>
        public bool UnlockAll { get; set; }

        /// <summary>
        /// Supplies a level's star thresholds by flat index — <c>[0]</c> earns 3 stars, <c>[1]</c> earns 2. The Unity
        /// layer sets this from the level assets, because Core cannot see a ScriptableObject. Left unset, every
        /// completion is worth the floor of one star, which is what an unauthored level should be worth.
        /// </summary>
        public Func<int, IReadOnlyList<double>> StarTimesForLevel { get; set; }

        /// <summary>Outcome of the most recent completed run; null until a level has been won.</summary>
        public CompletionResult? LastResult { get; private set; }

        /// <summary>Raised after every state change with (previous, current).</summary>
        public event Action<GameState, GameState> StateChanged;

        /// <summary>
        /// Raised when a level should be (re)loaded onto the board: start, retry and next. The snapshot is the saved
        /// progress to continue from, or null to show only the fixed pieces.
        /// </summary>
        public event Action<int, LevelProgress> LevelStarted;

        /// <param name="layout">
        /// How the flat list divides into lines. Null means one line holding every level, which is the shape the game
        /// had before the network existed, so existing callers and tests need no change.
        /// </param>
        public GameFlow(IReadOnlyList<string> levelIds, ISaveStore store, NetworkLayout layout = null)
        {
            _levelIds = levelIds ?? throw new ArgumentNullException(nameof(levelIds));
            _layout = layout ?? NetworkLayout.Single(_levelIds.Count);
            Progress = new ProgressTracker(store);
        }

        /// <summary>
        /// Whether a line is open. The first line always is; any other needs every station on the line before it to
        /// carry at least <see cref="MinStarsToOpenNextLine"/>. Derived from the stars on every call, never stored.
        /// </summary>
        public bool IsLineUnlocked(int lineIndex)
        {
            if (lineIndex < 0 || lineIndex >= _layout.LineCount) return false;
            if (UnlockAll || lineIndex == 0) return true;
            return Progress.IsLineUnlocked(_layout.LineIds(_levelIds, lineIndex - 1), MinStarsToOpenNextLine);
        }

        /// <summary>Stars collected on one line, for the network map's progress read-out.</summary>
        public int StarsOnLine(int lineIndex) => Progress.StarsOnLine(_layout.LineIds(_levelIds, lineIndex));

        public bool IsUnlocked(int index) => UnlockAll ? index >= 0 && index < LevelCount : Progress.IsUnlocked(_levelIds, index);

        public bool TryGetBestTime(int index, out double seconds)
        {
            seconds = 0;
            return index >= 0 && index < LevelCount && Progress.TryGetBestTime(_levelIds[index], out seconds);
        }

        /// <summary>True when the level was left unfinished and selecting it will continue that attempt.</summary>
        public bool HasInProgress(int index) => index >= 0 && index < LevelCount && Progress.TryGetInProgress(_levelIds[index], out _);

        public void Tick(double deltaSeconds) => Timer.Tick(deltaSeconds);

        // ---- Main menu

        public void ShowLevelSelect()
        {
            Require(GameState.MainMenu, GameState.Pause, GameState.Win);
            Transition(GameState.LevelSelect);
        }

        public void ShowMainMenu()
        {
            Require(GameState.LevelSelect, GameState.Network);
            Transition(GameState.MainMenu);
        }

        /// <summary>Opens the network map.</summary>
        public void ShowNetwork()
        {
            Require(GameState.MainMenu, GameState.LevelSelect, GameState.Win);
            Transition(GameState.Network);
        }

        // ---- Network

        /// <summary>Opens one line's map. Only reachable from the network, and only for a line that is open.</summary>
        public void ShowLineMap(int lineIndex)
        {
            Require(GameState.Network);
            if (lineIndex < 0 || lineIndex >= _layout.LineCount)
                throw new ArgumentOutOfRangeException(nameof(lineIndex), lineIndex, $"There are {_layout.LineCount} lines.");
            if (!IsLineUnlocked(lineIndex)) throw new InvalidOperationException($"Line {lineIndex} is locked.");
            SelectedLineIndex = lineIndex;
            Transition(GameState.LevelSelect);
        }

        // ---- Level select

        public void StartLevel(int index)
        {
            Require(GameState.LevelSelect);
            if (index < 0 || index >= LevelCount)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"There are {LevelCount} levels.");
            if (!IsUnlocked(index)) throw new InvalidOperationException($"Level {index} is locked.");
            Begin(index, true);
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

        /// <summary>
        /// Remembers the current attempt (pieces and clock) so the level can be continued later. The Unity layer calls
        /// this after every board change, on pause and when the app is about to quit.
        /// </summary>
        public void SaveProgress(Board board)
        {
            Require(GameState.Play, GameState.Pause);
            if (board == null) throw new ArgumentNullException(nameof(board));
            Progress.SaveInProgress(CurrentLevelId, LevelProgress.Capture(board, Timer.Elapsed));
        }

        /// <summary>The board reports a win: the clock stops, the result is recorded and the train run begins.</summary>
        public void CompleteLevel()
        {
            Require(GameState.Play);
            Timer.Stop();
            LastResult = Progress.RecordCompletion(CurrentLevelId, Timer.Elapsed, StarTimes(CurrentLevelIndex));
            Progress.ClearInProgress(CurrentLevelId);
            Transition(GameState.TrainRun);
        }

        // ---- Pause

        public void ResumeGame()
        {
            Require(GameState.Pause);
            Timer.Resume();
            Transition(GameState.Play);
        }

        /// <summary>Clears the board, the clock and any saved progress, and plays the same level again.</summary>
        public void Retry()
        {
            Require(GameState.Pause, GameState.Win);
            Progress.ClearInProgress(CurrentLevelId);
            Begin(CurrentLevelIndex, false);
        }

        // ---- Train run

        /// <summary>Called when the animation ends or the player taps to skip it.</summary>
        public void FinishTrainRun()
        {
            Require(GameState.TrainRun);
            Transition(GameState.Win);
        }

        // ---- Win

        /// <summary>
        /// Continues along the line. At a terminus there is no next station, so the run goes back to the network map
        /// instead — that is where a newly opened line is shown opening.
        /// </summary>
        public void NextLevel()
        {
            Require(GameState.Win);
            if (IsLineComplete)
            {
                Transition(GameState.Network);
                return;
            }

            if (!HasNextLevel) throw new InvalidOperationException("This is the last level.");
            Begin(CurrentLevelIndex + 1, true);
        }

        // ---- internals

        /// <summary>
        /// The version 1 to version 2 save migration. A v1 file recorded times but no stars, so any level with a best
        /// time and no rating has one worked out from its thresholds. Safe to call on every launch: a level that
        /// already carries a rating is left alone, so this never overwrites something the player earned.
        /// Returns how many ratings were filled in.
        /// </summary>
        public int AwardMissingStars()
        {
            var awarded = 0;
            for (var i = 0; i < _levelIds.Count; i++)
                if (Progress.AwardMissingStars(_levelIds[i], StarTimes(i))) awarded++;
            return awarded;
        }

        private IReadOnlyList<double> StarTimes(int index) =>
            StarTimesForLevel != null && index >= 0 ? StarTimesForLevel(index) : null;

        /// <summary>Enters Play on a level. With <paramref name="resume"/> a saved attempt is continued, clock included.</summary>
        private void Begin(int index, bool resume)
        {
            CurrentLevelIndex = index;
            var line = _layout.LineOf(index);
            if (line >= 0) SelectedLineIndex = line;
            Timer.Reset();
            LevelProgress progress = null;
            if (resume && Progress.TryGetInProgress(CurrentLevelId, out progress)) Timer.Restore(progress.Elapsed);
            Transition(GameState.Play);
            LevelStarted?.Invoke(index, progress);
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
