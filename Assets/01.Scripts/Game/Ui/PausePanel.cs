using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TrainSudoku.Game
{
    public sealed class PausePanel : PanelBase
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button debugWinButton;

        public override bool IsVisibleIn(GameState state) => state == GameState.Pause;

        protected override void BuildWidgets()
        {
            UiBuilder.FullScreen(Root, "Dim", UiBuilder.Dim);
            var card = UiBuilder.CenteredCard(Root, "Card", 760, 24);
            var title = UiBuilder.Label(card, "Title", "Paused", 72, UiBuilder.TextColor, TextAnchor.MiddleCenter, 110);
            title.fontStyle = FontStyle.Bold;
            resumeButton = UiBuilder.Button(card, "Resume", 130);
            retryButton = UiBuilder.Button(card, "Retry", 110, false);
            exitButton = UiBuilder.Button(card, "Exit", 110, false);
            debugWinButton = UiBuilder.Button(card, "Debug: win", 90, false, 30);
        }

        protected override void Wire()
        {
            UiBuilder.Wire(resumeButton, AudioCue.UiConfirm, () => Flow.ResumeGame());
            UiBuilder.Wire(retryButton, AudioCue.UiClick, () => Flow.Retry());
            UiBuilder.Wire(exitButton, AudioCue.UiBack, () => Flow.ShowLevelSelect());
            UiBuilder.Wire(debugWinButton, AudioCue.UiConfirm, () => Game.DebugCompleteLevel());
            // Editor-only shortcut to the win flow for testing the screens.
            debugWinButton.gameObject.SetActive(Application.isEditor);
        }
    }
}
