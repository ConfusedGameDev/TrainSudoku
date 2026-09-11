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
        /// <summary>
        /// Sign bar and LED strip heights, in reference pixels. What the camera gets is measured, not these — and
        /// <see cref="TopBarHeight"/> is now only what the rest of the UI borrows for its own slide distance, since
        /// the top of this screen is the bar plus the progress block and measures itself.
        /// </summary>
        public const float TopBarHeight = 250f;
        public const float BottomBarHeight = 150f;

        /// <summary>The white sign bar alone, without the progress block under it.</summary>
        private const float SignBarHeight = 190f;

        private VisualElement _view;
        private StationRoundel _roundel;
        private LineProgressStrip _strip;
        private Label _laid;
        private Button _hint;
        private TutorialCallout _coach;
        private TutorialBriefing _briefing;
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

            BuildSignBar(root);
            BuildProgress(root);

            // The camera view. Nothing is drawn here; it only reserves the space -- and it is the space the camera
            // is told about, so the two can never drift apart. Everything above it is a sibling, never a child, or
            // the insets would stop matching what the player can actually see.
            _view = Signage.Spacer();
            root.Add(_view);

            BuildHint(_view);

            // Inside the camera strip, so it is positioned in the same space the board is drawn in and cannot be
            // pushed about by the bars above and below. Hidden on every non-tutorial level.
            _coach = new TutorialCallout();
            _view.Add(_coach);

            _led = new LedStrip();
            _led.style.height = BottomBarHeight;
            root.Add(_led);

            // Over everything, and last so it draws on top. It is the only thing on this screen that takes taps.
            _briefing = new TutorialBriefing();
            _briefing.Finished += () => SetCoachVisible(true);
            root.Add(_briefing);
        }

        /// <summary>
        /// The white sign bar: the way out, which station this is, and the clock. Light rather than the dark band the
        /// other screens wear, because the artboard treats the play frame as a platform sign, not a headline.
        /// </summary>
        private void BuildSignBar(VisualElement root)
        {
            var bar = Signage.Row();
            bar.style.height = SignBarHeight;
            bar.style.flexShrink = 0;
            bar.style.backgroundColor = Palette.Paper;
            bar.style.paddingLeft = 44;
            bar.style.paddingRight = 44;
            root.Add(bar);

            _pause = Signage.IconButton(Icons.Pause(), AudioCue.UiClick, () => Flow.PauseGame());
            _pause.style.width = 100;
            _pause.style.height = 100;
            _pause.style.marginRight = 32;
            _pause.style.flexShrink = 0;
            _pause.style.backgroundColor = Color.clear;
            _pause.style.color = Palette.Ink;
            Border(_pause, 4, Palette.Ink);
            bar.Add(_pause);

            _roundel = new StationRoundel { Number = 1, Stacked = true };
            _roundel.style.width = 120;
            _roundel.style.height = 120;
            _roundel.style.marginRight = 24;
            _roundel.style.flexShrink = 0;
            bar.Add(_roundel);

            _stationName = Signage.SignageLabel("", 58);
            _stationName.style.flexGrow = 1f;
            _stationName.style.flexShrink = 1f;
            bar.Add(_stationName);

            // The clock is a lit readout on an unlit ground, so it needs its own dark block to sit on. A fixed width
            // rather than the artboard's content-driven one: the numerals are tabular and the box must not twitch.
            var box = Signage.Column();
            box.style.height = 108;
            box.style.width = 240;
            box.style.flexShrink = 0;
            box.style.justifyContent = Justify.Center;
            box.style.alignItems = Align.FlexEnd;
            box.style.paddingLeft = 24;
            box.style.paddingRight = 24;
            box.style.backgroundColor = Palette.LedGround;
            bar.Add(box);

            var caption = Signage.SignageLabel(Signage.Text("pause.elapsed"), 22);
            caption.style.color = Palette.Led;
            caption.style.opacity = 0.75f;
            box.Add(caption);

            _clock = Signage.Numerals("00:00", 58);
            _clockText = null;   // a fresh label: whatever the cache held belongs to the old one
            _clock.style.color = Palette.Led;
            box.Add(_clock);

            var rule = new VisualElement();
            rule.AddToClassList("band__rule");
            rule.AddToClassList(UiShell.LineBackgroundClass);
            root.Add(rule);
        }

        /// <summary>
        /// How much of the level's track is down, as a count and as a strip of one dot per rail. The same element the
        /// concourse uses for stations along a line: it takes a count and a state per index and knows nothing else.
        /// </summary>
        private void BuildProgress(VisualElement root)
        {
            var block = Signage.Column();
            block.style.flexShrink = 0;
            block.style.marginTop = 28;
            block.style.marginBottom = 8;
            Signage.Inset(block, 0f, 0f);
            root.Add(block);

            var caption = Signage.Row();
            caption.style.marginBottom = 14;
            block.Add(caption);

            var title = Signage.SignageLabel(Signage.Text("pause.rails_laid"), 30);
            title.style.color = Palette.InkDim;
            caption.Add(title);
            caption.Add(Signage.Spacer());

            _laid = Signage.SignageLabel("", 34);
            _laid.style.color = Palette.Ink;
            caption.Add(_laid);

            _strip = new LineProgressStrip { DotRadius = 17f, DotStroke = 5f, TrackWidth = 6f };
            _strip.style.height = 34;
            block.Add(_strip);
        }

        /// <summary>
        /// The hint button the artboard puts over the board's bottom right. There is no hint system in Core yet, so it
        /// ships <b>disabled</b>: the layout is right for the day there is one, and a dimmed control cannot be mistaken
        /// for a live one or swallow a tap meant for the board.
        /// </summary>
        /// <remarks>
        /// It hangs off the <b>view spacer</b>, not the screen root, so it is positioned against the bottom of the
        /// camera strip rather than against the bottom of the screen. That is what keeps it clear of the tutorial
        /// band, whose height is not known in advance and is zero on most levels. The spacer is
        /// <see cref="PickingMode.Ignore"/>, which does not travel to its children — the button still takes its own
        /// taps, and the board still gets everything else (4.4).
        /// </remarks>
        private void BuildHint(VisualElement host)
        {
            _hint = Signage.IconButton(Icons.Skip(), AudioCue.UiClick, () => { });
            _hint.style.position = Position.Absolute;
            _hint.style.right = 44;
            _hint.style.bottom = 70f;
            _hint.style.width = 124;
            _hint.style.height = 124;
            _hint.style.color = Palette.Ink;
            _hint.AddToClassList(UiShell.LineBackgroundClass);
            Border(_hint, 4, Palette.Ink);
            _hint.SetEnabled(false);
            host.Add(_hint);
        }

        private static void Border(VisualElement element, float width, Color colour)
        {
            element.style.borderTopWidth = element.style.borderRightWidth =
                element.style.borderBottomWidth = element.style.borderLeftWidth = width;
            element.style.borderTopColor = element.style.borderRightColor =
                element.style.borderBottomColor = element.style.borderLeftColor = colour;
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
            SetCoachVisible(state == GameState.Play);
            _coach.Refresh();
            _briefing.Refresh();
            UpdateProgress();
            UpdateClock(Flow.Timer.Elapsed);
            PushHudInsets();

            if (state != GameState.Play) return;
            _led.Clear();
            _led.Announce("play.next_stop");
        }

        /// <summary>
        /// Puts one line of tutorial instruction up, or takes the callout away with null. The key comes from
        /// <see cref="TrainSudoku.Core.TutorialCoach"/>; this screen does not decide what is taught or when.
        /// </summary>
        public void ShowTutorial(string key) => _coach.Show(key);

        /// <summary>
        /// Puts the rules up, three cards over an ink scrim, and hands the board back when they are dismissed.
        /// Only the tutorial station ever asks.
        /// </summary>
        public void ShowBriefing()
        {
            _briefing.Open();
            SetCoachVisible(false);
        }

        /// <summary>
        /// The callout is up in Play and only in Play — and never behind the briefing, which is talking about the
        /// same thing at the same time and would be arguing with it.
        /// </summary>
        private void SetCoachVisible(bool visible) => _coach.SetVisible(visible && !_briefing.IsOpen);

        /// <summary>
        /// Anchors the callout beside a cell on the board. Called every frame in Play, because the camera re-fits
        /// on rotation and when the safe area lands, and a bubble that lagged its cell would point at nothing.
        /// </summary>
        /// <remarks>
        /// This is the one place in the game that maps world space into the UI panel. It is the same two steps the
        /// shell already uses for the safe area (<c>UiShell.ApplySafeArea</c>): project, then hand the point to
        /// <see cref="RuntimePanelUtils.ScreenToPanel"/> with the y flipped, because screen space counts up from the
        /// bottom and panel space counts down from the top. <c>BoardCamera</c> assigns its fitted matrix to
        /// <c>Camera.projectionMatrix</c>, so the projection already carries the HUD-strip shift.
        /// </remarks>
        public void PointTutorialAt(Vector3 centre, Vector3 far, Vector3 near)
        {
            var panel = Root != null ? Root.panel : null;
            var camera = Game != null && Game.BoardCamera != null ? Game.BoardCamera.Camera : null;
            if (panel == null || camera == null) return;

            if (!TryProject(panel, camera, centre, out var c) ||
                !TryProject(panel, camera, far, out var f) ||
                !TryProject(panel, camera, near, out var b))
                return;

            // The callout is a child of the strip, so everything is expressed relative to the strip's own origin.
            var origin = _view.worldBound.position;
            var host = new Rect(Vector2.zero, _view.worldBound.size);
            if (host.width <= 1f || host.height <= 1f) return;

            _coach.PointAt(c - origin, f - origin, b - origin, host);
        }

        /// <summary>False when the point is behind the camera, where a projection mirrors rather than vanishes.</summary>
        private static bool TryProject(IPanel panel, Camera camera, Vector3 world, out Vector2 point)
        {
            var screen = camera.WorldToScreenPoint(world);
            point = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            return screen.z > 0f;
        }

        /// <summary>
        /// Prints a satisfied row or column on the LED strip (work order 9, clue satisfied). Indices are the board's,
        /// so they are counted from zero; the strip announces them the way a passenger would count platforms.
        /// </summary>
        public void AnnounceLineClear(bool isRow, int index) =>
            _led.Announce(isRow ? "play.row_clear" : "play.column_clear", index + 1);

        /// <summary>
        /// How much track is down out of what the level asks for. Called on every board change, so the strip fills as
        /// the player lays rail rather than only when the screen is shown.
        /// </summary>
        public void UpdateProgress()
        {
            var board = Game.Board;
            var total = board != null ? board.TotalRails : 0;
            var laid = board != null ? Mathf.Min(board.PieceCount, total) : 0;

            _laid.text = $"{laid} / {total}";
            _strip.SetStations(total, i => i < laid ? StationState.Cleared
                : i == laid ? StationState.Current
                : StationState.Closed);
        }

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
