using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TrainSudoku.Core;
using TrainSudoku.Game;
using TrainSudoku.XR.Rules;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The XR shell (XR-PRD 6). It owns the shared <see cref="GameFlow"/>, unchanged, and keeps the platform, the
    /// signboard and the wrist menu in step with it: the network and line maps printed on the platform, the board and
    /// tray in play, the pause, the train, and the arrival. It also owns the save, the same <c>save.json</c> format as
    /// the phone's, local to the headset, and the headset's own settings (<see cref="XRPreferences"/>).
    /// </summary>
    /// <remarks>
    /// Placing the board is its precondition, not a flow state (6.1): the flow starts once the board is in the room,
    /// goes straight to the network, and never shows the Concourse. Moving the board later never touches the flow.
    ///
    /// Pause (6.3, X18) stops the clock, dims the board and locks the pieces and the handle. The wrist menu, the left
    /// controller's menu button, losing focus (the headset taken off, the system menu) and being suspended all pause
    /// play, and a return from a focus loss comes back to the pause, never straight into play. Only a RESUME button,
    /// on the menu or on the signboard, resumes.
    /// </remarks>
    public sealed class XRGame : MonoBehaviour
    {
        [SerializeField] private NetworkDefinition network;
        [SerializeField] private XRBoardAssets assets;
        [SerializeField] private XRSignageAssets signage;

        [Tooltip("Hangs the board in the room. Without one the board floats in front of the head.")]
        [SerializeField] private XRBoardPlacement placement;

        [Tooltip("The hands: the grab interface on XRI (XR-PRD 10.4).")]
        [SerializeField] private XRIGrabInput grabInput;

        [Header("Hands (XR-PRD 4)")]
        [Tooltip("The dominant hand until the player chooses one in the wrist menu's settings (6.4). The tray docks on its side of the edge the player stands at (4.1); the menu is worn on the other wrist.")]
        [SerializeField] private Hand dominantHand = Hand.Right;

        [Tooltip("How high above the platform a held piece shows its ghost and lands on release, in metres (4.3).")]
        [SerializeField] private float hoverBand = 0.10f;

        [Tooltip("Hand speed above which letting go is a throw, in m/s (4.4).")]
        [SerializeField] private float throwThreshold = 1.2f;

        [Tooltip("World size of one cell, in metres, when floating without a placement (XR-PRD X6).")]
        [SerializeField] private float cellSize = 0.06f;

        [Header("Testing")]
        [Tooltip("Lay all but this many rails of every station's solution in advance, as fixed pieces, so a station plays in seconds (QuickBoard). 0 plays the real boards; it must be 0 in a release (XR11). Quick solves still count for progress, but stay out of the star sample.")]
        [Min(0)]
        [SerializeField] private int quickTestRails;

#if UNITY_EDITOR
        [Header("Testing (Editor only)")]
        [Tooltip("Every line and station counts as open.")]
        [SerializeField] private bool unlockAllInEditor;

        [Tooltip("With no headset running, float the board in front of the camera rather than wait for a placement only a headset can make.")]
        [SerializeField] private bool floatWithoutHeadset = true;
#endif

        private List<LevelDefinition> _levels;
        private XRBoardDisplay _display;
        private XRPlatformMap _map;
        private XRSignboard _sign;
        private XRTray _tray;
        private XRPieceHands _hands;
        private XRWristMenu _menu;
        private XRPreferences _preferences;
        private bool _quickTest;
        private int _grabs;
        private int _landings;

        public GameFlow Flow { get; private set; }

        /// <summary>The board on show in play; empty on the maps.</summary>
        public XRBoardDisplay Display => _display;

        /// <summary>The wrist menu, for tools and tests.</summary>
        public XRWristMenu Menu => _menu;

        public static string SaveFilePath => Path.Combine(Application.persistentDataPath, "save.json");

        private LevelDefinition CurrentLevel => Level(Flow.CurrentLevelIndex);
        private LineDefinition CurrentLine => network.Line(Flow.CurrentLineIndex);

        private IEnumerator Start()
        {
            if (network == null)
            {
                Debug.LogError("[XR game] No network assigned.", this);
                yield break;
            }

            _preferences = new XRPreferences(new XRPlayerPrefsStore(), dominantHand);
            BuildFlow();

            Transform root;
            if (UsePlacement)
            {
                while (!placement.IsPlaced) yield return null;
                root = placement.BoardRoot;
            }
            else
            {
                yield return null;
                FloatInFrontOfHead();
                root = transform;
            }

            Build(root);
            // Localization has long finished starting by now; the player's language replaces the system's.
            XRLocale.Apply(_preferences);
            Flow.StateChanged += OnStateChanged;
            Flow.LevelStarted += OnLevelStarted;
            // Straight to the network: XR has no Concourse (6.1).
            Flow.ShowNetwork();
        }

        private bool UsePlacement
        {
            get
            {
                if (placement == null) return false;
#if UNITY_EDITOR
                if (floatWithoutHeadset && !UnityEngine.XR.XRSettings.isDeviceActive) return false;
#endif
                return true;
            }
        }

        private void BuildFlow()
        {
            _levels = network.FlatLevels();
            var ids = new List<string>(_levels.Count);
            foreach (var level in _levels) ids.Add(level != null ? level.Id : "");

            Flow = new GameFlow(ids, new FileSaveStore(SaveFilePath, message => Debug.LogWarning($"[XR save] {message}", this)), network.ToLayout());
#if UNITY_EDITOR
            Flow.UnlockAll = unlockAllInEditor;
#endif
            // The phone's thresholds, read from the level assets; no XR scaling factor yet (XR-PRD 9).
            Flow.StarTimesForLevel = index => Level(index) != null ? Level(index).StarTimes : null;
            Flow.AwardMissingStars();
        }

        private void Build(Transform root)
        {
            _display = XRBoardDisplay.Create(root, assets);
            _map = XRPlatformMap.Create(root);
            _sign = XRSignboard.Create(root, signage);
            _map.LineChosen += OnLineChosen;
            _map.StationChosen += OnStationChosen;

            // The wrist menu is worn, not placed: it hangs from nothing under the board.
            _menu = XRWristMenu.Create(signage, _preferences);
            _menu.Toggled += OnWristToggled;
            _menu.Chosen += OnWristChosen;
            _menu.ReplaceBoard += OnReplaceBoard;
            _menu.NudgeBoard += OnNudgeBoard;

            if (placement != null && root == placement.BoardRoot)
            {
                // Shadows on the real table only when there is a table under the board.
                _display.ShowShadowCatcher(placement.IsOnSurface);
                placement.Placed += () => _display.ShowShadowCatcher(placement.IsOnSurface);
                placement.SurfaceChanged += () => _display.ShowShadowCatcher(placement.IsOnSurface);
            }

            if (grabInput == null)
            {
                Debug.LogWarning("[XR game] No grab input assigned; the stations cannot be played.", this);
                return;
            }

            // A pinch on the board handle's rail is the handle's, never the corner cell's piece under it.
            grabInput.Reserved = point => placement != null && placement.Handle != null && placement.Handle.Claims(point);
            _hands = XRPieceHands.Create(transform, grabInput, XRSteam.Create(null));
            _hands.Acted += OnActed;
            _hands.BoardChanged += OnBoardChanged;
            _tray = XRTray.Create(_display, grabInput, _preferences.DominantHand);
            _tray.gameObject.SetActive(false);
        }

        /// <summary>Without a placement: the board's near edge 0.36 m ahead of the head and 0.5 m below it, facing the gaze.</summary>
        private void FloatInFrontOfHead()
        {
            var head = Camera.main != null ? Camera.main.transform : null;
            var forward = Vector3.forward;
            var position = new Vector3(0f, 1f, 0.36f);
            if (head != null)
            {
                var flat = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (flat.sqrMagnitude > 1e-4f) forward = flat.normalized;
                position = head.position + forward * 0.36f + Vector3.down * 0.5f;
            }

            transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
            transform.localScale = Vector3.one * cellSize;
        }

        private void Update()
        {
            // No rays, from first placement on: everything is taken or pressed up close.
            XRTouchOnly.Enforce();
            if (Flow == null || _display == null) return;
            Flow.Tick(Time.deltaTime);
            var state = Flow.State;

            // Pieces only in play, and never while the handle is carrying the board.
            var moving = placement != null && placement.IsMoving;
            if (grabInput != null) grabInput.AcceptsGrabs = state == GameState.Play && !moving && _display.Board != null;
            if (_hands != null)
            {
                _hands.HoverBand = hoverBand;
                _hands.ThrowThreshold = throwThreshold;
            }

            var hand = _preferences.DominantHand;
            if (_tray != null) _tray.DominantHand = hand;
            if (placement != null && placement.Handle != null)
            {
                // Paused, the handle locks with the pieces (X18); a piece in a hand hides it too.
                placement.Handle.SetAvailable(state != GameState.Pause && (_hands == null || !_hands.IsHolding));
                // The rail wraps the near corner away from the tray.
                placement.Handle.DominantHand = hand;
            }

            _menu.WristHand = _preferences.WristHand;
            _menu.Offered = WristMenuPlan.Offered(state);

            if (state == GameState.Play || state == GameState.Pause) _sign.UpdateClock(Flow.Timer.Elapsed, Flow.StarTier);
        }

        private void OnDestroy()
        {
            if (Flow != null)
            {
                Flow.StateChanged -= OnStateChanged;
                Flow.LevelStarted -= OnLevelStarted;
            }

            if (_hands != null)
            {
                _hands.Acted -= OnActed;
                _hands.BoardChanged -= OnBoardChanged;
            }

            if (_menu != null)
            {
                _menu.Toggled -= OnWristToggled;
                _menu.Chosen -= OnWristChosen;
                _menu.ReplaceBoard -= OnReplaceBoard;
                _menu.NudgeBoard -= OnNudgeBoard;
            }
        }

        // ------------------------------------------------------------------ focus and saving

        /// <summary>Suspended (6.3): play pauses, which saves the attempt; anywhere else the attempt is only saved.</summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused) LoseFocus();
        }

        /// <summary>The headset taken off, the system menu or passthrough settings opened (6.3).</summary>
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) LoseFocus();
        }

        private void OnApplicationQuit() => SaveProgress();

        private void LoseFocus()
        {
            if (Flow == null) return;
#if UNITY_EDITOR
            // An Editor with no headset loses focus whenever another window is clicked; only a headset session pauses on it.
            if (!UnityEngine.XR.XRSettings.isDeviceActive)
            {
                SaveProgress();
                return;
            }
#endif
            PauseForFocusLoss();
        }

        /// <summary>Play pauses, and the pause saves the attempt (OnStateChanged). Coming back finds it paused, never playing.</summary>
        private void PauseForFocusLoss()
        {
            if (WristMenuPlan.PausesOnFocusLoss(Flow.State)) Flow.PauseGame();
            else SaveProgress();
        }

        private void SaveProgress()
        {
            if (Flow == null || _display == null || _display.Board == null) return;
            if (Flow.State == GameState.Play || Flow.State == GameState.Pause) Flow.SaveProgress(_display.Board);
        }

        // ------------------------------------------------------------------ the flow

        private void OnStateChanged(GameState previous, GameState current)
        {
            Debug.Log($"[XR flow] {previous} -> {current}");
            if (previous == GameState.Play && current == GameState.Pause) SaveProgress();
            if (previous == GameState.Play && current != GameState.Play && _hands != null) _hands.End();
            if (previous == GameState.Pause) _display.SetDimmed(false);
            // Every change of state closes the menu. Opening it as the pause reopens it once the pause is in (OnWristToggled).
            _menu.Close();

            switch (current)
            {
                case GameState.Network:
                    ClearBoard();
                    _map.ShowNetwork(network, Flow.IsLineUnlocked);
                    FitHandle(XRPlatformMap.HalfWidth);
                    _sign.ShowMasthead(TotalStars());
                    _sign.PlaceBehind(XRPlatformMap.FarEdge);
                    break;

                case GameState.LevelSelect:
                {
                    ClearBoard();
                    var lineIndex = Flow.SelectedLineIndex;
                    var line = network.Line(lineIndex);
                    _map.ShowLine(line, station => MarkOf(lineIndex, station));
                    FitHandle(XRPlatformMap.HalfWidth);
                    _sign.ShowLine(line, ClearedOn(lineIndex), Flow.Layout.StationCount(lineIndex), Flow.StarsOnLine(lineIndex));
                    _sign.PlaceBehind(XRPlatformMap.FarEdge);
                    break;
                }

                case GameState.Play:
                    _map.Hide();
                    if (_tray != null) _tray.gameObject.SetActive(true);
                    // A resume keeps the board; a start or retry loads its level next, in OnLevelStarted.
                    if (previous == GameState.Pause && _hands != null && _display.Board != null) _hands.Begin(_display, _tray);
                    ShowStationSign();
                    break;

                case GameState.Pause:
                    // The flow has stopped the clock; the board dims, and its pieces and the handle lock (X18).
                    _display.SetDimmed(true);
                    _sign.ShowPaused(CurrentLevel, CurrentLine, Flow.CurrentStationIndex, () => When(GameState.Pause, Flow.ResumeGame));
                    _sign.PlaceBehind(BoardFarEdge);
                    break;

                case GameState.TrainRun:
                    if (_tray != null) _tray.gameObject.SetActive(false);
                    _sign.Hide();
                    // It always runs into the exit tunnel before the arrival: there is no skip (6.2, revised after the XR7
                    // headset check, where a stray pinch after the winning drop cut it short).
                    _display.RunTrain(CurrentLevel != null ? CurrentLevel.DisplayName : "", OnTrainFinished);
                    break;

                case GameState.Win:
                    _sign.ShowArrival(CurrentLevel, CurrentLine, Flow.LastResult ?? default, Flow.IsLineComplete,
                        () => When(GameState.Win, Flow.NextLevel),
                        () => When(GameState.Win, Flow.Retry),
                        () => When(GameState.Win, Flow.ShowLevelSelect));
                    _sign.PlaceBehind(BoardFarEdge);
                    break;
            }
        }

        private void OnLevelStarted(int index, LevelProgress resume)
        {
            var level = Level(index);
            if (level == null) return;

            var line = CurrentLine;
            var data = level.ToLevelData();
            // Testing: all but the last few rails laid in advance, as fixed pieces, on this copy of the level only.
            _quickTest = quickTestRails > 0 && QuickBoard.TryLeave(data, quickTestRails, out _);
            _display.Load(data, line != null ? line.Color : XRPalette.Warn);
            FitHandle((float)BoardLayout.HalfWidth(data.Width));
            // Put back without legality checks, as the phone does: replaying the pieces one by one can refuse a board
            // the player was allowed to build. The clock waits, idle at the saved time, for the first grab.
            if (resume != null && resume.ApplyTo(_display.Board) > 0) _display.Sync(false);

            _grabs = 0;
            _landings = 0;
            if (_tray != null)
            {
                _tray.gameObject.SetActive(true);
                _tray.Build();
            }

            if (_hands != null) _hands.Begin(_display, _tray);
            ShowStationSign();
            Debug.Log($"[XR game] {level.DisplayName} ({level.Width}x{level.Height})" +
                      (resume != null ? $", continuing at {ProgressTracker.FormatTime(resume.Elapsed)} with {resume.Pieces.Count} rails" : "") +
                      (_quickTest ? $", quick test: {quickTestRails} rails to lay" : ""));
        }

        private void ShowStationSign()
        {
            _sign.ShowStation(CurrentLevel, CurrentLine, Flow.CurrentStationIndex);
            _sign.PlaceBehind(BoardFarEdge);
        }

        /// <summary>The board's far edge in cells from its root: it grows away from the player (X17).</summary>
        private float BoardFarEdge =>
            _display.Level != null ? 2f * (float)BoardLayout.HalfDepth(_display.Level.Height) : XRPlatformMap.FarEdge;

        /// <summary>The board handle wraps the near corner of whatever is on show: a board, or the map card.</summary>
        private void FitHandle(float halfWidth)
        {
            if (placement != null && placement.Handle != null) placement.Handle.SetFootprint(halfWidth);
        }

        /// <summary>The platform shows a map: the board and the tray go.</summary>
        private void ClearBoard()
        {
            _display.Clear();
            if (_tray != null) _tray.gameObject.SetActive(false);
        }

        private void OnTrainFinished()
        {
            if (Flow.State == GameState.TrainRun) Flow.FinishTrainRun();
        }

        /// <summary>A button acts only in the state that drew it: a double press must not hit the flow twice.</summary>
        private void When(GameState state, Action action)
        {
            if (Flow.State == state) action();
        }

        // ------------------------------------------------------------------ the wrist menu

        private void OnWristToggled()
        {
            switch (WristMenuPlan.Toggle(Flow.State, _menu.IsOpen))
            {
                case WristToggle.OpenAndPause:
                    // The pause closes the menu on its way in (OnStateChanged), so it opens after.
                    Flow.PauseGame();
                    OpenMenu();
                    break;
                case WristToggle.Open:
                    OpenMenu();
                    break;
                case WristToggle.Close:
                    _menu.Close();
                    break;
            }
        }

        private void OpenMenu()
        {
            string title;
            switch (Flow.State)
            {
                case GameState.Pause:
                    title = "PAUSED";
                    break;
                case GameState.LevelSelect:
                {
                    var line = network.Line(Flow.SelectedLineIndex);
                    title = line != null ? line.DisplayName.ToUpperInvariant() : "LINE MAP";
                    break;
                }
                default:
                    title = "NETWORK";
                    break;
            }

            _menu.Open(WristMenuPlan.Items(Flow.State), title);
        }

        /// <summary>An entry acts only in the state that offers it; the state change it makes closes the menu.</summary>
        private void OnWristChosen(WristItem item)
        {
            switch (item)
            {
                case WristItem.Resume:
                    When(GameState.Pause, Flow.ResumeGame);
                    break;
                case WristItem.Retry:
                    When(GameState.Pause, Flow.Retry);
                    break;
                case WristItem.BackToMap:
                    // From the pause, which saved the attempt on the way in.
                    When(GameState.Pause, Flow.ShowLevelSelect);
                    break;
                case WristItem.BackToNetwork:
                    When(GameState.LevelSelect, Flow.ShowNetwork);
                    break;
                case WristItem.Close:
                    _menu.Close();
                    break;
            }
        }

        /// <summary>Re-place board (5.3, 6.4): first placement runs again; the flow stays where it is, paused if it was playing.</summary>
        private void OnReplaceBoard()
        {
            _menu.Close();
            if (Flow.State == GameState.Play) Flow.PauseGame();
            if (UsePlacement) placement.Replace();
            else FloatInFrontOfHead();
        }

        /// <summary>The height nudge (6.4): up or down by <paramref name="metres"/>.</summary>
        private void OnNudgeBoard(float metres)
        {
            if (UsePlacement) placement.Nudge(metres);
            else transform.position += Vector3.up * metres;
        }

        // ------------------------------------------------------------------ the platform maps

        private void OnLineChosen(int line)
        {
            if (Flow.State == GameState.Network && Flow.IsLineUnlocked(line)) Flow.ShowLineMap(line);
        }

        private void OnStationChosen(int station)
        {
            if (Flow.State != GameState.LevelSelect) return;
            var flat = Flow.Layout.FlatIndex(Flow.SelectedLineIndex, station);
            if (flat >= 0 && Flow.IsUnlocked(flat)) Flow.StartLevel(flat);
        }

        private StationMark MarkOf(int line, int station)
        {
            var flat = Flow.Layout.FlatIndex(line, station);
            var level = Level(flat);
            if (level == null) return new StationMark(false, false, 0, false);
            return new StationMark(Flow.IsUnlocked(flat), Flow.HasInProgress(flat), Flow.Progress.GetStars(level.Id), station == NextStationOn(line));
        }

        /// <summary>The first station on a line with no stars yet, or -1 once every one has some.</summary>
        private int NextStationOn(int line)
        {
            for (var station = 0; station < Flow.Layout.StationCount(line); station++)
            {
                var level = Level(Flow.Layout.FlatIndex(line, station));
                if (level != null && Flow.Progress.GetStars(level.Id) == 0) return station;
            }

            return -1;
        }

        private int ClearedOn(int line)
        {
            var cleared = 0;
            for (var station = 0; station < Flow.Layout.StationCount(line); station++)
            {
                var level = Level(Flow.Layout.FlatIndex(line, station));
                if (level != null && Flow.Progress.GetStars(level.Id) > 0) cleared++;
            }

            return cleared;
        }

        private int TotalStars()
        {
            var stars = 0;
            for (var line = 0; line < Flow.LineCount; line++) stars += Flow.StarsOnLine(line);
            return stars;
        }

        private LevelDefinition Level(int index) => _levels != null && index >= 0 && index < _levels.Count ? _levels[index] : null;

        // ------------------------------------------------------------------ the hands

        private void OnActed(DropResult result)
        {
            switch (result.Outcome)
            {
                case DropOutcome.Taken:
                case DropOutcome.Lifted:
                    _grabs++;
                    // The first grab of any piece starts the clock (4.2), a restored one included; later grabs leave it running.
                    if (Flow.State == GameState.Play) Flow.BoardTouched();
                    break;
                case DropOutcome.Placed:
                case DropOutcome.Moved:
                case DropOutcome.Replaced:
                    _landings++;
                    break;
            }

            Debug.Log($"[XR hands] {result}");
        }

        /// <summary>The board changed: saved, or won. Every change is written through, as on the phone.</summary>
        private void OnBoardChanged()
        {
            if (Flow.State != GameState.Play || _display.Board == null) return;
            if (_display.LastResult != null && _display.LastResult.IsWin)
            {
                // Only whole solves by hand belong in the sample: the Editor's debug solve grabs nothing, and a quick
                // test board is mostly laid already.
                if (_grabs > 0 && !_quickTest) XRStarSample.Record(CurrentLevel, Flow.Timer.Elapsed, _grabs, _landings, UsePlacement ? placement.CellSize : cellSize);
                Flow.CompleteLevel();
                return;
            }

            Flow.SaveProgress(_display.Board);
        }

