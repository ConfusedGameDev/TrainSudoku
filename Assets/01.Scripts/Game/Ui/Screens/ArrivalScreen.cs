using System.Collections.Generic;
using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Arrival (work order 2): the result board over the solved 3D board. If the run completed the line, the way on
    /// is the network map rather than a next station — that is where a newly opened line is shown opening.
    /// </summary>
    /// <remarks>
    /// The arrival beat of work order 9 lives here: the title lands like a stamp on a ticket — 1.6 down through
    /// 0.96 and back to 1.0 over 380 ms — and the stars follow it in 120 ms apart, each with its cue. The buttons
    /// are live from the first frame, so none of it holds the player up.
    /// </remarks>
    public sealed class ArrivalScreen : UiScreen
    {
        private const float StampDuration = 0.38f;
        private const float StampFrom = 1.6f;
        private const float StampSettle = 0.96f;

        /// <summary>Where the stamp hands over to the settle, as a share of its duration.</summary>
        private const float StampImpact = 0.7f;

        private const float StarInterval = 0.12f;
        private const float StarDuration = 0.2f;
        private const float StarPop = 1.25f;

        private Label _title;
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

            _title = Signage.SignageLabel(Signage.Text("arrival.title"), 44, "signage--dim");
            _title.style.transformOrigin = new TransformOrigin(Length.Percent(0f), Length.Percent(50f));
            card.Add(_title);
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

        /// <summary>
        /// The stamp, then the stars. One tween drives the whole beat rather than a chain of delayed calls, so a
        /// second arrival landing on the first cannot leave a star mid-pop.
        /// </summary>
        private void PlayArrival(VisualElement starRow, int earned, bool cueEachStar)
        {
            var icons = new List<VisualElement>(starRow.Children());
            foreach (var icon in icons)
            {
                icon.style.opacity = 0f;
                icon.style.scale = new Scale(Vector3.zero);
            }

            var cued = new bool[icons.Count];
            var total = StampDuration + icons.Count * StarInterval + StarDuration;

            Motion.Play(Root, total, t =>
            {
                var elapsed = t * total;
                _title.style.scale = new Scale(Vector3.one * Stamp(Motion.Stage(elapsed, 0f, StampDuration)));

                for (var i = 0; i < icons.Count; i++)
                {
                    var u = Motion.Stage(elapsed, StampDuration + i * StarInterval, StarDuration);
                    icons[i].style.opacity = u <= 0f ? 0f : 1f;
                    icons[i].style.scale = new Scale(Vector3.one * (u <= 0f ? 0f : Motion.Pop(u, StarPop)));
                    if (u <= 0f || cued[i]) continue;
                    cued[i] = true;
                    if (cueEachStar && i < earned) AudioCuePlayer.Play(AudioCue.StarAwarded);
                }
            }, () =>
            {
                _title.style.scale = new Scale(Vector3.one);
                foreach (var icon in icons)
                {
                    icon.style.opacity = 1f;
                    icon.style.scale = new Scale(Vector3.one);
                }
            });
        }

        /// <summary>Down from <see cref="StampFrom"/> to <see cref="StampSettle"/>, then back up to rest.</summary>
        private static float Stamp(float t)
        {
            if (t < StampImpact) return Mathf.Lerp(StampFrom, StampSettle, Motion.EaseOut(t / StampImpact));
            return Mathf.Lerp(StampSettle, 1f, Motion.EaseOut((t - StampImpact) / (1f - StampImpact)));
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

            var stars = result.HasValue ? result.Value.Stars : 0;
            _starsHost.Clear();
            var row = Signage.Stars(stars, 56);
            _starsHost.Add(row);
            PlayArrival(row, stars, result.HasValue && result.Value.IsNewBestStars);

            // At a terminus there is no next station: NextLevel routes to the network instead, so say so.
            _next.text = Signage.Text(Flow.IsLineComplete ? "arrival.to_network" : "arrival.next");
        }
    }
}
