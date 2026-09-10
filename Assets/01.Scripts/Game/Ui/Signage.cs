using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The widget vocabulary the seven screens are assembled from. It replaces the widget half of
    /// <c>UiBuilder</c>: same idea, UI Toolkit instead of uGUI, and far smaller, because USS in
    /// <c>Uss/components.uss</c> carries the styling that used to be code.
    /// </summary>
    /// <remarks>
    /// Two rules are enforced here rather than left to each screen:
    /// <list type="bullet">
    /// <item><b>Every button plays a cue.</b> <see cref="Button"/> takes an <see cref="AudioCue"/> and there is no
    /// overload without one, so a silent button has to be written deliberately rather than by forgetting.</item>
    /// <item><b>Copy comes from the string table.</b> <see cref="Text"/> resolves a key, so a hard-coded English
    /// string in a screen stands out as the exception it is.</item>
    /// </list>
    /// </remarks>
    public static class Signage
    {
        private const string Table = "UI";

        /// <summary>Resolves a key in the active locale, falling back to the key so a gap is visible, never blank.</summary>
        public static string Text(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (LocalizationSettings.SelectedLocale == null) return key;
            var value = LocalizationSettings.StringDatabase.GetLocalizedString(Table, key);
            return string.IsNullOrEmpty(value) ? key : value;
        }

        // ---- labels ----

        public static Label SignageLabel(string text, int size, params string[] extraClasses)
        {
            var label = new Label(text);
            label.AddToClassList(UiShell.SignageClass);
            foreach (var c in extraClasses) label.AddToClassList(c);
            label.style.fontSize = size;
            label.style.letterSpacing = Tracking(size);
            return label;
        }

        /// <summary>
        /// Section 6's tracking rule, in the only place that can honour it: 0.04-0.10 em, tighter as size grows.
        /// </summary>
        /// <remarks>
        /// It cannot live in USS. <c>letter-spacing</c> there is an absolute pixel value with no em unit, so a
        /// stylesheet can state a constant but not "a fraction of my own size" -- and a constant is wrong at both
        /// ends, since the whole point of the rule is that a 24 px caption wants airier tracking than a 68 px
        /// headline. A flat 0.06 of the size, which this used to be, gets the small end far too tight.
        ///
        /// This is also section 0.1's "recovery is typographic": with the kana line gone (D5), the tracking split
        /// between a small wide over-line and a large tight title is a good part of what is left to carry the
        /// station-sign read.
        /// </remarks>
        public static float Tracking(float size)
        {
            var em = Mathf.Lerp(0.10f, 0.04f, Mathf.InverseLerp(24f, 72f, size));
            return size * em;
        }

        public static Label BodyLabel(string text, int size, params string[] extraClasses)
        {
            var label = new Label(text);
            label.AddToClassList(UiShell.BodyClass);
            foreach (var c in extraClasses) label.AddToClassList(c);
            label.style.fontSize = size;
            return label;
        }

        /// <summary>Tabular figures for a clock or a roundel, so the layout does not twitch as digits change.</summary>
        public static Label Numerals(string text, int size, params string[] extraClasses)
        {
            var label = new Label(text);
            label.AddToClassList(UiShell.NumeralsClass);
            foreach (var c in extraClasses) label.AddToClassList(c);
            label.style.fontSize = size;
            return label;
        }

        // ---- buttons ----

        /// <summary>A button with literal text. Every button plays a cue before it acts.</summary>
        public static Button Button(string text, AudioCue cue, System.Action action, bool primary = false)
        {
            var button = new Button(() =>
            {
                AudioCuePlayer.Play(cue);
                action?.Invoke();
            }) { text = text };

            button.AddToClassList("button");
            button.AddToClassList(UiShell.SignageClass);
            if (primary) button.AddToClassList("button--primary");
            button.style.fontSize = 46;
            return button;
        }

        /// <summary>The same, with the label taken from the string table.</summary>
        public static Button LocalizedButton(string key, AudioCue cue, System.Action action, bool primary = false) =>
            Button(Text(key), cue, action, primary);

        /// <summary>A square button carrying an icon instead of text.</summary>
        public static Button IconButton(Icon icon, AudioCue cue, System.Action action)
        {
            var button = Button("", cue, action);
            button.AddToClassList("button--icon");
            icon.style.flexGrow = 1f;
            icon.style.marginTop = 22;
            icon.style.marginBottom = 22;
            icon.style.marginLeft = 22;
            icon.style.marginRight = 22;
            icon.pickingMode = PickingMode.Ignore;
            button.Add(icon);
            return button;
        }

        // ---- structure ----

        /// <summary>The dark signage band at the top of a screen, plus the line-colour rule under it.</summary>
        public static VisualElement Band(VisualElement parent, out VisualElement content)
        {
            var band = new VisualElement();
            band.AddToClassList("band");
            parent.Add(band);

            content = new VisualElement { style = { flexGrow = 1f, flexShrink = 1f } };
            band.Add(content);

            var rule = new VisualElement();
            rule.AddToClassList("band__rule");
            rule.AddToClassList(UiShell.LineBackgroundClass);
            parent.Add(rule);

            return band;
        }

        /// <summary>
        /// The station name board: a white card with the line-colour rule along its bottom edge, which is how the
        /// mockup draws the top of the concourse.
        /// </summary>
        /// <remarks>
        /// It is the light counterpart of <see cref="Band"/>, not a replacement for it. Section 6 puts signage
        /// panels on <c>--ink</c> and <see cref="Band"/> is that; the mockup's concourse sign is a <c>--paper</c>
        /// card on a <c>--paper</c> ground, held apart by the 4 px <c>--ink</c> border every object in this UI has.
        /// Both are legitimate station signage -- a platform hanging sign is dark, a station name board is light --
        /// so the two live side by side and a screen picks the one it wants.
        ///
        /// The rule sits <b>inside</b> the border rather than under the card, again following the mockup: on a card
        /// the line colour reads as part of the sign, where under it it would read as a separate strip.
        /// </remarks>
        public static VisualElement SignCard(VisualElement parent, out VisualElement content)
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            card.AddToClassList("sign-card");
            parent.Add(card);

            var row = new VisualElement();
            row.AddToClassList("sign-card__row");
            card.Add(row);

            content = new VisualElement { style = { flexGrow = 1f, flexShrink = 1f } };
            row.Add(content);

            var rule = new VisualElement();
            rule.AddToClassList("sign-card__rule");
            rule.AddToClassList(UiShell.LineBackgroundClass);
            card.Add(rule);

            return card;
        }

        public static VisualElement Row(params VisualElement[] children)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            foreach (var child in children) row.Add(child);
            return row;
        }

        public static VisualElement Column(params VisualElement[] children)
        {
            var column = new VisualElement();
            foreach (var child in children) column.Add(child);
            return column;
        }

        /// <summary>An element that soaks up the leftover space in a column or row.</summary>
        public static VisualElement Spacer()
        {
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1f;
            return spacer;
        }

        public static T Margins<T>(T element, float left, float right, float top, float bottom) where T : VisualElement
        {
            element.style.marginLeft = left;
            element.style.marginRight = right;
            element.style.marginTop = top;
            element.style.marginBottom = bottom;
            return element;
        }

        /// <summary>Screen-edge margin, the 48px of section 6.</summary>
        public static T Inset<T>(T element, float top = 0f, float bottom = 0f) where T : VisualElement =>
            Margins(element, 48f, 48f, top, bottom);

        /// <summary>Three stars, filled up to <paramref name="earned"/>.</summary>
        public static VisualElement Stars(int earned, float size)
        {
            var row = Row();
            for (var i = 0; i < 3; i++)
            {
                var star = Icons.StarIcon(i < earned);
                star.style.width = size;
                star.style.height = size;
                star.style.marginRight = size * 0.18f;
                star.style.color = i < earned ? Palette.Warn : Palette.Closed;
                row.Add(star);
            }

            return row;
        }
    }
}
