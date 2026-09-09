using System.Collections.Generic;
using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Scene entry point. Owns the <see cref="GameFlow"/>, builds the UI and the board view, and keeps them in step:
    /// panels show according to the flow state, board events feed the flow, and the clock ticks every frame.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private LevelCollection levels = null;
        [SerializeField] private InputActionAsset inputActions = null;
        [SerializeField] private AudioCueLibrary audioCues = null;

        [Tooltip("Editor only: every level counts as unlocked in Level Select (PRD section 5).")]
        [SerializeField] private bool unlockAllLevelsInEditor = false;

        private readonly List<PanelBase> _panels = new List<PanelBase>();
        private PlayPanel _playPanel;

        public GameFlow Flow { get; private set; }
        public LevelCollection Levels => levels;
        public IBoardView Board { get; private set; }

        public LevelDefinition CurrentLevel =>
            Flow != null && Flow.CurrentLevelIndex >= 0 && Flow.CurrentLevelIndex < levels.Count ? levels[Flow.CurrentLevelIndex] : null;

        private void Awake()
        {
            if (levels == null)
            {
                Debug.LogWarning("GameManager has no LevelCollection assigned; Level Select will be empty.", this);
                levels = ScriptableObject.CreateInstance<LevelCollection>();
            }

            var ids = new List<string>(levels.Count);
            foreach (var level in levels.Levels) ids.Add(level != null ? level.Id : "");

            // M9 replaces the in-memory store with the JSON file under persistentDataPath.
            Flow = new GameFlow(ids, new InMemorySaveStore());
#if UNITY_EDITOR
            Flow.UnlockAll = unlockAllLevelsInEditor;
#endif

            AudioCuePlayer.Create(transform, audioCues);

            var canvas = UiBuilder.CreateCanvas("UI", inputActions);
            canvas.transform.SetParent(transform, false);

            AddPanel<MainMenuPanel>(canvas.transform, "Main Menu");
            AddPanel<LevelSelectPanel>(canvas.transform, "Level Select");
            _playPanel = AddPanel<PlayPanel>(canvas.transform, "Play");
            AddPanel<PausePanel>(canvas.transform, "Pause");
            AddPanel<TrainRunPanel>(canvas.transform, "Train Run");
            AddPanel<WinPanel>(canvas.transform, "Win");

            var board = StubBoardView.Create(_playPanel.BoardArea);
            board.Interacted += OnBoardInteracted;
            board.Completed += OnBoardCompleted;
            Board = board;

            Flow.StateChanged += OnStateChanged;
            Flow.LevelStarted += OnLevelStarted;
            ApplyState(Flow.State);
        }

        private T AddPanel<T>(Transform canvas, string name) where T : PanelBase
        {
            var rect = UiBuilder.Stretch(UiBuilder.Rect(canvas, name));
            var panel = rect.gameObject.AddComponent<T>();
            panel.Initialize(this);
            _panels.Add(panel);
            return panel;
        }

        private void Update()
        {
            Flow.Tick(Time.deltaTime);
            if (Flow.State == GameState.Play) _playPanel.UpdateClock(Flow.Timer.Elapsed);
        }

        private void OnDestroy()
        {
            if (Flow == null) return;
            Flow.StateChanged -= OnStateChanged;
            Flow.LevelStarted -= OnLevelStarted;
        }

        private void OnStateChanged(GameState previous, GameState current) => ApplyState(current);

        private void ApplyState(GameState state)
        {
            foreach (var panel in _panels)
            {
                var visible = panel.IsVisibleIn(state);
                panel.SetVisible(visible);
                if (visible) panel.Refresh(state);
            }

            Board?.SetInteractable(state == GameState.Play);
        }

        private void OnLevelStarted(int index) => Board.Load(index >= 0 && index < levels.Count ? levels[index] : null);

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
