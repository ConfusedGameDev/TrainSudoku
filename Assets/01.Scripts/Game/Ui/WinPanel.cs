using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TrainSudoku.Game
{
    /// <summary>This time, best time, new-best flag, and Next / Retry / Menu (PRD section 5).</summary>
    public sealed class WinPanel : PanelBase
    {
        private Text _time;
        private Text _best;
        private Text _newBest;
        private Button _next;

        public override bool IsVisibleIn(GameState state) => state == GameState.Win;

        protected override void Build()
        {
            UiBuilder.FullScreen(Root, "Dim", UiBuilder.Dim);
            var card = UiBuilder.CenteredCard(Root, "Card", 800, 20);
            var title = UiBuilder.Label(card, "Title", "Level complete", 72, UiBuilder.TextColor, TextAnchor.MiddleCenter, 110);
            title.fontStyle = FontStyle.Bold;
            _time = UiBuilder.Label(card, "Time", "", 48, UiBuilder.TextColor, TextAnchor.MiddleCenter, 70);
            _best = UiBuilder.Label(card, "Best", "", 40, UiBuilder.Muted, TextAnchor.MiddleCenter, 60);
            _newBest = UiBuilder.Label(card, "New Best", "New best!", 44, UiBuilder.Success, TextAnchor.MiddleCenter, 70);
            _newBest.fontStyle = FontStyle.Bold;

            _next = UiBuilder.Button(card, "Next", () => Flow.NextLevel(), AudioCue.UiConfirm, 130);
            UiBuilder.Button(card, "Retry", () => Flow.Retry(), AudioCue.UiClick, 110, false);
            UiBuilder.Button(card, "Menu", () => Flow.ShowLevelSelect(), AudioCue.UiBack, 110, false);
        }

        public override void Refresh(GameState state)
        {
            var result = Flow.LastResult;
            _time.text = result.HasValue ? $"Time  {ProgressTracker.FormatTime(result.Value.Time)}" : "";
            _best.text = result.HasValue ? $"Best  {ProgressTracker.FormatTime(result.Value.BestTime)}" : "";
            _newBest.gameObject.SetActive(result.HasValue && result.Value.IsNewBest);
            _next.interactable = Flow.HasNextLevel;
        }
    }
}
