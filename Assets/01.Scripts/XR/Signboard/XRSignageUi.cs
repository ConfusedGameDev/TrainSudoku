using System;
using TrainSudoku.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The station-signage building blocks every XR panel is drawn from (XR-PRD 8, X20): the paper card ruled in ink,
    /// heading and body type, buttons, the LED strip, line badges and star tallies. The signboard and the wrist menu
    /// share them, so the two read as one set of signs.
    /// </summary>
    public static class XRSignageUi
    {
        /// <summary>The paper card ruled in ink that a panel is drawn on. It fills <paramref name="root"/>.</summary>
        public static VisualElement Card(VisualElement root, float border, float radius, float paddingY, float paddingX)
        {
            var card = new VisualElement { name = "card" };
            card.style.flexGrow = 1;
            card.style.backgroundColor = XRPalette.Paper;
            card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = border;
            card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = XRPalette.Ink;
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
        /// A sign button: ink on paper, or ink on the warning yellow for the one to press. A fingertip presses it through
        /// <see cref="XRPanelTouch"/>; its own click is only the Editor's way in, since there are no rays.
        /// </summary>
        public static Button Button(XRSignageAssets assets, string text, Action clicked, bool primary)
        {
            var button = new Button(clicked) { text = text };
            button.style.fontSize = 50f;
            button.style.letterSpacing = 4f;
            button.style.color = primary ? XRPalette.Ink : XRPalette.Paper;
            button.style.backgroundColor = primary ? XRPalette.Warn : XRPalette.Ink;
            button.style.borderTopWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = button.style.borderRightWidth = 0f;
            button.style.paddingTop = button.style.paddingBottom = 18f;
            button.style.paddingLeft = button.style.paddingRight = 34f;
            button.style.marginLeft = 20f;
            Round(button, 16f);
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
            Round(strip, 16f);
            return strip;
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

        public static void Round(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius =
                element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = radius;
        }
    }
}
