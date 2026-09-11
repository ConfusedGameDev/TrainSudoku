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
        /// <summary>Five 72 px rows. Past that the legend scrolls instead of growing.</summary>
        private const float LegendMaxHeight = 360f;

        private NetworkMapElement _map;
        private VisualElement _legend;

        /// <summary>
        /// Which lines were open the last time this screen was looked at. A line that has opened since is drawn
        /// opening (work order 9); on the first visit of a session there is no "since", so nothing plays and a
        /// player returning to the map is not told again about a line they earned an hour ago.
        /// </summary>
        private bool[] _wereOpen;

        public override bool IsVisibleIn(GameState state) => state == GameState.Network;

        protected override void BuildTree(VisualElement root)
        {
            root.AddToClassList("screen");

            Signage.Band(root, out var bandContent);
            bandContent.Add(Signage.SignageLabel(Signage.Text("network.title"), 56, "signage--onDark"));

            _map = new NetworkMapElement { style = { flexGrow = 1f } };
            _map.LineClicked += OpenLine;
            root.Add(_map);

            // The legend used to be a short fixed column because the network was four lines long. It now lists only
            // what the map is showing, which grows as lines open, and a full network's worth of 72 px rows is taller
            // than the screen — so it scrolls inside a capped box rather than pushing Back off the bottom.
            _legend = Signage.Column();
            var legendScroll = new ScrollView(ScrollViewMode.Vertical);
            legendScroll.style.maxHeight = LegendMaxHeight;
            legendScroll.style.flexShrink = 0f;
            legendScroll.Add(_legend);
            Signage.Inset(legendScroll, 0f, 16f);
            root.Add(legendScroll);

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
            PlayAnyOpening();

            _legend.Clear();
            var network = Game.Network;
            if (network == null) return;

            // Only the lines the map is drawing (D10, plus the reveal rule): the legend is a key to the picture, so
            // naming a line that is not on it would give away a network the player has not reached.
            foreach (var i in _map.VisibleLines)
            {
                var line = network.Line(i);
                if (line == null) continue;

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

        /// <summary>Finds a line that has opened since the last visit and runs it out from its interchange.</summary>
        private void PlayAnyOpening()
        {
            var network = Game.Network;
            if (network == null) return;

            var open = new bool[network.LineCount];
            for (var i = 0; i < network.LineCount; i++)
            {
                var line = network.Line(i);
                open[i] = line != null && line.HasContent && Flow.IsLineUnlocked(i);
            }

            if (_wereOpen == null || _wereOpen.Length != open.Length)
            {
                _wereOpen = open;
                return;
            }

            for (var i = 0; i < open.Length; i++)
            {
                if (!open[i] || _wereOpen[i]) continue;
                AudioCuePlayer.Play(AudioCue.LineUnlocked);
                _map.PlayOpening(i);
                // Only this one is marked as seen, so a second line opening in the same breath still gets its turn.
                _wereOpen[i] = true;
                return;
            }

            _wereOpen = open;
        }

        private void OpenLine(int lineIndex)
        {
            if (!Flow.IsLineUnlocked(lineIndex)) return;
            AudioCuePlayer.Play(AudioCue.StationSelect);
            Flow.ShowLineMap(lineIndex);
        }
    }
}
