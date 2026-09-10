using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The frame around the 3D board (work order 2): a sign bar along the top, an LED strip along the bottom, and
    /// nothing at all in between — the middle is camera view.
    /// </summary>
    /// <remarks>
    /// It stays visible through Pause, the train run and Arrival, so the board never loses its frame while an overlay
    /// is up. <b>No UI code here draws a tile, a piece or a train.</b>
    /// </remarks>
    public sealed class PlayScreen : UiScreen
    {
        /// <summary>Sign bar and LED strip heights, in reference pixels. M17 feeds these to the camera.</summary>
        public const float TopBarHeight = 250f;
        public const float BottomBarHeight = 150f;

        private StationRoundel _roundel;
        private Label _stationName;
        private Label _clock;
        private Button _pause;
        private LedStrip _led;

        public override bool IsVisibleIn(GameState state) =>
            state == GameState.Play || state == GameState.Pause ||
            state == GameState.TrainRun || state == GameState.Win;

        protected override void BuildTree(VisualElement root)
        {
            root.AddToClassList("screen--transparent");

            var bar = Signage.Row();
            bar.style.height = TopBarHeight;
            bar.style.flexShrink = 0;
            bar.style.backgroundColor = Palette.Ink;
            bar.style.paddingLeft = 40;
            bar.style.paddingRight = 40;
            root.Add(bar);

            _pause = Signage.IconButton(Icons.Pause(), AudioCue.UiClick, () => Flow.PauseGame());
            _pause.style.marginRight = 28;
            bar.Add(_pause);

            _roundel = new StationRoundel { Number = 1 };
            _roundel.style.width = 84;
            _roundel.style.height = 84;
            _roundel.style.marginRight = 24;
            bar.Add(_roundel);

            _stationName = Signage.SignageLabel("", 46, "signage--onDark");
            _stationName.style.flexGrow = 1f;
            _stationName.style.flexShrink = 1f;
            bar.Add(_stationName);

            _clock = Signage.Numerals("0:00.0", 52);
            _clock.style.color = Palette.Led;
            _clock.style.minWidth = 220;
            bar.Add(_clock);

            var rule = new VisualElement();
            rule.AddToClassList("band__rule");
            rule.AddToClassList(UiShell.LineBackgroundClass);
            root.Add(rule);

            // The camera view. Nothing is drawn here; it only reserves the space.
            root.Add(Signage.Spacer());

            _led = new LedStrip();
            _led.style.height = BottomBarHeight;
            root.Add(_led);
        }

        protected override void Wire()
        {
        }

        public override void Refresh(GameState state)
        {
            var level = Game.CurrentLevel;
            _stationName.text = level != null ? level.DisplayName : "";
            _roundel.Number = Flow.CurrentStationIndex + 1;
            _roundel.State = StationState.Current;
            _pause.SetEnabled(state == GameState.Play);
            UpdateClock(Flow.Timer.Elapsed);

            if (state != GameState.Play) return;
            _led.Clear();
            _led.Announce("play.next_stop");
        }

        /// <summary>Called every frame in Play by <see cref="GameManager"/>. The clock is tabular so it cannot reflow.</summary>
        public void UpdateClock(double seconds) => _clock.text = ProgressTracker.FormatTime(seconds);
    }
}
