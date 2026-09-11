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
    /// 0.96 and back to 1.0 over 380 ms — the stars follow it in 120 ms apart, each with its cue, and then the
    /// verdict is stamped on: down from 2.4 and off square, under-size on impact, and a damped rattle to a stop.
    /// The buttons are live from the first frame, so none of it holds the player up.
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

        /// <summary>
        /// The verdict stamp. It comes down big and off-axis, hits under-size, settles, then rattles: a rubber stamp
        /// pressed onto a ticket does not stop dead, and the shake is what sells the press as an impact rather than
        /// as a fade-in.
        /// </summary>
        private const float StampPressDuration = 0.26f;
        private const float StampPressFrom = 2.4f;
        private const float StampPressHit = 0.88f;

        /// <summary>Where the press bottoms out and hands over to the settle, as a share of its duration.</summary>
        private const float StampPressImpact = 0.62f;

        /// <summary>The rattle after the hit: how long it lasts, how far it swings, and how many times.</summary>
        private const float StampShakeDuration = 0.34f;
        private const float StampShakeDegrees = 3.2f;
        private const float StampShakeCycles = 2.5f;

        /// <summary>How far off square the stamp finally rests, the way a hand-pressed one never lands true.</summary>
        private const float StampRest = -4f;
        private const float StampEntryTilt = -22f;

        private Label _title;
        private Label _stationName;
        private Label _thisRun;
        private Label _best;
        private Label _newBest;
        private VisualElement _starsHost;
        private VisualElement _stamp;
        private Label _stampText;
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

            // Stars on the left, the verdict stamped on the right, the way the artboard reads the result out.
            var verdict = Signage.Row();
            Signage.Margins(verdict, 0f, 0f, 0f, 24f);
            card.Add(verdict);

            _starsHost = Signage.Column();
            verdict.Add(_starsHost);
            verdict.Add(Signage.Spacer());
            verdict.Add(BuildStamp());

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
        /// The verdict stamp: a bordered box held off square, carrying how the run went. Built once; only its text
        /// changes, because the three messages are the same shape.
        /// </summary>
        private VisualElement BuildStamp()
        {
            _stamp = Signage.Column();
            _stamp.style.alignItems = Align.Center;
            _stamp.style.justifyContent = Justify.Center;
            _stamp.style.flexShrink = 0;
            _stamp.style.paddingTop = 12;
            _stamp.style.paddingBottom = 12;
            _stamp.style.paddingLeft = 26;
            _stamp.style.paddingRight = 26;
            _stamp.style.backgroundColor = Palette.Paper;
            _stamp.style.borderTopWidth = _stamp.style.borderRightWidth =
                _stamp.style.borderBottomWidth = _stamp.style.borderLeftWidth = 3;
            _stamp.AddToClassList(UiShell.LineBorderClass);
            _stamp.style.rotate = new Rotate(new Angle(StampRest, AngleUnit.Degree));

            _stampText = Signage.SignageLabel("", 44, UiShell.LineTextClass);
            _stamp.Add(_stampText);
            return _stamp;
        }

        /// <summary>
        /// How the run reads on the stamp. Three messages for the three ratings a finished run can carry — the timer
        /// never awards none, so there is no fourth.
        /// </summary>
        private static string StampKey(int stars) =>
            stars >= 3 ? "arrival.on_time" : stars == 2 ? "arrival.slight_delay" : "arrival.delayed";

        /// <summary>
        /// The stamp, then the stars. One tween drives the whole beat rather than a chain of delayed calls, so a
        /// second arrival landing on the first cannot leave a star mid-pop.
        /// </summary>
        /// <summary>One fanfare per star count. A run always earns at least one star, so there is no silent case.</summary>
        private static AudioCue FanfareFor(int stars) => stars >= 3 ? AudioCue.WinFanfareThree
            : stars == 2 ? AudioCue.WinFanfareTwo
            : AudioCue.WinFanfareOne;

        private void PlayArrival(VisualElement starRow, int earned, bool cueEachStar)
        {
            // At the top of the beat, so the fanfare underlays the whole stamp-and-stars sequence rather than landing
            // after it. The per-star pops still ride on top.
            AudioCuePlayer.Play(FanfareFor(earned));

            var icons = new List<VisualElement>(starRow.Children());
            foreach (var icon in icons)
            {
                icon.style.opacity = 0f;
                icon.style.scale = new Scale(Vector3.zero);
            }

            var cued = new bool[icons.Count];

            // The stamp comes down after the last star, so the beat reads title, rating, verdict.
            var stampAt = StampDuration + icons.Count * StarInterval + StarDuration;
            var total = stampAt + StampPressDuration + StampShakeDuration;
            var struck = false;

            _stamp.style.opacity = 0f;
            _stamp.style.scale = new Scale(Vector3.one * StampPressFrom);
            _stamp.style.rotate = new Rotate(new Angle(StampEntryTilt, AngleUnit.Degree));

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

                var press = Motion.Stage(elapsed, stampAt, StampPressDuration);
                if (press > 0f)
                {
                    _stamp.style.opacity = Mathf.Clamp01(press * 3f);
                    _stamp.style.scale = new Scale(Vector3.one * Press(press));
                    _stamp.style.rotate = new Rotate(new Angle(
                        Mathf.Lerp(StampEntryTilt, StampRest, Motion.EaseIn(Mathf.Min(press / StampPressImpact, 1f))),
                        AngleUnit.Degree));

                    // One hit, at the moment it bottoms out rather than when the tween starts.
                    if (!struck && press >= StampPressImpact)
                    {
                        struck = true;
                        AudioCuePlayer.Play(AudioCue.UiConfirm);
                    }
                }

                var shake = Motion.Stage(elapsed, stampAt + StampPressDuration, StampShakeDuration);
                if (shake > 0f) _stamp.style.rotate = new Rotate(new Angle(StampRest + Rattle(shake), AngleUnit.Degree));
            }, () =>
            {
                _title.style.scale = new Scale(Vector3.one);
                foreach (var icon in icons)
                {
                    icon.style.opacity = 1f;
                    icon.style.scale = new Scale(Vector3.one);
                }

                _stamp.style.opacity = 1f;
                _stamp.style.scale = new Scale(Vector3.one);
                _stamp.style.rotate = new Rotate(new Angle(StampRest, AngleUnit.Degree));
            });
        }

        /// <summary>
        /// The press: down fast from <see cref="StampPressFrom"/> past its resting size to
        /// <see cref="StampPressHit"/>, then back out to rest. Easing in on the way down is what makes it read as
        /// something falling onto the screen rather than something growing on it.
        /// </summary>
        private static float Press(float t)
        {
            if (t < StampPressImpact) return Mathf.Lerp(StampPressFrom, StampPressHit, Motion.EaseIn(t / StampPressImpact));
            return Mathf.Lerp(StampPressHit, 1f, Motion.EaseOut((t - StampPressImpact) / (1f - StampPressImpact)));
        }

        /// <summary>A damped swing either side of rest, so the stamp rattles to a stop instead of stopping dead.</summary>
        private static float Rattle(float t) =>
            Mathf.Sin(t * StampShakeCycles * 2f * Mathf.PI) * StampShakeDegrees * (1f - t) * (1f - t);

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
            _stampText.text = Signage.Text(StampKey(stars));
            _starsHost.Clear();
            var row = Signage.Stars(stars, 56);
            _starsHost.Add(row);
            PlayArrival(row, stars, result.HasValue && result.Value.IsNewBestStars);

            // At a terminus there is no next station: NextLevel routes to the network instead, so say so.
            _next.text = Signage.Text(Flow.IsLineComplete ? "arrival.to_network" : "arrival.next");
        }
    }
}
