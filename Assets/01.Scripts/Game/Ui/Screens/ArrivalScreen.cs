using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Arrival (work order 2): the result board over the solved 3D board. If the run completed the line, the way on
    /// is the network map rather than a next station — that is where a newly opened line is shown opening.
    /// </summary>
    public sealed class ArrivalScreen : UiScreen
    {
        private Label _stationName;
        private Label _thisRun;
        private Label _best;
        private Label _newBest;
        private VisualElement _starsHost;
        private Button _next;

        public override bool IsVisibleIn(GameState state) => state == GameState.Win;

        protected override void BuildTree(VisualElement root)
        {
            root.AddToClassList("screen--transparent");
            root.AddToClassList("scrim");
            root.style.justifyContent = Justify.Center;

            var card = Signage.Column();
            card.AddToClassList("card");
            Signage.Inset(card);
            card.style.paddingTop = 40;
            card.style.paddingBottom = 40;
            card.style.paddingLeft = 40;
            card.style.paddingRight = 40;
            root.Add(card);

            card.Add(Signage.SignageLabel(Signage.Text("arrival.title"), 44, "signage--dim"));
            _stationName = Signage.SignageLabel("", 68);
            card.Add(_stationName);

            var rule = new VisualElement();
            rule.AddToClassList("band__rule");
            rule.AddToClassList(UiShell.LineBackgroundClass);
            Signage.Margins(rule, 0f, 0f, 12f, 28f);
            card.Add(rule);

            _starsHost = Signage.Column();
            Signage.Margins(_starsHost, 0f, 0f, 0f, 24f);
            card.Add(_starsHost);

            card.Add(Readout(Signage.Text("arrival.this_run"), out _thisRun));
            card.Add(Readout(Signage.Text("arrival.best"), out _best));

            _newBest = Signage.SignageLabel(Signage.Text("arrival.new_best"), 34);
            _newBest.style.color = Palette.Success;
            Signage.Margins(_newBest, 0f, 0f, 8f, 0f);
            card.Add(_newBest);

            _next = Signage.LocalizedButton("arrival.next", AudioCue.UiConfirm, () => Flow.NextLevel(), true);
            Signage.Margins(_next, 0f, 0f, 32f, 18f);
            card.Add(_next);

            var retry = Signage.LocalizedButton("arrival.retry", AudioCue.UiClick, () => Flow.Retry());
            Signage.Margins(retry, 0f, 0f, 0f, 18f);
            card.Add(retry);

            card.Add(Signage.LocalizedButton("arrival.map", AudioCue.MapOpen, () => Flow.ShowLevelSelect()));
        }

        private static VisualElement Readout(string caption, out Label value)
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
            var level = Game.CurrentLevel;
            _stationName.text = level != null ? level.DisplayName : "";

            var result = Flow.LastResult;
            _thisRun.text = result.HasValue ? ProgressTracker.FormatTime(result.Value.Time) : "—";
            _best.text = result.HasValue ? ProgressTracker.FormatTime(result.Value.BestTime) : "—";
            _newBest.style.display = result.HasValue && result.Value.IsNewBest ? DisplayStyle.Flex : DisplayStyle.None;

            _starsHost.Clear();
            _starsHost.Add(Signage.Stars(result.HasValue ? result.Value.Stars : 0, 56));
            if (result.HasValue && result.Value.IsNewBestStars) AudioCuePlayer.Play(AudioCue.StarAwarded);

            // At a terminus there is no next station: NextLevel routes to the network instead, so say so.
            _next.text = Signage.Text(Flow.IsLineComplete ? "arrival.to_network" : "arrival.next");
        }
    }
}
