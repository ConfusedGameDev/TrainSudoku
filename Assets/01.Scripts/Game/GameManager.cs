using System.Collections.Generic;
using System.IO;
using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Scene entry point. Owns the <see cref="GameFlow"/> and keeps the generated UI and board in step with it: panels
    /// show according to the flow state, board events feed the flow, and the clock ticks every frame.
    /// The scene objects (canvas, panels, board root, audio player, event system) are created by
    /// <see cref="Generate"/>, normally from the Inspector button so they are saved in the scene. If a scene was never
    /// generated, <see cref="Awake"/> generates them at runtime as a fallback.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private LevelCollection levels = null;
        [SerializeField] private InputActionAsset inputActions = null;
        [SerializeField] private AudioCueLibrary audioCues = null;
        [SerializeField] private TrackAssets trackAssets = null;
        [SerializeField] private TrainAssets trainAssets = null;

        [Tooltip("Editor only: every level counts as unlocked in Level Select (PRD section 5).")]
        [SerializeField] private bool unlockAllLevelsInEditor = false;

        [Header("Generated scene objects")]
        [SerializeField] private Canvas canvas = null;
        [SerializeField] private EventSystem eventSystem = null;
        [SerializeField] private AudioCuePlayer audioPlayer = null;
        [SerializeField] private BoardCamera boardCamera = null;
        [SerializeField] private BoardView boardView = null;
        [SerializeField] private TrainRunner trainRunner = null;
        [SerializeField] private List<PanelBase> panels = new List<PanelBase>();

        private PlayPanel _playPanel;

        /// <summary>The JSON save file (PRD section 6).</summary>
        public static string SaveFilePath => Path.Combine(Application.persistentDataPath, "save.json");

        public GameFlow Flow { get; private set; }
        public ISaveStore SaveStore { get; private set; }
        public LevelCollection Levels => levels;
        public IBoardView Board => boardView;

        public LevelDefinition CurrentLevel =>
            Flow != null && Flow.CurrentLevelIndex >= 0 && Flow.CurrentLevelIndex < levels.Count ? levels[Flow.CurrentLevelIndex] : null;

        /// <summary>True when every generated object exists and the panels have their widgets.</summary>
        public bool IsGenerated
        {
            get
            {
                if (canvas == null || audioPlayer == null || boardCamera == null || boardView == null || !boardView.IsGenerated) return false;
                if (trainRunner == null) return false;
                if (panels == null || panels.Count != 6) return false;
                foreach (var panel in panels)
                    if (panel == null || !panel.IsBuilt) return false;
                return true;
            }
        }

        // ------------------------------------------------------------------ generation

        /// <summary>Creates the canvas, panels, board root, audio player and event system as children of this object.</summary>
        [ContextMenu("Generate Scene Objects")]
        public void Generate()
        {
            ClearGenerated();

            var camera = Camera.main;
            if (camera == null) camera = FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
            boardCamera = BoardCamera.Attach(camera);
            if (boardCamera == null) Debug.LogWarning("GameManager: no camera in the scene, the board will not be visible.", this);

            audioPlayer = AudioCuePlayer.Create(transform, audioCues);
            eventSystem = UiBuilder.EnsureEventSystem(inputActions, transform);
            canvas = UiBuilder.CreateCanvas("UI", transform);

            panels.Clear();
            AddPanel<MainMenuPanel>("Main Menu");
            AddPanel<LevelSelectPanel>("Level Select");
            AddPanel<PlayPanel>("Play");
            AddPanel<PausePanel>("Pause");
            AddPanel<TrainRunPanel>("Train Run");
            AddPanel<WinPanel>("Win");

            boardView = BoardView.Create(transform, boardCamera, trackAssets);
            trainRunner = TrainRunner.Create(transform, trainAssets);
        }

        /// <summary>Removes everything <see cref="Generate"/> created. The camera keeps its BoardCamera.</summary>
        [ContextMenu("Clear Generated Scene Objects")]
        public void ClearGenerated()
        {
            if (canvas != null) UiBuilder.Destroy(canvas.gameObject);
            if (audioPlayer != null) UiBuilder.Destroy(audioPlayer.gameObject);
            if (boardView != null) UiBuilder.Destroy(boardView.gameObject);
            if (trainRunner != null) UiBuilder.Destroy(trainRunner.gameObject);
            // Only remove the event system if it is ours; the scene may have had one already.
            if (eventSystem != null && eventSystem.transform.parent == transform) UiBuilder.Destroy(eventSystem.gameObject);
            canvas = null;
            audioPlayer = null;
            boardView = null;
            trainRunner = null;
            eventSystem = null;
            panels.Clear();
        }

        private void AddPanel<T>(string name) where T : PanelBase
        {
            var rect = UiBuilder.Stretch(UiBuilder.Rect(canvas.transform, name));
            var panel = rect.gameObject.AddComponent<T>();
            panel.Build();
            panels.Add(panel);
        }

        // ------------------------------------------------------------------ runtime

        private void Awake()
        {
            if (levels == null)
            {
                Debug.LogWarning("GameManager has no LevelCollection assigned; Level Select will be empty.", this);
                levels = ScriptableObject.CreateInstance<LevelCollection>();
            }

            if (!IsGenerated)
            {
                Debug.Log("GameManager: scene objects were not generated in the Editor; generating them now.", this);
                Generate();
            }

            var ids = new List<string>(levels.Count);
            foreach (var level in levels.Levels) ids.Add(level != null ? level.Id : "");

            SaveStore = new FileSaveStore(SaveFilePath, message => Debug.LogWarning($"Save: {message}", this));
            Flow = new GameFlow(ids, SaveStore);
#if UNITY_EDITOR
            Flow.UnlockAll = unlockAllLevelsInEditor;
#endif

            foreach (var panel in panels)
            {
                panel.Bind(this);
                if (panel is PlayPanel play) _playPanel = play;
            }

            boardView.Configure(trackAssets);
            boardView.Interacted += OnBoardInteracted;
            boardView.Completed += OnBoardCompleted;

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
            if (Flow.State == GameState.Play && _playPanel != null) _playPanel.UpdateClock(Flow.Timer.Elapsed);
        }

        private void OnDestroy()
        {
            if (Flow == null) return;
            Flow.StateChanged -= OnStateChanged;
            Flow.LevelStarted -= OnLevelStarted;
            if (trainRunner != null) trainRunner.Finished -= OnTrainFinished;
        }

        private void OnStateChanged(GameState previous, GameState current) => ApplyState(current);

        private void ApplyState(GameState state)
        {
            foreach (var panel in panels)
            {
                var visible = panel.IsVisibleIn(state);
                panel.SetVisible(visible);
                if (visible) panel.Refresh(state);
            }

            boardView.SetInteractable(state == GameState.Play);

            if (state == GameState.TrainRun) trainRunner.Run(boardView.Board);
            else if (trainRunner.IsRunning) trainRunner.Stop();
        }

        private void OnTrainFinished()
        {
            if (Flow.State == GameState.TrainRun) Flow.FinishTrainRun();
        }

        private void OnLevelStarted(int index) => boardView.Load(index >= 0 && index < levels.Count ? levels[index] : null);

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
