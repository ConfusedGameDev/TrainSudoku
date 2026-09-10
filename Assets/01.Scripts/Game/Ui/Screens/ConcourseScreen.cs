using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The concourse (work order 2): the station sign, how far along the line the player is, the platform, and the
    /// two ways on — board the next station, or look at the map. No board is loaded behind it.
    /// </summary>
    /// <remarks>
    /// <b>Where this sits against the mockup.</b> Direction 2's concourse is built out of kana/roman stacked pairs —
    /// とれいんすうどく over TRAIN SUDOKU, のりば over BOARD NOW — and D5 killed every one of them. Section 0.1 says
    /// what to do about the plainness that leaves: recover it typographically, not by putting the pair back. So the
    /// sign keeps the mockup's three-tier vertical rhythm (a small line above the title, the title, a small line
    /// below) and fills the two small lines with Latin the game already owns: the line name over the top, the two
    /// termini along the bottom. Same read, one language.
    /// </remarks>
    public sealed class ConcourseScreen : UiScreen
    {
        /// <summary>The platform art's share of the screen. Tall enough to read as a scene, not an icon.</summary>
        private const float ArtHeight = 460f;

        private const string MutedKey = "audio.muted";

        private Label _lineName;
        private Label _terminusFrom;
        private Label _terminusTo;
        private Label _cardLine;
        private Label _stationCount;
        private Label _starCount;
        private StationRoundel _roundel;
        private LineProgressStrip _strip;
        private PlatformArt _platform;
        private Button _board;
        private Button _sound;
        private Button _map;
        private LedStrip _led;

        public override bool IsVisibleIn(GameState state) => state == GameState.MainMenu;

        protected override void BuildTree(VisualElement root)
        {
            root.AddToClassList("screen");

            BuildSign(root);
            root.Add(Signage.Spacer());
            root.Add(BuildProgressCard());

            _platform = new PlatformArt();
            _platform.style.height = ArtHeight;
            _platform.style.flexShrink = 1f;
            _platform.style.marginTop = 36;
            root.Add(_platform);

            root.Add(Signage.Spacer());
            BuildButtons(root);

            _led = new LedStrip();
            _led.style.height = 150;
            root.Add(_led);
        }

        /// <summary>The station name board: roundel, line name, title, termini. Three tiers, as the mockup reads.</summary>
        private void BuildSign(VisualElement root)
        {
            Signage.SignCard(root, out var content);

            _roundel = new StationRoundel { Stacked = true };
            _roundel.style.width = 128;
            _roundel.style.height = 128;
            _roundel.style.marginRight = 30;
            content.parent.Insert(0, _roundel);

            // The over-line, where the kana was. Small, dim and widely tracked, so the title lands harder for it.
            _lineName = Signage.SignageLabel("", 26, "signage--dim");
            _lineName.style.letterSpacing = 6f;
            content.Add(_lineName);

            content.Add(Signage.SignageLabel("TRAIN SUDOKU", 68));

            // The under-line: where the line runs from and to, the way a platform sign carries its two directions.
            // A row pushed to both edges rather than one string, so the two ends read as two directions.
            var termini = Signage.Row();
            termini.style.marginTop = 8;
            _terminusFrom = Terminus();
            _terminusTo = Terminus();
            _terminusTo.style.unityTextAlign = TextAnchor.MiddleRight;
            termini.Add(Chevron(Icons.ArrowLeftSmall(), 0f, 8f));
            termini.Add(_terminusFrom);
            termini.Add(Signage.Spacer());
            termini.Add(_terminusTo);
            termini.Add(Chevron(Icons.ArrowRightSmall(), 8f, 0f));
            content.Add(termini);
        }

        /// <summary>A direction arrow beside a terminus, sized to sit on the cap height of the name next to it.</summary>
        private static Icon Chevron(Icon icon, float left, float right)
        {
            icon.pickingMode = PickingMode.Ignore;
            icon.StrokeWidth = 2.5f;
            icon.style.width = 22;
            icon.style.height = 22;
            icon.style.flexShrink = 0f;
            icon.style.marginLeft = left;
            icon.style.marginRight = right;
            icon.style.color = Palette.Closed;
            return icon;
        }

        private static Label Terminus()
        {
            var label = Signage.BodyLabel("", 24);
            label.style.color = Palette.Closed;
            label.style.letterSpacing = 2f;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.flexShrink = 1f;
            return label;
        }

        /// <summary>Stations done, stars earned, and the line as a strip of dots.</summary>
        private VisualElement BuildProgressCard()
        {
            var card = Signage.Column();
            card.AddToClassList("card");
            Signage.Inset(card, 32f, 0f);
            card.style.paddingTop = 24;
            card.style.paddingBottom = 26;
            card.style.paddingLeft = 28;
            card.style.paddingRight = 28;

            var header = Signage.Row();
            header.style.alignItems = Align.Center;
            card.Add(header);

            var pip = new VisualElement { pickingMode = PickingMode.Ignore };
            pip.AddToClassList(UiShell.LineBackgroundClass);
            pip.style.width = 22;
            pip.style.height = 22;
            pip.style.borderTopLeftRadius = 11;
            pip.style.borderTopRightRadius = 11;
            pip.style.borderBottomLeftRadius = 11;
            pip.style.borderBottomRightRadius = 11;
            pip.style.marginRight = 16;
            header.Add(pip);

            _cardLine = Signage.SignageLabel("", 32);
            header.Add(_cardLine);

            header.Add(Signage.Spacer());

            _stationCount = Signage.Numerals("", 32);
            _stationCount.style.marginRight = 22;
            header.Add(_stationCount);

            var star = Icons.StarIcon(true);
            star.style.width = 30;
            star.style.height = 30;
            star.style.color = Palette.Warn;
            star.style.marginRight = 10;
            header.Add(star);

            _starCount = Signage.Numerals("", 32);
            header.Add(_starCount);

            _strip = new LineProgressStrip();
            _strip.style.height = 56;
            _strip.style.marginTop = 22;
            card.Add(_strip);

            return card;
        }

        private void BuildButtons(VisualElement root)
        {
            var buttons = Signage.Column();
            Signage.Inset(buttons, 0f, 24f);
            root.Add(buttons);

            // The one thing the player came to do, so it wears the line rather than the ink.
            _board = Signage.LocalizedButton("concourse.board", AudioCue.UiConfirm, BoardNow, true);
            _board.RemoveFromClassList("button--primary");
            _board.AddToClassList("button--line");
            _board.AddToClassList(UiShell.LineBackgroundClass);
            Dress(_board, Icons.ArrowRight());
            Signage.Margins(_board, 0f, 0f, 0f, 22f);
            buttons.Add(_board);

            _map = Signage.LocalizedButton("concourse.line_map", AudioCue.MapOpen, OpenMap);
            Dress(_map, Icons.LineRing());
            buttons.Add(_map);

            buttons.Add(BuildIconRow());
        }

        /// <summary>
        /// The three utility buttons the mockup puts under the two big ones.
        /// </summary>
        /// <remarks>
        /// Two of them do something and one does not. Sound and language are real: sound mutes the game and
        /// remembers it, and language walks the four locales, which is also the only way anyone can carry out
        /// M20's outstanding "look at all seven screens in all four locales" check without four builds.
        /// **Settings is deliberately inert and shown disabled**, because there is no settings screen and nothing
        /// left to put in one once sound and language have their own buttons. It is drawn rather than dropped so
        /// the row keeps the mockup's composition, and disabled rather than silent so it does not lie about it.
        ///
        /// Language sits against D5, which says one language per build. It does not break it -- the build still
        /// ships one set of fonts and one string table -- but if the switcher is not wanted in front of players,
        /// this is the button to wrap in a UNITY_EDITOR guard before release.
        /// </remarks>
        private VisualElement BuildIconRow()
        {
            var row = Signage.Row();
            row.AddToClassList("icon-row");

            _sound = Signage.IconButton(Icons.Speaker(), AudioCue.UiClick, ToggleSound);
            _sound.style.marginLeft = 0;
            row.Add(_sound);

            var settings = Signage.IconButton(Icons.Gear(), AudioCue.UiClick, () => { });
            settings.SetEnabled(false);
            row.Add(settings);

            row.Add(Signage.IconButton(Icons.Globe(), AudioCue.UiClick, NextLocale));
            return row;
        }

        /// <summary>Mutes every cue and the train, and remembers it across launches.</summary>
        private void ToggleSound()
        {
            var muted = !AudioListener.pause;
            AudioListener.pause = muted;
            PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            ShowSoundState(muted);
        }

        private void ShowSoundState(bool muted)
        {
            if (_sound == null) return;
            _sound.Clear();
            var icon = muted ? Icons.SpeakerMuted() : Icons.Speaker();
            icon.pickingMode = PickingMode.Ignore;
            icon.style.flexGrow = 1f;
            // The same inset Signage.IconButton uses, so the swapped icon matches its two neighbours exactly.
            icon.style.marginTop = 22;
            icon.style.marginBottom = 22;
            icon.style.marginLeft = 22;
            icon.style.marginRight = 22;
            icon.style.color = muted ? Palette.Closed : Palette.Ink;
            _sound.Add(icon);
        }

        /// <summary>Walks to the next available locale. The shell re-faces every label on the change.</summary>
        private void NextLocale()
        {
            var locales = LocalizationSettings.AvailableLocales != null
                ? LocalizationSettings.AvailableLocales.Locales
                : null;
            if (locales == null || locales.Count < 2) return;

            var current = locales.IndexOf(LocalizationSettings.SelectedLocale);
            LocalizationSettings.SelectedLocale = locales[(current + 1) % locales.Count];

            // Every label on this screen was resolved once, at build time, so the screen has to re-read them.
            Rebuild();
        }

        /// <summary>Re-resolves the copy that was baked in at build time, then re-reads the state.</summary>
        private void Rebuild()
        {
            _board.text = Signage.Text("concourse.board");
            _map.text = Signage.Text("concourse.line_map");
            Refresh(GameState.MainMenu);
        }

        /// <summary>
        /// Left-aligns a button's label and parks an icon at its right edge.
        /// </summary>
        /// <remarks>
        /// The icon is absolutely positioned rather than laid out beside the text, because a <c>Button</c> is a
        /// <c>TextElement</c>: it draws its own label across the whole content box and any child laid out in flow
        /// lands on top of it. Out of flow, the two cannot collide however long the localised label runs.
        /// </remarks>
        private static void Dress(Button button, Icon icon)
        {
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.paddingRight = 120;

            icon.pickingMode = PickingMode.Ignore;
            icon.style.position = Position.Absolute;
            icon.style.right = 36;
            icon.style.top = Length.Percent(50f);
            icon.style.width = 44;
            icon.style.height = 44;
            icon.style.marginTop = -22;
            button.Add(icon);
        }

        protected override void Wire()
        {
            AudioListener.pause = PlayerPrefs.GetInt(MutedKey, 0) == 1;
        }

        public override void Refresh(GameState state)
        {
            var line = Game.CurrentLine;
            var lineIndex = Flow.SelectedLineIndex;
            var stations = Flow.Layout.StationCount(lineIndex);

            var next = Game.NextStationOnLine(lineIndex);
            _roundel.Code = line != null ? line.Code : "TS";
            _roundel.Number = (next >= 0 ? next : stations - 1) + 1;
            _roundel.State = StationState.Current;
            _lineName.text = line != null ? $"{line.DisplayName} LINE".ToUpperInvariant() : "";

            var from = Game.Station(lineIndex, 0);
            var to = Game.Station(lineIndex, stations - 1);
            var bothEnds = from != null && to != null && stations > 1;
            _terminusFrom.text = bothEnds ? from.DisplayName.ToUpperInvariant() : "";
            _terminusTo.text = bothEnds ? to.DisplayName.ToUpperInvariant() : "";

            // No middle dot between the two: the coloured pip to the left of this label is already the separator,
            // and U+00B7 is outside the ja atlas (see Icons.ArrowLeftSmall).
            _cardLine.text = line != null
                ? $"{line.Code}   {line.DisplayName}".ToUpperInvariant()
                : "";

            var cleared = 0;
            for (var i = 0; i < stations; i++)
            {
                var id = Game.StationId(lineIndex, i);
                if (id != null && Flow.Progress.GetStars(id) > 0) cleared++;
            }

            _stationCount.text = $"{cleared} / {stations}";
            _starCount.text = Flow.StarsOnLine(lineIndex).ToString();
            _strip.SetStations(stations, StateOf);

            var destination = Game.Station(lineIndex, next >= 0 ? next : stations - 1);
            _platform.Destination = line != null && destination != null
                ? $"{line.Code}  {destination.DisplayName.ToUpperInvariant()}"
                : "";

            _board.SetEnabled(Game.NextStationIndex() >= 0);
            ShowSoundState(AudioListener.pause);
            // The whole point of the strip is to name where the player is going, so the station goes through as the
            // message's argument rather than the strip announcing a bare heading.
            _led.Clear();
            if (next >= 0 && destination != null)
                _led.Announce("concourse.next_station", destination.DisplayName.ToUpperInvariant());
            else
                _led.Announce("concourse.line_complete");
        }

        /// <summary>The same rule the line map uses: starred is cleared, the first unplayed one is current.</summary>
        private StationState StateOf(int station)
        {
            var id = Game.StationId(Flow.SelectedLineIndex, station);
            if (id == null) return StationState.Closed;
            if (Flow.Progress.GetStars(id) > 0) return StationState.Cleared;

            var flat = Flow.Layout.FlatIndex(Flow.SelectedLineIndex, station);
            return Flow.IsUnlocked(flat) ? StationState.Current : StationState.Closed;
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
