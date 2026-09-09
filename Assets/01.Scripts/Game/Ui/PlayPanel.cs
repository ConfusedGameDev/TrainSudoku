using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TrainSudoku.Game
{
    /// <summary>
    /// HUD over the 3D board (PRD section 5): level name, timer and pause button in a top bar. The rest of the screen is
    /// transparent so the camera view shows through. Stays visible under the pause, train run and win overlays.
    /// </summary>
    public sealed class PlayPanel : PanelBase
    {
        private Text _levelName;
        private Text _clock;
        private Button _pauseButton;
        private Button _debugWinButton;

        public override bool IsVisibleIn(GameState state) =>
            state == GameState.Play || state == GameState.Pause || state == GameState.TrainRun || state == GameState.Win;

        protected override void Build()
        {
            var column = UiBuilder.Column(Root, "Content", 0, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
            UiBuilder.Stretch(column);

            var bar = UiBuilder.Row(column, "Top Bar", 24, new RectOffset(36, 36, 48, 16), 180);
            bar.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

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

            // Empty flexible area: the board is rendered by the camera behind the canvas.
            UiBuilder.Spacer(column);

            if (Application.isEditor)
            {
                // Editor-only shortcut to reach the win flow before M7 hooks up the validator.
                var debugRow = UiBuilder.Row(column, "Debug", 0, new RectOffset(36, 36, 0, 36), 90);
                UiBuilder.Spacer(debugRow, 0f, 1f);
                _debugWinButton = UiBuilder.Button(debugRow, "Debug: win", () => Game.DebugCompleteLevel(), AudioCue.UiConfirm, 70, false);
                var element = _debugWinButton.GetComponent<LayoutElement>();
                element.preferredWidth = 260;
                element.flexibleWidth = 0;
            }
        }

        public override void Refresh(GameState state)
        {
            var level = Game.CurrentLevel;
            _levelName.text = level == null ? "" : string.IsNullOrEmpty(level.DisplayName) ? level.name : level.DisplayName;
            _pauseButton.interactable = state == GameState.Play;
            if (_debugWinButton != null) _debugWinButton.gameObject.SetActive(state == GameState.Play);
            UpdateClock(Flow.Timer.Elapsed);
        }

        public void UpdateClock(double seconds) => _clock.text = ProgressTracker.FormatTime(seconds);
    }
}
