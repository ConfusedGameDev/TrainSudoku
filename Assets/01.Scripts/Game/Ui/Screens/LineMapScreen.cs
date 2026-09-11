using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// One line's map (work order 2): the route, a marker per station, and a docked card for whichever station is
    /// selected. It replaces the scrolling button list the old Level Select used.
    /// </summary>
    /// <remarks>
    /// Built to the Line Map artboard, with two amendments the closed decisions force. The artboard's header and card
    /// each carry a kana line over their roman one and its action button reads <c>RESUME · さいかい</c>: D5 kills the
    /// stacked pair and D13 keeps Japanese out of every <see cref="VisualElement"/>, so each of those is one localised
    /// line in the active locale. The artboard's station names are level filenames that predate D14 and D17; the real
    /// ones come off the assets.
    /// </remarks>
    public sealed class LineMapScreen : UiScreen
    {
        /// <summary>The artboard's legend strip, and the card's action button.</summary>
        private const float LegendHeight = 130f;
        private const float ActionHeight = 104f;

        private LineMapElement _map;
        private StationRoundel _cardRoundel;
        private Label _lineName;
        private Label _cleared;
        private Label _stationName;
        private Label _stationMeta;
        private VisualElement _starsHost;
        private Button _board;
        private int _selected;

        public override bool IsVisibleIn(GameState state) => state == GameState.LevelSelect;

        protected override void BuildTree(VisualElement root)
        {
            root.AddToClassList("screen");

            BuildBand(root);

            _map = new LineMapElement { style = { flexGrow = 1f, flexShrink = 1f } };
            _map.AddToClassList(UiShell.LineTextClass);
            _map.StationClicked += Select;
            root.Add(_map);

            root.Add(BuildCard());
            root.Add(BuildLegend());
        }

        /// <summary>
        /// The dark header: the way back on the left, the screen's name and the line's beside it, and how much of the
        /// line is cleared on the right. The artboard puts the back button here rather than at the foot of the screen.
        /// </summary>
        private void BuildBand(VisualElement root)
        {
            Signage.Band(root, out var bandContent);

            var back = Signage.IconButton(Icons.Back(), AudioCue.UiBack, GoBack);
            back.style.width = 84;
            back.style.height = 84;
            back.style.marginRight = 28;
            back.style.flexShrink = 0;
            bandContent.parent.Insert(0, back);

            bandContent.Add(Signage.SignageLabel(Signage.Text("linemap.title"), 56, "signage--onDark"));
            _lineName = Signage.BodyLabel("", 30, "body--dim");
            bandContent.Add(_lineName);

            // A hairline box rather than a filled pill: on the dark band the line colour has to read as signage, and
            // a solid block of it beside the title would outweigh the title.
            _cleared = Signage.SignageLabel("", 38, UiShell.LineTextClass);
            _cleared.style.flexShrink = 0;
            _cleared.style.alignSelf = Align.Center;
            _cleared.style.paddingLeft = 26;
            _cleared.style.paddingRight = 26;
            _cleared.style.paddingTop = 12;
            _cleared.style.paddingBottom = 12;
            _cleared.style.borderTopWidth = _cleared.style.borderRightWidth =
                _cleared.style.borderBottomWidth = _cleared.style.borderLeftWidth = 3;
            _cleared.AddToClassList(UiShell.LineBorderClass);
            bandContent.parent.Add(_cleared);
        }

        /// <summary>The docked card: which station is selected, how it stands, and the way into it.</summary>
        private VisualElement BuildCard()
        {
            var card = Signage.Column();
            card.AddToClassList("card");
            Signage.Inset(card, 0f, 20f);
            card.style.paddingTop = 28;
            card.style.paddingBottom = 28;
            card.style.paddingLeft = 32;
            card.style.paddingRight = 32;

            var row = Signage.Row();
            row.style.alignItems = Align.Center;
            card.Add(row);

            _cardRoundel = new StationRoundel { Number = 1, Stacked = true };
            _cardRoundel.style.width = 104;
            _cardRoundel.style.height = 104;
            _cardRoundel.style.marginRight = 24;
            _cardRoundel.style.flexShrink = 0;
            row.Add(_cardRoundel);

            var column = Signage.Column();
            column.style.flexGrow = 1f;
            column.style.flexShrink = 1f;
            row.Add(column);

            _stationName = Signage.SignageLabel("", 58);
            column.Add(_stationName);
            _stationMeta = Signage.BodyLabel("", 28, "body--dim");
            column.Add(_stationMeta);

            _starsHost = Signage.Column();
            column.Add(_starsHost);

            // Full width and line-liveried, like the concourse's BOARD: this is the one thing the screen is for, and
            // at this size the label can be any locale's without a fixed box to clip it.
            _board = Signage.LocalizedButton("linemap.board", AudioCue.UiConfirm, BoardSelected);
            _board.RemoveFromClassList("button--primary");
            _board.AddToClassList("button--line");
            _board.AddToClassList(UiShell.LineBackgroundClass);
            _board.style.height = ActionHeight;
            _board.style.marginTop = 24;
            _board.style.marginLeft = 0;
            _board.style.marginRight = 0;
            _board.style.whiteSpace = WhiteSpace.NoWrap;
            card.Add(_board);

            return card;
        }

        /// <summary>The legend: what the three kinds of marker mean, in the same order the player meets them.</summary>
        private VisualElement BuildLegend()
        {
            var legend = Signage.Row();
            legend.style.height = LegendHeight;
            legend.style.flexShrink = 0;
            legend.style.alignItems = Align.Center;
            legend.style.justifyContent = Justify.Center;
            legend.style.backgroundColor = Palette.Ink;

            legend.Add(LegendItem("linemap.legend_cleared", StationState.Cleared));
            legend.Add(LegendItem("linemap.legend_service", StationState.Current));
            legend.Add(LegendItem("linemap.legend_closed", StationState.Closed));
            return legend;
        }

        private VisualElement LegendItem(string key, StationState state)
        {
            var item = Signage.Row();
            item.style.alignItems = Align.Center;
            item.style.marginLeft = 22;
            item.style.marginRight = 22;

            var dot = new VisualElement();
            dot.style.width = 26;
            dot.style.height = 26;
            dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius =
                dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = 13;
            dot.style.marginRight = 12;
            if (state == StationState.Cleared)
            {
                dot.AddToClassList(UiShell.LineBackgroundClass);
            }
            else
            {
                dot.style.backgroundColor = Palette.Paper;
                dot.style.borderTopWidth = dot.style.borderRightWidth =
                    dot.style.borderBottomWidth = dot.style.borderLeftWidth = 5;
                if (state == StationState.Current) dot.AddToClassList(UiShell.LineBorderClass);
                else
                    dot.style.borderTopColor = dot.style.borderRightColor =
                        dot.style.borderBottomColor = dot.style.borderLeftColor = Palette.ClosedLight;
            }

            item.Add(dot);
            var label = Signage.SignageLabel(Signage.Text(key), 30,
                state == StationState.Closed ? "signage--dim" : "signage--onDark");
            item.Add(label);
            return item;
        }

        protected override void Wire()
        {
        }

        public override void Refresh(GameState state)
        {
            var line = Game.CurrentLine;
            _lineName.text = line != null ? line.DisplayName.ToUpperInvariant() : "";

            _map.SetLine(line, StateOf);

            var stations = line != null ? line.StationCount : 0;
            var cleared = 0;
            for (var i = 0; i < stations; i++)
                if (StateOf(i) == StationState.Cleared) cleared++;
            _cleared.text = Signage.Text("linemap.cleared", cleared, stations);

            // Open on the station the player is up to, not always the first.
            var next = Game.NextStationOnLine(Flow.SelectedLineIndex);
            Select(next >= 0 ? next : 0);
        }

        /// <summary>A station is cleared once it has a star, current when it is the first unplayed one, else closed.</summary>
        private StationState StateOf(int station)
        {
            var id = Game.StationId(Flow.SelectedLineIndex, station);
            if (id == null) return StationState.Closed;
            if (Flow.Progress.GetStars(id) > 0) return StationState.Cleared;

            var flat = Flow.Layout.FlatIndex(Flow.SelectedLineIndex, station);
            return Flow.IsUnlocked(flat) ? StationState.Current : StationState.Closed;
        }

        private void Select(int station)
        {
            _selected = station;
            var level = Game.Station(Flow.SelectedLineIndex, station);
            var state = StateOf(station);
            var line = Game.CurrentLine;

            _cardRoundel.Code = line != null ? line.Code : "";
            _cardRoundel.Number = station + 1;
            _cardRoundel.State = state;
            _stationName.text = level != null ? level.DisplayName.ToUpperInvariant() : "";
            _stationMeta.text = Meta(level, state);

            var id = level != null ? level.Id : null;
            _starsHost.Clear();
            _starsHost.Add(Signage.Stars(id != null ? Flow.Progress.GetStars(id) : 0, 34));

            // A half-finished attempt is waiting to be resumed, which is a different offer from starting one.
            var resuming = id != null && Flow.Progress.TryGetInProgress(id, out _);
            _board.text = Signage.Text(resuming ? "linemap.resume" : "linemap.board");
            _board.SetEnabled(state != StationState.Closed);

            _map.Selected = station;
            _map.Refresh();
        }

        /// <summary>
        /// The line under the station name: where the player stands with it, then its size. A level in progress reads
        /// out how much track is already down, because that is what decides whether it is worth going back to.
        /// </summary>
        private string Meta(LevelDefinition level, StationState state)
        {
            if (level == null) return "";

            var size = $"{level.Width}×{level.Height}";
            var id = level.Id;
            if (id != null && Flow.Progress.TryGetInProgress(id, out var progress))
            {
                var total = 0;
                var data = level.ToLevelData();
                foreach (var clue in data.RowClues) total += clue;
                var laid = progress.Pieces.Count + data.FixedPieces.Count;
                return $"{Signage.Text("linemap.in_progress")}   ·   {Signage.Text("linemap.rails", laid, total)}   ·   {size}";
            }

            if (state == StationState.Cleared && Flow.Progress.TryGetBestTime(id, out var seconds))
                return $"{Signage.Text("linemap.best")}   ·   {ProgressTracker.FormatTime(seconds)}   ·   {size}";

            return state == StationState.Closed
                ? $"{Signage.Text("linemap.closed")}   ·   {size}"
                : size;
        }

        private void GoBack()
        {
            if (Game.SoleLineIndex >= 0) Flow.ShowMainMenu();
            else Flow.ShowNetwork();
        }

        private void BoardSelected()
        {
            var flat = Flow.Layout.FlatIndex(Flow.SelectedLineIndex, _selected);
            if (flat < 0 || !Flow.IsUnlocked(flat)) return;
            Flow.StartLevel(flat);
        }
    }
}
