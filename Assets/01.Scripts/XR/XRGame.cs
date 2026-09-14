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
    /// The XR shell (XR-PRD 6). It owns the shared <see cref="GameFlow"/>, unchanged, and keeps the platform and the
    /// signboard in step with it: the network and line maps printed on the platform, the board and tray in play, the
    /// train, and the arrival. It also owns the save, the same <c>save.json</c> format as the phone's, local to the headset.
    /// </summary>
    /// <remarks>
    /// Placing the board is its precondition, not a flow state (6.1): the flow starts once the board is in the room,
    /// goes straight to the network, and never shows the Concourse. Moving the board later never touches the flow.
    ///
    /// Until the wrist menu arrives (XR8) the signboard carries the way back: the line map from a station, the network
    /// from a line. Losing focus saves the attempt; XR8 turns it into the Pause of 6.3.
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
        [Tooltip("The tray docks on this side of the edge the player stands at (4.1). Becomes a setting at XR8.")]
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
        private bool _quickTest;
        private int _grabs;
        private int _landings;

        public GameFlow Flow { get; private set; }

        /// <summary>The board on show in play; empty on the maps.</summary>
        public XRBoardDisplay Display => _display;

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

            _hands = XRPieceHands.Create(transform, grabInput, XRSteam.Create(null));
            _hands.Acted += OnActed;
            _hands.BoardChanged += OnBoardChanged;
            _tray = XRTray.Create(_display, grabInput, dominantHand);
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
            if (Flow == null || _display == null) return;
            Flow.Tick(Time.deltaTime);

            // Pieces only in play, and never while the handle is carrying the board.
            var moving = placement != null && placement.IsMoving;
            if (grabInput != null) grabInput.AcceptsGrabs = Flow.State == GameState.Play && !moving && _display.Board != null;
            if (_hands != null)
            {
                _hands.HoverBand = hoverBand;
                _hands.ThrowThreshold = throwThreshold;
                if (placement != null && placement.Handle != null) placement.Handle.SetAvailable(!_hands.IsHolding);
            }

            if (_tray != null) _tray.DominantHand = dominantHand;
            // The handle's knob rests at the near corner away from the tray.
            if (placement != null && placement.Handle != null) placement.Handle.DominantHand = dominantHand;

            if (Flow.State == GameState.Play) _sign.UpdateClock(Flow.Timer.Elapsed, Flow.StarTier);
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
        }

        // ------------------------------------------------------------------ saving

        /// <summary>Taking the headset off, the system menu, or suspending: the attempt is saved (6.3; XR8 adds the pause).</summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveProgress();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SaveProgress();
        }

        private void OnApplicationQuit() => SaveProgress();

        private void SaveProgress()
        {
            if (Flow == null || _display == null || _display.Board == null) return;
            if (Flow.State == GameState.Play || Flow.State == GameState.Pause) Flow.SaveProgress(_display.Board);
        }

        // ------------------------------------------------------------------ the flow

        private void OnStateChanged(GameState previous, GameState current)
        {
            if (previous == GameState.Play && current == GameState.Pause) SaveProgress();
            if (previous == GameState.Play && current != GameState.Play && _hands != null) _hands.End();

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
                    _sign.ShowLine(line, ClearedOn(lineIndex), Flow.Layout.StationCount(lineIndex), Flow.StarsOnLine(lineIndex),
                        () => When(GameState.LevelSelect, Flow.ShowNetwork));
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
                    // XR8: the board dims and the wrist menu opens. At XR7 the flow only passes through on its way to the map.
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
            _sign.ShowStation(CurrentLevel, CurrentLine, Flow.CurrentStationIndex, () => When(GameState.Play, () =>
            {
                // Pause first: that is the flow's only way out of play, and it saves the attempt on the way.
                Flow.PauseGame();
                Flow.ShowLevelSelect();
            }));
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

        /// <summary>A sign button acts only in the state that drew it: a double press must not hit the flow twice.</summary>
        private void When(GameState state, Action action)
        {
            if (Flow.State == state) action();
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
#endif
    }
}
