using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TrainSudoku.Game
{
    /// <summary>This time, best time, new-best flag, and Next / Retry / Menu (PRD section 5).</summary>
    public sealed class WinPanel : PanelBase
    {
        [SerializeField] private Text time;
        [SerializeField] private Text best;
        [SerializeField] private Text newBest;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;

        public override bool IsVisibleIn(GameState state) => state == GameState.Win;

        protected override void BuildWidgets()
        {
            UiBuilder.FullScreen(Root, "Dim", UiBuilder.Dim);
            var card = UiBuilder.CenteredCard(Root, "Card", 800, 20);
            var title = UiBuilder.Label(card, "Title", "Level complete", 72, UiBuilder.TextColor, TextAnchor.MiddleCenter, 110);
            title.fontStyle = FontStyle.Bold;
            time = UiBuilder.Label(card, "Time", "", 48, UiBuilder.TextColor, TextAnchor.MiddleCenter, 70);
            best = UiBuilder.Label(card, "Best", "", 40, UiBuilder.Muted, TextAnchor.MiddleCenter, 60);
            newBest = UiBuilder.Label(card, "New Best", "New best!", 44, UiBuilder.Success, TextAnchor.MiddleCenter, 70);
            newBest.fontStyle = FontStyle.Bold;

            nextButton = UiBuilder.Button(card, "Next", 130);
            retryButton = UiBuilder.Button(card, "Retry", 110, false);
            menuButton = UiBuilder.Button(card, "Menu", 110, false);
        }

        protected override void Wire()
        {
            UiBuilder.Wire(nextButton, AudioCue.UiConfirm, () => Flow.NextLevel());
            UiBuilder.Wire(retryButton, AudioCue.UiClick, () => Flow.Retry());
            UiBuilder.Wire(menuButton, AudioCue.UiBack, () => Flow.ShowLevelSelect());
        }

        public override void Refresh(GameState state)
        {
            var result = Flow.LastResult;
            time.text = result.HasValue ? $"Time  {ProgressTracker.FormatTime(result.Value.Time)}" : "";
            best.text = result.HasValue ? $"Best  {ProgressTracker.FormatTime(result.Value.BestTime)}" : "";
            newBest.gameObject.SetActive(result.HasValue && result.Value.IsNewBest);
            nextButton.interactable = Flow.HasNextLevel;
        }
    }
}