#if UNITY_EDITOR
        /// <summary>Editor only: lays the station's solution and wins it, to walk the flow without a headset.</summary>
        [ContextMenu("Debug: Solve This Station")]
        public void DebugSolve()
        {
            if (Flow == null || Flow.State != GameState.Play || _display.Board == null) return;
            if (!Solver.TrySolve(_display.Level, out var solved))
            {
                Debug.LogWarning("[XR game] The solver could not finish this board.", this);
                return;
            }

            Flow.BoardTouched();
            var board = _display.Board;
            for (var y = 0; y < board.Height; y++)
            for (var x = 0; x < board.Width; x++)
            {
                if (!(solved[x, y] is Piece piece) || piece.IsFixed) continue;
                if (board[x, y] is Piece placed && !placed.IsFixed && placed.Key != piece.Key) board.TryErase(x, y);
                if (!board[x, y].HasValue) board.SetUnchecked(x, y, new Piece(piece.Key, false));
            }

            _display.Sync(true);
            OnBoardChanged();
        }

        /// <summary>Editor only: what taking the headset off does (6.3), without a headset to take off.</summary>
        [ContextMenu("Debug: Lose Focus")]
        public void DebugLoseFocus()
        {
            if (Flow != null) PauseForFocusLoss();
        }

        /// <summary>Editor only: what pressing the wrist roundel or the menu button does (6.4).</summary>
        [ContextMenu("Debug: Press Wrist Menu")]
        public void DebugPressWristMenu()
        {
            if (Flow != null && _menu != null) OnWristToggled();
        }
#endif
    }
}
