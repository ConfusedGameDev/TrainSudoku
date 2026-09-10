#if UNITY_EDITOR
using System.Collections.Generic;
using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// THROWAWAY. M15's look test: the Line map screen mocked at final type, in all four locales, so the question
    /// 0.1 asks — does single-line signage carry the identity? — is answered before six more screens are built on
    /// top of it. Delete with the Spike_M15 scene at sign-off.
    /// </summary>
    public class LookTest : UiScreen
    {
        private LineDefinition _line;
        private LineMapElement _map;
        private LedStrip _led;
        private Label _cardName;
        private Label _cardMeta;
        private StationRoundel _cardRoundel;
        private readonly List<Label> _signage = new List<Label>();
        private readonly List<Label> _body = new List<Label>();
        private int _selected;

        public override bool IsVisibleIn(GameState state) => true;

        private void Start()
        {
            Bind(null);
            SetVisible(true);
            ApplyFonts();
        }

        protected override void BuildTree(VisualElement root)
        {
            _line = UnityEditor.AssetDatabase.LoadAssetAtPath<LineDefinition>("Assets/03.Data/Levels/Line_TS.asset");
            root.style.backgroundColor = Palette.Paper;

            if (Shell != null) Shell.SetLineColour(_line != null ? _line.Color : Palette.Warn);

            root.Add(BuildSignageBand());
            root.Add(BuildRule());
            root.Add(BuildMap());
            root.Add(BuildCard());
            root.Add(BuildLocaleRow());
            root.Add(BuildLed());

            Select(0);
        }

        // ---- the dark signage band, 190px minimum, wrapping rather than truncating (3.3) ----
        private VisualElement BuildSignageBand()
        {
            var band = new VisualElement();
            band.style.minHeight = 190;
            band.style.backgroundColor = Palette.Ink;
            band.style.flexDirection = FlexDirection.Row;
            band.style.alignItems = Align.Center;
            band.style.paddingLeft = 48;
            band.style.paddingRight = 48;
            band.style.paddingTop = 24;
            band.style.paddingBottom = 24;
            band.style.flexShrink = 0;

            var roundel = new StationRoundel { Code = _line != null ? _line.Code : "TS", Number = -1 };
            roundel.style.width = 84;
            roundel.style.height = 84;
            roundel.style.flexShrink = 0;
            roundel.style.marginRight = 28;
            band.Add(roundel);

            var text = new VisualElement { style = { flexGrow = 1f, flexShrink = 1f } };
            band.Add(text);

            var header = Signage("LINE MAP", 56, Palette.Paper);
            text.Add(header);

            var subtitle = Body(_line != null ? _line.DisplayName.ToUpperInvariant() : "", 30, Palette.InkDim);
            subtitle.style.letterSpacing = 2f;
            text.Add(subtitle);

            return band;
        }

        private VisualElement BuildRule()
        {
            var rule = new VisualElement();
            rule.style.height = 12;
            rule.style.flexShrink = 0;
            rule.AddToClassList(UiShell.LineBackgroundClass);
            return rule;
        }

        private VisualElement BuildMap()
        {
            _map = new LineMapElement { style = { flexGrow = 1f } };
            _map.AddToClassList(UiShell.LineTextClass);
            _map.SetLine(_line, StateOf);
            _map.StationClicked += Select;
            return _map;
        }

        /// <summary>The docked card for the current selection — the shape the real Line map screen uses.</summary>
        private VisualElement BuildCard()
        {
            var card = new VisualElement();
            card.style.marginLeft = 48;
            card.style.marginRight = 48;
            card.style.marginBottom = 24;
            card.style.paddingTop = 24;
            card.style.paddingBottom = 24;
            card.style.paddingLeft = 28;
            card.style.paddingRight = 28;
            card.style.backgroundColor = Color.white;
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.Center;
            card.style.flexShrink = 0;
            Border(card, 4, Palette.Ink);

            _cardRoundel = new StationRoundel { Number = 1 };
            _cardRoundel.style.width = 96;
            _cardRoundel.style.height = 96;
            _cardRoundel.style.flexShrink = 0;
            _cardRoundel.style.marginRight = 24;
            card.Add(_cardRoundel);

            var column = new VisualElement { style = { flexGrow = 1f, flexShrink = 1f } };
            card.Add(column);

            _cardName = Signage("", 52, Palette.Ink);
            column.Add(_cardName);

            _cardMeta = Body("", 30, Palette.InkDim);
            column.Add(_cardMeta);

            var arrow = Icons.ArrowRight();
            arrow.style.width = 56;
            arrow.style.height = 56;
            arrow.style.flexShrink = 0;
            arrow.style.color = Palette.Ink;
            card.Add(arrow);

            return card;
        }

        private VisualElement BuildLocaleRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginLeft = 48;
            row.style.marginRight = 48;
            row.style.marginBottom = 16;
            row.style.flexShrink = 0;

            foreach (var code in new[] { "en", "ja", "es", "fr" })
            {
                var button = new Button(() => SelectLocale(code)) { text = code };
                button.style.flexGrow = 1f;
                button.style.height = 64;
                row.Add(button);
            }

            return row;
        }

        private VisualElement BuildLed()
        {
            _led = new LedStrip();
            _led.style.height = 150;
            _led.style.flexShrink = 0;
            _led.SetGrille(UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/02.Graphics/Ui/led-grille.png"));
            _led.Announce("spike.hello");
            _led.Announce("network.opens_after");
            return _led;
        }

        // ---- behaviour ----

        private StationState StateOf(int station)
        {
            // A plausible mid-game shape for the look test: the first three cleared, the fourth current, the rest shut.
            if (station < 3) return StationState.Cleared;
            return station == 3 ? StationState.Current : StationState.Closed;
        }

        private void Select(int station)
        {
            _selected = station;
            var level = _line != null ? _line.Station(station) : null;

            _cardRoundel.Number = station + 1;
            _cardRoundel.State = StateOf(station);
            _cardName.text = level != null ? level.DisplayName : "";

            var times = level != null ? level.StarTimes : null;
            _cardMeta.text = level == null
                ? ""
                : $"{level.Width}x{level.Height}   ·   {ProgressTracker.FormatTime(times[0])} for three stars";

            _map.Refresh();
        }

        private void SelectLocale(string code)
        {
            var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (locale == null) return;
            LocalizationSettings.SelectedLocale = locale;
            ApplyFonts();
            _led.Clear();
            _led.Announce("spike.hello");
            _led.Announce("network.opens_after");
            Debug.Log($"[M15 look test] locale {code}: LINE MAP reads \"" +
                      LocalizationSettings.StringDatabase.GetLocalizedString("UI", "spike.hello") + "\"");
        }

        /// <summary>Swaps every label to the face the Fonts table gives for the active locale (3.4).</summary>
        private void ApplyFonts()
        {
            var signage = LocalizationSettings.AssetDatabase.GetLocalizedAsset<FontAsset>("Fonts", "font.signage");
            var body = LocalizationSettings.AssetDatabase.GetLocalizedAsset<FontAsset>("Fonts", "font.body");

            if (signage != null)
                foreach (var label in _signage) label.style.unityFontDefinition = new StyleFontDefinition(signage);
            if (body != null)
                foreach (var label in _body) label.style.unityFontDefinition = new StyleFontDefinition(body);
        }

        // ---- helpers ----

        private Label Signage(string text, int size, Color colour)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = colour;
            label.style.whiteSpace = WhiteSpace.Normal;   // signage wraps, never ellipsises (3.3)
            label.style.letterSpacing = size * 0.06f;
            _signage.Add(label);
            return label;
        }

        private Label Body(string text, int size, Color colour)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = colour;
            label.style.whiteSpace = WhiteSpace.Normal;
            _body.Add(label);
            return label;
        }

        private static void Border(VisualElement element, float width, Color colour)
        {
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderTopColor = colour;
            element.style.borderBottomColor = colour;
            element.style.borderLeftColor = colour;
            element.style.borderRightColor = colour;
        }

        protected override void Wire()
        {
        }
    }
}
#endif
