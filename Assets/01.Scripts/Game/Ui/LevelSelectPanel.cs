using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Ordered level list (PRD section 5): locked entries are greyed, completed ones show their best time and a level
    /// left unfinished offers to continue.
    /// </summary>
    public sealed class LevelSelectPanel : PanelBase
    {
        [SerializeField] private RectTransform list;
        [SerializeField] private Text empty;
        [SerializeField] private Button backButton;

        public override bool IsVisibleIn(GameState state) => state == GameState.LevelSelect;

        protected override void BuildWidgets()
        {
            UiBuilder.FullScreen(Root, "Background", UiBuilder.Background);
            var column = UiBuilder.Column(Root, "Content", 24, new RectOffset(72, 72, 96, 72), TextAnchor.UpperCenter);
            UiBuilder.Stretch(column);

            var title = UiBuilder.Label(column, "Title", "Select a level", 80, UiBuilder.TextColor, TextAnchor.MiddleCenter, 120);
            title.fontStyle = FontStyle.Bold;

            empty = UiBuilder.Label(column, "Empty", "No levels in the collection yet. Author one in Window > TrainSudoku > Level Editor and add it to the LevelCollection.", 34, UiBuilder.Muted, TextAnchor.MiddleCenter, 160);
            list = UiBuilder.ScrollList(column, "Levels", 18);

            backButton = UiBuilder.Button(column, "Back", 110, false);
        }

        protected override void Wire()
        {
            UiBuilder.Wire(backButton, AudioCue.UiBack, () => Flow.ShowMainMenu());
        }

        public override void Refresh(GameState state)
        {
            UiBuilder.Clear(list);
            var levels = Game.Levels;
            empty.gameObject.SetActive(levels.Count == 0);

            for (var i = 0; i < levels.Count; i++)
            {
                var index = i;
                var level = levels[i];
                var unlocked = Flow.IsUnlocked(index);
                var hasBest = Flow.TryGetBestTime(index, out var best);
                var name = level == null ? "(missing level)" : string.IsNullOrEmpty(level.DisplayName) ? level.name : level.DisplayName;

                var button = UiBuilder.Button(list, $"{index + 1}.  {name}", 140, unlocked);
                UiBuilder.Wire(button, AudioCue.UiConfirm, () => Flow.StartLevel(index));
                button.interactable = unlocked && level != null;

                var inProgress = Flow.HasInProgress(index);
                var status = inProgress ? "Continue" : hasBest ? $"Best {ProgressTracker.FormatTime(best)}" : unlocked ? "New" : "Locked";
                var statusColor = inProgress ? UiBuilder.Accent : hasBest ? UiBuilder.Success : UiBuilder.TextColor;
                var statusLabel = UiBuilder.Label(button.transform, "Status", status, 34, statusColor, TextAnchor.MiddleRight, 140);
                var rect = (RectTransform)statusLabel.transform;
                UiBuilder.Stretch(rect);
                rect.offsetMax = new Vector2(-36f, 0f);

                var label = button.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                ((RectTransform)label.transform).offsetMin = new Vector2(36f, 0f);
                if (!unlocked) label.color = UiBuilder.Muted;
            }
        }
    }
}
