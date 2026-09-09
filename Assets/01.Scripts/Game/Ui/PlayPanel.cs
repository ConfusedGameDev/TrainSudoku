using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TrainSudoku.Game
{
    /// <summary>
    /// HUD over the 3D board (PRD section 5): a small pause button in the top-left corner, the level name at the top
    /// centre and the clock top-right. Everything else is transparent so the board is the main thing on screen. Stays
    /// visible under the pause, train run and win overlays.
    /// </summary>
    public sealed class PlayPanel : PanelBase
    {
        private const float Margin = 36f;
        private const float BarHeight = 96f;

        [SerializeField] private Text levelName;
        [SerializeField] private Text clock;
        [SerializeField] private Button pauseButton;

        public override bool IsVisibleIn(GameState state) =>
            state == GameState.Play || state == GameState.Pause || state == GameState.TrainRun || state == GameState.Win;

        protected override void BuildWidgets()
        {
            pauseButton = UiBuilder.Button(Root, "||", BarHeight, false, 44);
            UiBuilder.Anchor((RectTransform)pauseButton.transform, new Vector2(0f, 1f), new Vector2(BarHeight, BarHeight), new Vector2(Margin, -Margin));
            pauseButton.name = "Pause";

            levelName = UiBuilder.Label(Root, "Level", "", 40, UiBuilder.TextColor, TextAnchor.MiddleCenter, BarHeight);
            UiBuilder.Anchor((RectTransform)levelName.transform, new Vector2(0.5f, 1f), new Vector2(560f, BarHeight), new Vector2(0f, -Margin));

            clock = UiBuilder.Label(Root, "Clock", "0:00.0", 48, UiBuilder.Accent, TextAnchor.MiddleRight, BarHeight);
            UiBuilder.Anchor((RectTransform)clock.transform, new Vector2(1f, 1f), new Vector2(240f, BarHeight), new Vector2(-Margin, -Margin));
            clock.fontStyle = FontStyle.Bold;
        }

        protected override void Wire()
        {
            UiBuilder.Wire(pauseButton, AudioCue.UiClick, () => Flow.PauseGame());
        }

        public override void Refresh(GameState state)
        {
            var level = Game.CurrentLevel;
            levelName.text = level == null ? "" : string.IsNullOrEmpty(level.DisplayName) ? level.name : level.DisplayName;
            pauseButton.interactable = state == GameState.Play;
            UpdateClock(Flow.Timer.Elapsed);
        }

        public void UpdateClock(double seconds) => clock.text = ProgressTracker.FormatTime(seconds);
    }
}
