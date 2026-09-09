using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TrainSudoku.Game
{
    /// <summary>Board, clues, timer and pause button (PRD section 5). Stays visible under the pause, train run and win overlays.</summary>
    public sealed class PlayPanel : PanelBase
    {
        private Text _levelName;
        private Text _clock;
        private Button _pauseButton;

        /// <summary>Where the board view lives.</summary>
        public RectTransform BoardArea { get; private set; }

        public override bool IsVisibleIn(GameState state) =>
            state == GameState.Play || state == GameState.Pause || state == GameState.TrainRun || state == GameState.Win;

        protected override void Build()
        {
            UiBuilder.FullScreen(Root, "Background", UiBuilder.Background);
            var column = UiBuilder.Column(Root, "Content", 0, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
            UiBuilder.Stretch(column);

            var bar = UiBuilder.Row(column, "Top Bar", 24, new RectOffset(36, 36, 48, 16), 180);
            _pauseButton = UiBuilder.Button(bar, "Pause", () => Flow.PauseGame(), AudioCue.UiClick, 100, false);
            var pauseElement = _pauseButton.GetComponent<LayoutElement>();
            pauseElement.preferredWidth = 220;
            pauseElement.flexibleWidth = 0;

            _levelName = UiBuilder.Label(bar, "Level", "", 44, UiBuilder.TextColor, TextAnchor.MiddleCenter, 100);
            _levelName.GetComponent<LayoutElement>().flexibleWidth = 1;

            _clock = UiBuilder.Label(bar, "Clock", "0:00.0", 52, UiBuilder.Accent, TextAnchor.MiddleRight, 100);
            var clockElement = _clock.GetComponent<LayoutElement>();
            clockElement.preferredWidth = 220;
            clockElement.flexibleWidth = 0;
            _clock.fontStyle = FontStyle.Bold;

            BoardArea = UiBuilder.Rect(column, "Board Area");
            var boardElement = BoardArea.gameObject.AddComponent<LayoutElement>();
            boardElement.flexibleHeight = 1;
        }

        public override void Refresh(GameState state)
        {
            var level = Game.CurrentLevel;
            _levelName.text = level == null ? "" : string.IsNullOrEmpty(level.DisplayName) ? level.name : level.DisplayName;
            _pauseButton.interactable = state == GameState.Play;
            UpdateClock(Flow.Timer.Elapsed);
        }

        public void UpdateClock(double seconds) => _clock.text = ProgressTracker.FormatTime(seconds);
    }
}
