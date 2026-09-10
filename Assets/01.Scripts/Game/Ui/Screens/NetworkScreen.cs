using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The network map (work order 2): every line that has content, open ones in full colour, unearned ones tinted
    /// with a lock at the terminus. Only lines with content appear (D10) — v1 ships one, and a map of one line beside
    /// four empty promises would read as a game four-fifths missing.
    /// </summary>
    public sealed class NetworkScreen : UiScreen
    {
        private NetworkMapElement _map;
        private VisualElement _legend;

        public override bool IsVisibleIn(GameState state) => state == GameState.Network;

        protected override void BuildTree(VisualElement root)
        {
            root.AddToClassList("screen");

            Signage.Band(root, out var bandContent);
            bandContent.Add(Signage.SignageLabel(Signage.Text("network.title"), 56, "signage--onDark"));

            _map = new NetworkMapElement { style = { flexGrow = 1f } };
            _map.LineClicked += OpenLine;
            root.Add(_map);

            _legend = Signage.Column();
            Signage.Inset(_legend, 0f, 16f);
            root.Add(_legend);

            var back = Signage.LocalizedButton("common.back", AudioCue.UiBack, () => Flow.ShowMainMenu());
            Signage.Inset(back, 0f, 24f);
            root.Add(back);
        }

        protected override void Wire()
        {
        }

        public override void Refresh(GameState state)
        {
            _map.SetNetwork(Game.Network, Flow.IsLineUnlocked);
            _map.Refresh();

            _legend.Clear();
            var network = Game.Network;
            if (network == null) return;

            for (var i = 0; i < network.LineCount; i++)
            {
                var line = network.Line(i);
                if (line == null || !line.HasContent) continue;   // D10

                var open = Flow.IsLineUnlocked(i);
                var row = Signage.Row();
                row.style.height = 72;

                var swatch = new VisualElement();
                swatch.style.width = 14;
                swatch.style.height = 44;
                swatch.style.marginRight = 22;
                swatch.style.flexShrink = 0;
                swatch.style.backgroundColor = open ? line.Color : Palette.Closed;
                row.Add(swatch);

                var code = Signage.SignageLabel(line.Code, 40, open ? "signage" : "signage--dim");
                code.style.marginRight = 14;
                row.Add(code);

                var name = Signage.SignageLabel(
                    $"{line.DisplayName.ToUpperInvariant()} · {line.StationCount}", 34, "signage--dim");
                name.style.flexGrow = 1f;
                row.Add(name);

                if (!open)
                {
                    var lockIcon = Icons.Padlock();
                    lockIcon.style.width = 40;
                    lockIcon.style.height = 40;
                    lockIcon.style.color = Palette.Closed;
                    row.Add(lockIcon);
                }

                _legend.Add(row);
            }
        }

        private void OpenLine(int lineIndex)
        {
            if (!Flow.IsLineUnlocked(lineIndex)) return;
            AudioCuePlayer.Play(AudioCue.StationSelect);
            Flow.ShowLineMap(lineIndex);
        }
    }
}
