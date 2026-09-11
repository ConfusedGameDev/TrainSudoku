using System.Collections.Generic;
using System.IO;
using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Scene entry point. Owns the <see cref="GameFlow"/> and keeps the generated UI and board in step with it:
    /// screens show according to the flow state, board events feed the flow, and the clock ticks every frame. An
    /// unfinished level is auto-saved after every board change, on pause and when the app quits, and backgrounding the
    /// app pauses it. The scene objects (UI shell, screens, board root, audio player, event system) are created by
    /// <see cref="Generate"/>, normally from the Inspector button so they are saved in the scene. If a scene was never
    /// generated, <see cref="Awake"/> generates them at runtime as a fallback.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        [Header("Content")]
        [Tooltip("Every line the game ships. The flat level order is this asset's line order, concatenated, and that " +
                 "order is the save-file identity.")]
        [SerializeField] private NetworkDefinition network = null;

        [Tooltip("Legacy flat list, kept as a fallback for a scene authored before the network existed.")]
        [SerializeField] private LevelCollection levels = null;
        [SerializeField] private InputActionAsset inputActions = null;
        [SerializeField] private AudioCueLibrary audioCues = null;
        [SerializeField] private TrackAssets trackAssets = null;
        [SerializeField] private TrainAssets trainAssets = null;

        [Tooltip("Editor only: every level counts as unlocked in Level Select (PRD section 5).")]
        [SerializeField] private bool unlockAllLevelsInEditor = false;

        [Header("Erase cue")]
        [Tooltip("The ring that fills while a piece is held down to erase it. Leave fully transparent to keep the palette's own yellow.")]
        [SerializeField] private Color eraseRingColour = Palette.Warn;

        [Tooltip("Scales the ring's radii. The cell is one unit across, so much above 1.15 laps onto the neighbouring slabs.")]
        [Range(0.2f, 2f)] [SerializeField] private float eraseRingScale = 1f;

        [Header("Generated scene objects")]
        [SerializeField] private UiShell shell = null;
        [SerializeField] private EventSystem eventSystem = null;
        [SerializeField] private AudioCuePlayer audioPlayer = null;
        [SerializeField] private BoardCamera boardCamera = null;
        [SerializeField] private BoardView boardView = null;
        [SerializeField] private TrainRunner trainRunner = null;
        [SerializeField] private List<UiScreen> screens = new List<UiScreen>();

        [Header("UI assets")]
        [SerializeField] private PanelSettings panelSettings = null;
        [SerializeField] private StyleSheet tokenSheet = null;
        [SerializeField] private StyleSheet componentSheet = null;

        private PlayScreen _playScreen;
        private List<LevelDefinition> _flatLevels;

        /// <summary>The JSON save file (PRD section 6).</summary>
        public static string SaveFilePath => Path.Combine(Application.persistentDataPath, "save.json");

        public GameFlow Flow { get; private set; }
        public ISaveStore SaveStore { get; private set; }
        public LevelCollection Levels => levels;
        public IBoardView Board => boardView;

        /// <summary>The rig that frames the board. The Play screen hands it the two bar heights (work order 5.3).</summary>
        public BoardCamera BoardCamera => boardCamera;

        public UiShell Shell => shell;
        public NetworkDefinition Network => network;

        /// <summary>Every level across every line, in line order. This order is the save-file identity.</summary>
        public IReadOnlyList<LevelDefinition> FlatLevels => _flatLevels;

        public LineDefinition CurrentLine => network != null ? network.Line(Flow != null ? Flow.SelectedLineIndex : 0) : null;

        public LevelDefinition CurrentLevel => Level(Flow != null ? Flow.CurrentLevelIndex : -1);

        private LevelDefinition Level(int flatIndex) =>
            _flatLevels != null && flatIndex >= 0 && flatIndex < _flatLevels.Count ? _flatLevels[flatIndex] : null;

        /// <summary>One station of one line, or null.</summary>
        public LevelDefinition Station(int lineIndex, int station) =>
            Level(Flow != null ? Flow.Layout.FlatIndex(lineIndex, station) : -1);

        public string StationId(int lineIndex, int station)
        {
            var level = Station(lineIndex, station);
            return level != null ? level.Id : null;
        }

        /// <summary>
        /// The only line with content, or -1 when the network has a choice to offer. While it is set, the network
        /// screen is a hop with nothing in it -- one line drawn without its stations, between the concourse and that
        /// same line's map -- so the screens step over it and go straight to the line. It is the same reasoning as
        /// D10, which keeps empty lines off the map: a network of one reads as a missing screen, not a network.
        /// The moment a second line has content this returns -1 and the network takes its place back.
        /// </summary>
        public int SoleLineIndex
        {
            get
            {
                if (network == null) return -1;
                var only = -1;
                for (var i = 0; i < network.LineCount; i++)
                {
                    var line = network.Line(i);
                    if (line == null || !line.HasContent) continue;   // D10: the same lines the map draws
                    if (only >= 0) return -1;
                    only = i;
                }

                return only;
            }
        }

        /// <summary>
        /// The first station on a line the player has not cleared, or -1 when the line is finished. This is what
        /// "board now" means and where the line map opens.
        /// </summary>
        public int NextStationOnLine(int lineIndex)
        {
            if (Flow == null) return -1;
            var count = Flow.Layout.StationCount(lineIndex);
            for (var station = 0; station < count; station++)
            {
                var id = StationId(lineIndex, station);
                if (id == null) continue;
                if (Flow.Progress.GetStars(id) == 0) return station;
            }

            return -1;
        }

        /// <summary>The same, as a flat index, for the concourse's one button.</summary>
        public int NextStationIndex()
        {
            if (Flow == null) return -1;
            var station = NextStationOnLine(Flow.SelectedLineIndex);
            return station < 0 ? -1 : Flow.Layout.FlatIndex(Flow.SelectedLineIndex, station);
        }

        /// <summary>True when every generated object exists and all seven screens are present.</summary>
        public bool IsGenerated
        {
            get
            {
                if (shell == null || audioPlayer == null || boardCamera == null || boardView == null || !boardView.IsGenerated) return false;
                if (trainRunner == null) return false;
                // Seven screens, one per row of work order section 2. Change this with the screen list or the scene
                // silently regenerates its UI on every launch.
                if (screens == null || screens.Count != 7) return false;
                foreach (var screen in screens)
                    if (screen == null) return false;
                return true;
            }
        }

        // ------------------------------------------------------------------ generation

        /// <summary>Creates the UI shell and its screens, the board root, the audio player and the event system.</summary>
        [ContextMenu("Generate Scene Objects")]
        public void Generate()
        {
            ClearGenerated();

            var camera = Camera.main;
            if (camera == null) camera = FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
            boardCamera = BoardCamera.Attach(camera);
            if (boardCamera == null) Debug.LogWarning("GameManager: no camera in the scene, the board will not be visible.", this);

            audioPlayer = AudioCuePlayer.Create(transform, audioCues);
            // UI Toolkit still routes runtime pointer events through the EventSystem, so this stays.
            eventSystem = SceneObjects.EnsureEventSystem(inputActions, transform);
            shell = UiShell.Create(transform, panelSettings, tokenSheet, componentSheet);

            screens.Clear();
            AddScreen<ConcourseScreen>("Concourse");
            AddScreen<NetworkScreen>("Network");
            AddScreen<LineMapScreen>("Line Map");
            AddScreen<PlayScreen>("Play");
            AddScreen<SignalStopScreen>("Signal Stop");
            AddScreen<TrainRunScreen>("Train Run");
            AddScreen<ArrivalScreen>("Arrival");

            boardView = BoardView.Create(transform, boardCamera, trackAssets);
            trainRunner = TrainRunner.Create(transform, trainAssets);
        }

        /// <summary>Removes everything <see cref="Generate"/> created. The camera keeps its BoardCamera.</summary>
        [ContextMenu("Clear Generated Scene Objects")]
        public void ClearGenerated()
        {
            if (shell != null) SceneObjects.Destroy(shell.gameObject);
            if (audioPlayer != null) SceneObjects.Destroy(audioPlayer.gameObject);
            if (boardView != null) SceneObjects.Destroy(boardView.gameObject);
            if (trainRunner != null) SceneObjects.Destroy(trainRunner.gameObject);
            // Only remove the event system if it is ours; the scene may have had one already.
            if (eventSystem != null && eventSystem.transform.parent == transform) SceneObjects.Destroy(eventSystem.gameObject);
            shell = null;
            audioPlayer = null;
            boardView = null;
            trainRunner = null;
            eventSystem = null;
            screens.Clear();
        }

        /// <summary>
        /// One screen, one <see cref="UIDocument"/>, all sharing the shell's <see cref="PanelSettings"/>. Nothing is
        /// baked: a UI Toolkit tree is built in code at runtime, so unlike the uGUI panels there are no serialized
        /// widget references and no Generate step is needed after changing a screen's layout.
        /// </summary>
        private void AddScreen<T>(string name) where T : UiScreen
        {
            var go = new GameObject(name);
            go.transform.SetParent(shell != null ? shell.transform : transform, false);
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            screens.Add(go.AddComponent<T>());
        }

        // ------------------------------------------------------------------ runtime

        private void Awake()
        {
            // The network is the source of truth; the flat collection is the fallback for a scene authored before it.
            if (network != null)
            {
                _flatLevels = network.FlatLevels();
            }
            else
            {
                if (levels == null)
                {
                    Debug.LogWarning("GameManager has neither a NetworkDefinition nor a LevelCollection; the line will be empty.", this);
                    levels = ScriptableObject.CreateInstance<LevelCollection>();
                }

                _flatLevels = new List<LevelDefinition>(levels.Levels);
            }

            if (!IsGenerated)
            {
                Debug.Log("GameManager: scene objects were not generated in the Editor; generating them now.", this);
                Generate();
            }

            var ids = new List<string>(_flatLevels.Count);
            foreach (var level in _flatLevels) ids.Add(level != null ? level.Id : "");

            SaveStore = new FileSaveStore(SaveFilePath, message => Debug.LogWarning($"Save: {message}", this));
            Flow = new GameFlow(ids, SaveStore, network != null ? network.ToLayout() : null);
#if UNITY_EDITOR
            Flow.UnlockAll = unlockAllLevelsInEditor;
#endif

            // Core cannot read a ScriptableObject, so the flow is handed a lookup for the star thresholds. Then any
            // level carrying a best time but no rating - the shape a version 1 save leaves behind - has one worked
            // out from those thresholds. Both are idempotent and neither can lower a rating already earned.
            Flow.StarTimesForLevel = index =>
                index >= 0 && index < _flatLevels.Count && _flatLevels[index] != null ? _flatLevels[index].StarTimes : null;
            Flow.AwardMissingStars();

            foreach (var screen in screens)
            {
                screen.Bind(this);
                if (screen is PlayScreen play) _playScreen = play;
            }

            // The active line's colour is published once here; every state change republishes it.
            if (CurrentLine != null)
            {
                if (shell != null) shell.SetLineColour(CurrentLine.Color);
                BoardMaterials.SetLineColour(CurrentLine.Color);
            }

            boardView.Configure(trackAssets);
            boardView.SetEraseRing(eraseRingColour, eraseRingScale);
            boardView.Interacted += OnBoardInteracted;
            boardView.Completed += OnBoardCompleted;
            boardView.BoardChanged += SaveProgress;
            boardView.BoardChanged += OnBoardChanged;
            boardView.LineCleared += OnLineCleared;

            // Scenes baked before the train existed get the runner at runtime; Regenerate bakes it.
            if (trainRunner == null) trainRunner = TrainRunner.Create(transform, trainAssets);
            trainRunner.Configure(trainAssets);
            trainRunner.Finished += OnTrainFinished;

            Flow.StateChanged += OnStateChanged;
            Flow.LevelStarted += OnLevelStarted;
            ApplyState(Flow.State);
        }

        private void Update()
        {
            Flow.Tick(Time.deltaTime);
            if (Flow.State == GameState.Play && _playScreen != null) _playScreen.UpdateClock(Flow.Timer.Elapsed);
        }

        private void OnDestroy()
        {
            if (Flow == null) return;
            Flow.StateChanged -= OnStateChanged;
            Flow.LevelStarted -= OnLevelStarted;
            if (boardView != null)
            {
                boardView.BoardChanged -= SaveProgress;
                boardView.BoardChanged -= OnBoardChanged;
                boardView.LineCleared -= OnLineCleared;
            }

            if (trainRunner != null) trainRunner.Finished -= OnTrainFinished;
        }

        /// <summary>Going to the background mid-level pauses the game, which also saves the attempt.</summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused && Flow != null && Flow.State == GameState.Play) Flow.PauseGame();
        }

        private void OnApplicationQuit()
        {
            if (Flow != null && Flow.State == GameState.Play) SaveProgress();
        }

        private void OnStateChanged(GameState previous, GameState current)
        {
            if (previous == GameState.Play && current == GameState.Pause) SaveProgress();
            ApplyState(current);
        }

        /// <summary>Writes the current attempt to the save file. Safe to call in any state; only Play and Pause have one.</summary>
        private void SaveProgress()
        {
            if (Flow.State != GameState.Play && Flow.State != GameState.Pause) return;
            if (boardView.Board == null) return;
            Flow.SaveProgress(boardView.Board);
        }

        private void ApplyState(GameState state)
        {
            // The loop is deliberately unchanged from the uGUI version: show, then refresh what is showing.
            // The line's colour goes to both halves of the game: the shell tints the screens, the board tints its
            // fixed pieces and forced markers.
            if (CurrentLine != null)
            {
                if (shell != null) shell.SetLineColour(CurrentLine.Color);
                BoardMaterials.SetLineColour(CurrentLine.Color);
            }

            foreach (var screen in screens)
            {
                var visible = screen.IsVisibleIn(state);
                screen.SetVisible(visible);
                if (visible) screen.Refresh(state);
            }

            boardView.SetInteractable(state == GameState.Play);

            if (state == GameState.TrainRun)
            {
                trainRunner.SetDestination(CurrentLevel != null ? CurrentLevel.DisplayName : "");
                // The board knows how deep the tunnels it built are; the runner hides its cars to match.
                trainRunner.RevealDistance = boardView.TunnelRevealDistance;
                trainRunner.Run(boardView.Board);
            }
            else if (trainRunner.IsRunning) trainRunner.Stop();
        }

        private void OnTrainFinished()
        {
            if (Flow.State == GameState.TrainRun) Flow.FinishTrainRun();
        }

        private void OnLevelStarted(int index, LevelProgress resume) => boardView.Load(Level(index), resume);

        /// <summary>A satisfied row or column goes to the LED strip, which is the Play screen's to print.</summary>
        /// <summary>A piece went down or came up: the play screen's track-laid strip counts it.</summary>
        private void OnBoardChanged()
        {
            if (_playScreen != null) _playScreen.UpdateProgress();
        }

        private void OnLineCleared(bool isRow, int index)
        {
            if (_playScreen != null) _playScreen.AnnounceLineClear(isRow, index);
        }

        private void OnBoardInteracted()
        {
            if (Flow.State == GameState.Play) Flow.BoardTouched();
        }

        private void OnBoardCompleted()
        {
            if (Flow.State != GameState.Play) return;
            AudioCuePlayer.Play(AudioCue.Win);
            Flow.CompleteLevel();
        }

        /// <summary>Editor-only: resumes if paused and declares the level solved, to test the win screens without solving.</summary>
        public void DebugCompleteLevel()
        {
            if (!Application.isEditor) return;
            if (Flow.State == GameState.Pause) Flow.ResumeGame();
            if (Flow.State == GameState.Play) boardView.ForceComplete();
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
