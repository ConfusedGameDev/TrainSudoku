using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// One line's map (work order 2): the route, a dot per station, and a docked card for whichever station is
    /// selected. It replaces the scrolling button list the old Level Select used.
    /// </summary>
    public sealed class LineMapScreen : UiScreen
    {
        private LineMapElement _map;
        private StationRoundel _bandRoundel;
        private StationRoundel _cardRoundel;
        private Label _lineName;
        private Label _stationName;
        private Label _stationMeta;
        private VisualElement _stars;
        private VisualElement _starsHost;
        private Button _board;
        private int _selected;

        public override bool IsVisibleIn(GameState state) => state == GameState.LevelSelect;

        protected override void BuildTree(VisualElement root)
        {
            root.AddToClassList("screen");

            Signage.Band(root, out var bandContent);
            _bandRoundel = new StationRoundel { Number = -1 };
            _bandRoundel.style.width = 84;
            _bandRoundel.style.height = 84;
            _bandRoundel.style.marginRight = 28;
            bandContent.parent.Insert(0, _bandRoundel);

            bandContent.Add(Signage.SignageLabel(Signage.Text("linemap.title"), 56, "signage--onDark"));
            _lineName = Signage.BodyLabel("", 30, "body--dim");
            bandContent.Add(_lineName);

            _map = new LineMapElement { style = { flexGrow = 1f } };
            _map.AddToClassList(UiShell.LineTextClass);
            _map.StationClicked += Select;
            root.Add(_map);

            root.Add(BuildCard());

            // Back retraces the way in: to the network when there was a choice there, to the concourse when the
            // network was stepped over. Both edges are legal out of LevelSelect.
            var back = Signage.LocalizedButton("common.back", AudioCue.UiBack, GoBack);
            Signage.Inset(back, 0f, 24f);
            root.Add(back);
        }

        private VisualElement BuildCard()
        {
            var card = Signage.Row();
            card.AddToClassList("card");
            Signage.Inset(card, 0f, 20f);
            card.style.paddingTop = 24;
            card.style.paddingBottom = 24;
            card.style.paddingLeft = 28;
            card.style.paddingRight = 28;

            _cardRoundel = new StationRoundel { Number = 1 };
            _cardRoundel.style.width = 96;
            _cardRoundel.style.height = 96;
            _cardRoundel.style.marginRight = 24;
            card.Add(_cardRoundel);

            var column = Signage.Column();
            column.style.flexGrow = 1f;
            column.style.flexShrink = 1f;
            card.Add(column);

            _stationName = Signage.SignageLabel("", 52);
            column.Add(_stationName);
            _stationMeta = Signage.BodyLabel("", 28, "body--dim");
            column.Add(_stationMeta);

            _starsHost = Signage.Column();
            column.Add(_starsHost);

            _board = Signage.LocalizedButton("linemap.board", AudioCue.UiConfirm, BoardSelected, true);
            // No fixed width: the label is localised (BOARD / EMBARQUER / SUBIR / 乗車) and a fixed box clips the
            // long ones. It sizes to its own text through the .button padding, with a floor so a short label is
            // still a fair target, and it never shrinks -- the name column beside it gives way instead.
            _board.style.minWidth = 220;
            _board.style.flexShrink = 0;
            _board.style.whiteSpace = WhiteSpace.NoWrap;
            _board.style.marginLeft = 20;
            card.Add(_board);

            return card;
        }

        protected override void Wire()
        {
        }

        public override void Refresh(GameState state)
        {
            var line = Game.CurrentLine;
            _bandRoundel.Code = line != null ? line.Code : "";
            _lineName.text = line != null ? line.DisplayName.ToUpperInvariant() : "";

            _map.SetLine(line, StateOf);

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

            _cardRoundel.Number = station + 1;
            _cardRoundel.State = state;
            _stationName.text = level != null ? level.DisplayName : "";

            var id = level != null ? level.Id : null;
            var best = id != null && Flow.Progress.TryGetBestTime(id, out var seconds)
                ? ProgressTracker.FormatTime(seconds)
                : "—";
            _stationMeta.text = level == null ? "" : $"{level.Width}×{level.Height}   ·   {best}";

            _starsHost.Clear();
            if (_stars != null) _stars = null;
            _stars = Signage.Stars(id != null ? Flow.Progress.GetStars(id) : 0, 34);
            _starsHost.Add(_stars);

            _board.SetEnabled(state != StationState.Closed);
            _map.Refresh();
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
