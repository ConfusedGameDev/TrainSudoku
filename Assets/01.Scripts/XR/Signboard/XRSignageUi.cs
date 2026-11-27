using System;
using TrainSudoku.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The station-signage building blocks every XR panel is drawn from (XR-PRD 8, X20): the white card ruled in ink,
    /// heading and body type, buttons, the LED strip, line badges, star tallies and Pip's speech bubble. The signboard,
    /// the wrist menu and the tutorial callout share them, so they read as one set of signs.
    /// </summary>
    /// <remarks>
    /// The look is the web edition's rounded restyle (its <c>tokens.css</c>): rounder corners, a heavier ink rule, and
    /// a "chunk" under every card and button. UI Toolkit has no box shadow, so the chunk is a taller bottom border.
    /// </remarks>
    public static class XRSignageUi
    {
        /// <summary>The ink rule round a signboard card and its buttons, and the chunk under each, in UI pixels.</summary>
        public const float CardBorder = 10f;
        public const float CardChunk = 16f;
        public const float CardRadius = 56f;
        public const float ButtonBorder = 8f;
        public const float ButtonChunk = 12f;
        public const float ButtonRadius = 30f;

        /// <summary>
        /// The colour of the button to press: the line the player is on, as <c>--line-current</c> is on the web. The
        /// shell sets it whenever the line of interest changes; panels pick it up the next time they are drawn.
        /// </summary>
        public static Color Accent { get; set; } = XRPalette.DefaultLine;

        /// <summary>The white card ruled in ink that a panel is drawn on, with its chunk underneath. It fills <paramref name="root"/>.</summary>
        public static VisualElement Card(VisualElement root, float border, float radius, float paddingY, float paddingX, float chunk = 0f)
        {
            var card = new VisualElement { name = "card" };
            card.style.flexGrow = 1;
            card.style.backgroundColor = XRPalette.White;
            Rule(card, border, chunk, XRPalette.Ink);
            Round(card, radius);
            card.style.paddingTop = card.style.paddingBottom = paddingY;
            card.style.paddingLeft = card.style.paddingRight = paddingX;
            root.Add(card);
            return card;
        }

        public static Label Text(XRSignageAssets assets, string text, float size, bool heading, Color colour, float letterSpacing = 0f)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = colour;
            label.style.letterSpacing = letterSpacing;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0f;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0f;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            var font = assets == null ? null : heading ? assets.HeadingFont : assets.BodyFont;
            if (font != null) label.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(font));
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        /// <summary>
        /// A sign button: ink on white ruled in ink, or filled with the line colour (<see cref="Accent"/>) for the one to
        /// press, each standing on its chunk. A fingertip presses it through <see cref="XRPanelTouch"/>; its own click is
        /// only the Editor's way in, since there are no rays.
        /// </summary>
        public static Button Button(XRSignageAssets assets, string text, Action clicked, bool primary)
        {
            var button = new Button(clicked) { text = text };
            var fill = primary ? Accent : XRPalette.White;
            button.style.fontSize = 50f;
            button.style.letterSpacing = 4f;
            button.style.color = primary ? XRPlatformMap.Contrast(fill) : XRPalette.Ink;
            button.style.backgroundColor = fill;
            Rule(button, ButtonBorder, ButtonChunk, XRPalette.Ink);
            button.style.paddingTop = button.style.paddingBottom = 12f;
            button.style.paddingLeft = button.style.paddingRight = 34f;
            button.style.marginLeft = 20f;
            Round(button, ButtonRadius);
            if (assets != null && assets.HeadingFont != null)
                button.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(assets.HeadingFont));
            return button;
        }

        /// <summary>The LED strip: amber type on the dark ground, the phone's departure-board look.</summary>
        public static VisualElement Led(VisualElement parent)
        {
            var strip = Row(parent);
            strip.style.alignItems = Align.Center;
            strip.style.backgroundColor = XRPalette.LedGround;
            strip.style.paddingTop = strip.style.paddingBottom = 16f;
            strip.style.paddingLeft = strip.style.paddingRight = 30f;
            Round(strip, 24f);
            return strip;
        }

        /// <summary>
        /// Pip's speech bubble: white, ruled in ink, with one squarer corner at the bottom on the side Pip stands. It
        /// wraps and never truncates.
        /// </summary>
        public static Label Bubble(XRSignageAssets assets, string text, float size, bool pipOnLeft = true)
        {
            var bubble = Text(assets, text, size, true, XRPalette.Ink, 2f);
            bubble.style.whiteSpace = WhiteSpace.Normal;
            bubble.style.flexShrink = 1;
            bubble.style.backgroundColor = XRPalette.White;
            Rule(bubble, ButtonBorder, 0f, XRPalette.Ink);
            Round(bubble, 40f);
            if (pipOnLeft) bubble.style.borderBottomLeftRadius = 10f;
            else bubble.style.borderBottomRightRadius = 10f;
            bubble.style.paddingTop = bubble.style.paddingBottom = 14f;
            bubble.style.paddingLeft = bubble.style.paddingRight = 28f;
            return bubble;
        }

        /// <summary>A line's roundel: its code on a disc of its colour.</summary>
        public static VisualElement Badge(XRSignageAssets assets, LineDefinition line, float size)
        {
            var colour = line != null ? line.Color : XRPalette.Warn;
            var badge = new VisualElement();
            Size(badge, size, size);
            Round(badge, size / 2f);
            badge.style.backgroundColor = colour;
            badge.style.alignItems = Align.Center;
            badge.style.justifyContent = Justify.Center;
            var code = Text(assets, line != null ? line.Code : "", size * 0.42f, true, XRPlatformMap.Contrast(colour), 2f);
            code.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.Add(code);
            return badge;
        }

        public static VisualElement StarTally(XRSignageAssets assets, int stars)
        {
            var tally = Row(null);
            tally.style.alignItems = Align.Center;
            tally.Add(new XRStarGlyph(58f, XRPalette.Led));
            Put(tally, Text(assets, stars.ToString(), 64f, true, XRPalette.Led, 4f)).style.marginLeft = 14f;
            return tally;
        }

        /// <summary>Adds <paramref name="child"/> and hands it back, for styling it in the same breath.</summary>
        public static T Put<T>(VisualElement parent, T child) where T : VisualElement
        {
            parent.Add(child);
            return child;
        }

        public static VisualElement Row(VisualElement parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.pickingMode = PickingMode.Ignore;
            parent?.Add(row);
            return row;
        }

        public static VisualElement Spacer()
        {
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            spacer.pickingMode = PickingMode.Ignore;
            return spacer;
        }

        public static void Size(VisualElement element, float width, float height)
        {
            element.style.width = width;
            element.style.height = height;
            element.style.flexShrink = 0;
        }

        /// <summary>An ink rule all round, <paramref name="chunk"/> pixels taller along the bottom: the restyle's drop, without a shadow.</summary>
        public static void Rule(VisualElement element, float border, float chunk, Color colour)
        {
            element.style.borderTopWidth = element.style.borderLeftWidth = element.style.borderRightWidth = border;
            element.style.borderBottomWidth = border + chunk;
            element.style.borderTopColor = element.style.borderBottomColor = element.style.borderLeftColor = element.style.borderRightColor = colour;
        }

        public static void Round(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius =
                element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = radius;
        }
    }

    /// <summary>A card's tail: a filled triangle pointing down, centred under whatever is above it.</summary>
    public sealed class XRTailGlyph : VisualElement
    {
        private readonly Color _colour;

        public XRTailGlyph(float width, float height, Color colour)
        {
            _colour = colour;
            style.width = width;
            style.height = height;
            style.flexShrink = 0;
            style.alignSelf = Align.Center;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            var painter = context.painter2D;
            painter.fillColor = _colour;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.center.x, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
