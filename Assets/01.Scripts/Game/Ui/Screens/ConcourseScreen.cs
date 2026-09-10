using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The concourse (work order 2): the station sign, how far along the line the player is, and the two ways on —
    /// board the next station, or look at the map. No board is loaded behind it.
    /// </summary>
    public sealed class ConcourseScreen : UiScreen
    {
        private Label _lineName;
        private Label _progress;
        private StationRoundel _roundel;
        private Button _board;
        private LedStrip _led;

        public override bool IsVisibleIn(GameState state) => state == GameState.MainMenu;

        protected override void BuildTree(VisualElement root)
        {
            root.AddToClassList("screen");

            Signage.Band(root, out var bandContent);
            var bandRow = bandContent.parent;

            _roundel = new StationRoundel { Number = -1 };
            _roundel.style.width = 84;
            _roundel.style.height = 84;
            _roundel.style.marginRight = 28;
            bandRow.Insert(0, _roundel);

            bandContent.Add(Signage.SignageLabel("TRAIN SUDOKU", 64, "signage--onDark"));
            _lineName = Signage.BodyLabel("", 30, "body--dim");
            bandContent.Add(_lineName);

            root.Add(Signage.Spacer());

            var middle = Signage.Column();
            Signage.Inset(middle);
            root.Add(middle);

            _progress = Signage.SignageLabel("", 40);
            middle.Add(_progress);

            root.Add(Signage.Spacer());

            var buttons = Signage.Column();
            Signage.Inset(buttons, 0f, 24f);
            root.Add(buttons);

            _board = Signage.LocalizedButton("concourse.board", AudioCue.UiConfirm, BoardNow, true);
            Signage.Margins(_board, 0f, 0f, 0f, 22f);
            buttons.Add(_board);

            buttons.Add(Signage.LocalizedButton("concourse.line_map", AudioCue.MapOpen, OpenMap));

            _led = new LedStrip();
            _led.style.height = 150;
            root.Add(_led);
        }

        protected override void Wire()
        {
        }

        public override void Refresh(GameState state)
        {
            var line = Game.CurrentLine;
            _roundel.Code = line != null ? line.Code : "TS";
            _roundel.State = StationState.Current;
            _lineName.text = line != null ? line.DisplayName.ToUpperInvariant() : "";

            var index = Game.NextStationIndex();
            var stars = Flow.StarsOnLine(Flow.SelectedLineIndex);
            var total = Flow.Layout.StationCount(Flow.SelectedLineIndex) * 3;
            _progress.text = $"{stars} / {total}";

            _board.SetEnabled(index >= 0);
            _led.Clear();
            _led.Announce(index >= 0 ? "concourse.next_station" : "concourse.line_complete");
        }

        /// <summary>
        /// The map button. With one line in the network the network screen has nothing to choose between, so this
        /// goes straight through it to that line's map -- both hops are legal, the same way <see cref="BoardNow"/>
        /// takes two. With two it stops at the network, which is then a real choice.
        /// </summary>
        private void OpenMap()
        {
            var sole = Game.SoleLineIndex;
            Flow.ShowNetwork();
            if (sole >= 0 && Flow.IsLineUnlocked(sole)) Flow.ShowLineMap(sole);
        }

        /// <summary>Straight to the station the player is up to. Both hops are legal from the menu.</summary>
        private void BoardNow()
        {
            var index = Game.NextStationIndex();
            if (index < 0) return;
            Flow.ShowLevelSelect();
            Flow.StartLevel(index);
        }
    }
}
