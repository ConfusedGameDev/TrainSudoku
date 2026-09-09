using TrainSudoku.Core;
using UnityEngine;

namespace TrainSudoku.Game
{
    public sealed class PausePanel : PanelBase
    {
        public override bool IsVisibleIn(GameState state) => state == GameState.Pause;

        protected override void Build()
        {
            UiBuilder.FullScreen(Root, "Dim", UiBuilder.Dim);
            var card = UiBuilder.CenteredCard(Root, "Card", 760, 24);
            var title = UiBuilder.Label(card, "Title", "Paused", 72, UiBuilder.TextColor, TextAnchor.MiddleCenter, 110);
            title.fontStyle = FontStyle.Bold;
            UiBuilder.Button(card, "Resume", () => Flow.ResumeGame(), AudioCue.UiConfirm, 130);
            UiBuilder.Button(card, "Retry", () => Flow.Retry(), AudioCue.UiClick, 110, false);
            UiBuilder.Button(card, "Exit", () => Flow.ShowLevelSelect(), AudioCue.UiBack, 110, false);
        }
    }
}
