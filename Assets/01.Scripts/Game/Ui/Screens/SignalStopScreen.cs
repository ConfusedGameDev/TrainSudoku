using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The pause screen (work order 2.1). Direction 2 never drew it and no art is coming, so it is <b>derived</b>:
    /// the Arrival card's chrome over an ink scrim, with the same two-column read-out and three stacked buttons.
    /// </summary>
    public sealed class SignalStopScreen : UiScreen
    {
        private Label _elapsed;
        private Label _rails;

        public override bool IsVisibleIn(GameState state) => state == GameState.Pause;

        protected override void BuildTree(VisualElement root)
        {
            root.AddToClassList("screen--transparent");
            root.AddToClassList("scrim");          // the board keeps rendering underneath
            root.style.justifyContent = Justify.Center;

            var card = Signage.Column();
            card.AddToClassList("card");
            Signage.Inset(card);
            card.style.paddingTop = 40;
            card.style.paddingBottom = 40;
            card.style.paddingLeft = 40;
            card.style.paddingRight = 40;
            root.Add(card);

            card.Add(Signage.SignageLabel(Signage.Text("pause.title"), 60));

            var rule = new VisualElement();
            rule.AddToClassList("band__rule");
            rule.AddToClassList(UiShell.LineBackgroundClass);
            Signage.Margins(rule, 0f, 0f, 12f, 28f);
            card.Add(rule);

            card.Add(ReadoutRow(Signage.Text("pause.elapsed"), out _elapsed));
            card.Add(ReadoutRow(Signage.Text("pause.rails_laid"), out _rails));

            var resume = Signage.LocalizedButton("pause.resume", AudioCue.UiConfirm, () => Flow.ResumeGame(), true);
            Signage.Margins(resume, 0f, 0f, 32f, 18f);
            card.Add(resume);

            var retry = Signage.LocalizedButton("pause.start_over", AudioCue.UiClick, () => Flow.Retry());
            Signage.Margins(retry, 0f, 0f, 0f, 18f);
            card.Add(retry);

            card.Add(Signage.LocalizedButton("pause.back_to_map", AudioCue.UiBack, () => Flow.ShowLevelSelect()));

#if UNITY_EDITOR
            var debug = Signage.Button("Debug: win", AudioCue.UiClick, () => Game.DebugCompleteLevel());
            Signage.Margins(debug, 0f, 0f, 24f, 0f);
            card.Add(debug);
#endif
        }

        /// <summary>Label on the left, value on the right — the same two-column form Arrival uses.</summary>
        private static VisualElement ReadoutRow(string caption, out Label value)
        {
            var row = Signage.Row();
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 10;

            row.Add(Signage.BodyLabel(caption, 32, "body--dim"));
            value = Signage.Numerals("", 40);
            row.Add(value);
            return row;
        }

        protected override void Wire()
        {
        }

        public override void Refresh(GameState state)
        {
            _elapsed.text = ProgressTracker.FormatTime(Flow.Timer.Elapsed);
            _rails.text = Game.Board != null ? Game.Board.PieceCount.ToString() : "0";
        }
    }
}
