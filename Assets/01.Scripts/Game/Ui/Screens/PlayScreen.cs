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
        /// <summary>Sign bar and LED strip heights, in reference pixels. What the camera gets is measured, not these.</summary>
        public const float TopBarHeight = 250f;
        public const float BottomBarHeight = 150f;

        private VisualElement _view;
        private StationRoundel _roundel;
        private Label _stationName;
        private Label _clock;
        private string _clockText;
        private Button _pause;
        private LedStrip _led;

        /// <summary>
        /// The board's frame does not slide. See <see cref="UiScreen.Animates"/>: it is up across four states, and
        /// the camera insets below are measured off this tree, so a transform on it would be a transform on them.
        /// </summary>
        protected override bool Animates => false;

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

            _clock = Signage.Numerals("00:00", 52);
            _clockText = null;   // a fresh label: whatever the cache held belongs to the old one
            _clock.style.color = Palette.Led;
            _clock.style.minWidth = 220;
            bar.Add(_clock);

            var rule = new VisualElement();
            rule.AddToClassList("band__rule");
            rule.AddToClassList(UiShell.LineBackgroundClass);
            root.Add(rule);

            // The camera view. Nothing is drawn here; it only reserves the space -- and it is the space the camera
            // is told about, so the two can never drift apart.
            _view = Signage.Spacer();
            root.Add(_view);

            _led = new LedStrip();
            _led.style.height = BottomBarHeight;
            root.Add(_led);
        }

        protected override void Wire()
        {
            // The strip's geometry is only known after the first layout, and it moves again on rotation and when the
            // safe area lands (iOS reports it a frame late). Re-measure whenever it moves.
            _view.RegisterCallback<GeometryChangedEvent>(_ => PushHudInsets());
        }

        public override void Refresh(GameState state)
        {
            var level = Game.CurrentLevel;
            _stationName.text = level != null ? level.DisplayName : "";
            _roundel.Number = Flow.CurrentStationIndex + 1;
            _roundel.State = StationState.Current;
            _pause.SetEnabled(state == GameState.Play);
            UpdateClock(Flow.Timer.Elapsed);
            PushHudInsets();

            if (state != GameState.Play) return;
            _led.Clear();
            _led.Announce("play.next_stop");
        }

        /// <summary>
        /// Prints a satisfied row or column on the LED strip (work order 9, clue satisfied). Indices are the board's,
        /// so they are counted from zero; the strip announces them the way a passenger would count platforms.
        /// </summary>
        public void AnnounceLineClear(bool isRow, int index) =>
            _led.Announce(isRow ? "play.row_clear" : "play.column_clear", index + 1);

        /// <summary>
        /// Called every frame in Play by <see cref="GameManager"/>. The clock is tabular so it cannot reflow, and it
        /// reads in whole seconds, so the text is only assigned on the frame it actually changes — writing it every
        /// frame would queue a layout pass sixty times a second to say the same thing.
        /// </summary>
        public void UpdateClock(double seconds)
        {
            var text = ProgressTracker.FormatTime(seconds);
            if (text == _clockText) return;
            _clockText = text;
            _clock.text = text;
        }

        /// <summary>
        /// Hands the camera the two bars as fractions of the screen height, so the board is framed in the strip
        /// between them (work order 5.3, D9).
        /// </summary>
        /// <remarks>
        /// The numbers are <b>measured off the laid-out strip</b>, not derived from <see cref="TopBarHeight"/> and
        /// <see cref="BottomBarHeight"/>. That way the safe-area padding the shell puts on the root, and any bar that
        /// ends up taller than its nominal height, are both already in them -- and a constant can never go stale
        /// against the layout it is supposed to describe.
        /// </remarks>
        private void PushHudInsets()
        {
            var root = Root;
            if (_view == null || root == null || Game == null) return;
            var rig = Game.BoardCamera;
            if (rig == null) return;

            // worldBound is panel space, the same space as the panel's own visual tree, so the two divide cleanly.
            var screen = root.panel != null ? root.panel.visualTree.layout.height : root.worldBound.height;
            var strip = _view.worldBound;
            if (screen <= 0f || strip.height <= 0f || float.IsNaN(strip.height)) return;

            rig.SetHudInsets(strip.yMin / screen, (screen - strip.yMax) / screen);
        }
    }
}
