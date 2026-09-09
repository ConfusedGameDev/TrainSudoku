using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TrainSudoku.Game
{
    /// <summary>Ordered level list (PRD section 5): locked entries are greyed, completed ones show their best time.</summary>
    public sealed class LevelSelectPanel : PanelBase
    {
        private RectTransform _list;
        private Text _empty;

        public override bool IsVisibleIn(GameState state) => state == GameState.LevelSelect;

        protected override void Build()
        {
            UiBuilder.FullScreen(Root, "Background", UiBuilder.Background);
            var column = UiBuilder.Column(Root, "Content", 24, new RectOffset(72, 72, 96, 72), TextAnchor.UpperCenter);
            UiBuilder.Stretch(column);

            var title = UiBuilder.Label(column, "Title", "Select a level", 80, UiBuilder.TextColor, TextAnchor.MiddleCenter, 120);
            title.fontStyle = FontStyle.Bold;

            _empty = UiBuilder.Label(column, "Empty", "No levels in the collection yet. Author one in Window > TrainSudoku > Level Editor and add it to the LevelCollection.", 34, UiBuilder.Muted, TextAnchor.MiddleCenter, 160);
            _list = UiBuilder.ScrollList(column, "Levels", 18);

            UiBuilder.Button(column, "Back", () => Flow.ShowMainMenu(), AudioCue.UiBack, 110, false);
        }

        public override void Refresh(GameState state)
        {
            UiBuilder.Clear(_list);
            var levels = Game.Levels;
            _empty.gameObject.SetActive(levels.Count == 0);

            for (var i = 0; i < levels.Count; i++)
            {
                var index = i;
                var level = levels[i];
                var unlocked = Flow.IsUnlocked(index);
                var hasBest = Flow.TryGetBestTime(index, out var best);
                var name = level == null ? "(missing level)" : string.IsNullOrEmpty(level.DisplayName) ? level.name : level.DisplayName;

                var button = UiBuilder.Button(_list, $"{index + 1}.  {name}", () => Flow.StartLevel(index), AudioCue.UiConfirm, 140, unlocked);
                button.interactable = unlocked && level != null;

                var status = hasBest ? $"Best {ProgressTracker.FormatTime(best)}" : unlocked ? "New" : "Locked";
                var statusLabel = UiBuilder.Label(button.transform, "Status", status, 34, hasBest ? UiBuilder.Success : UiBuilder.TextColor, TextAnchor.MiddleRight, 140);
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
