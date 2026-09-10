using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The concourse's platform art (work order 2): a train standing at a platform, in the active line's livery,
    /// with the next station on its destination blind.
    /// </summary>
    /// <remarks>
    /// <b>Flat rectangles, not a painted illustration.</b> Everything here except the car's rounded ends is a
    /// square-cornered flat fill with a 4 px <c>--ink</c> border, which is section 6's geometry rule applied to a
    /// picture instead of a card. That is a deliberate departure from the mockup, whose train is a soft grey
    /// silhouette with no outline: section 6 is authoritative over the mockup where the two disagree, and an
    /// unoutlined car on a <c>--paper</c> ground has almost no contrast to stand on.
    ///
    /// <b>It uses the signage tokens only.</b> The platform greys are <c>--closed</c> / <c>--closed-light</c> and
    /// the tactile edge is <c>--warn</c>, not the board's <c>Concrete</c> / <c>Steel</c> values, even though those
    /// are named for exactly this — `CLAUDE.md` keeps the two sets disjoint, and this is a signage screen.
    ///
    /// Every proportion is a percentage of the element, so the screen sets one height and the art follows.
    /// </remarks>
    public class PlatformArt : VisualElement
    {
        private readonly Label _plate = new Label();

        public PlatformArt()
        {
            AddToClassList("platform-art");
            pickingMode = PickingMode.Ignore;
            style.overflow = Overflow.Hidden;

            BuildCatenary();
            BuildPlatform();   // before the car, so the car stands in front of the platform edge
            BuildCar();
        }

        /// <summary>What the destination blind reads. A proper noun, so it needs no translation (D14).</summary>
        public string Destination
        {
            get => _plate.text;
            set => _plate.text = value ?? "";
        }

        /// <summary>The overhead line, which is most of what says "railway" in a picture this simple.</summary>
        private void BuildCatenary()
        {
            var wire = Block(Palette.Closed);
            Place(wire, 28f, 28f, 2f, float.NaN);
            wire.style.height = 6;
            Add(wire);

            var drop = Block(Palette.Closed);
            drop.style.position = Position.Absolute;
            drop.style.left = Length.Percent(49f);
            drop.style.top = Length.Percent(2f);
            drop.style.width = 6;
            drop.style.height = Length.Percent(8f);
            Add(drop);
        }

        private void BuildCar()
        {
            var car = Block(Palette.Paper);
            Place(car, 6f, 6f, 12f, float.NaN);
            car.style.height = Length.Percent(46f);
            Border(car, Palette.Ink);
            Radius(car, 26f);
            Add(car);

            // Windows: one row, evenly spread, with the car's own body showing between them as pillars.
            var windows = new VisualElement { pickingMode = PickingMode.Ignore };
            windows.style.position = Position.Absolute;
            windows.style.left = Length.Percent(4f);
            windows.style.right = Length.Percent(4f);
            windows.style.top = Length.Percent(16f);
            windows.style.height = Length.Percent(38f);
            windows.style.flexDirection = FlexDirection.Row;
            car.Add(windows);

            for (var i = 0; i < 5; i++)
            {
                var window = Block(Palette.Ink);
                window.style.flexGrow = 1f;
                window.style.marginLeft = i == 0 ? 0 : 9;
                Radius(window, 8f);
                windows.Add(window);
            }

            // The livery band is the one thing on this screen that is unmistakably the line, so it runs the full
            // length of the car and the shell paints it.
            var livery = new VisualElement { pickingMode = PickingMode.Ignore };
            livery.AddToClassList(UiShell.LineBackgroundClass);
            livery.style.position = Position.Absolute;
            livery.style.left = 0;
            livery.style.right = 0;
            livery.style.top = Length.Percent(62f);
            livery.style.height = Length.Percent(17f);
            car.Add(livery);

            // The destination blind, sitting on the livery the way a real one sits on the body side.
            var blind = Block(Palette.Ink);
            blind.style.position = Position.Absolute;
            blind.style.left = Length.Percent(8f);
            blind.style.top = Length.Percent(58f);
            blind.style.height = Length.Percent(25f);
            blind.style.paddingLeft = 16;
            blind.style.paddingRight = 16;
            blind.style.justifyContent = Justify.Center;
            car.Add(blind);

            _plate.AddToClassList(UiShell.SignageClass);
            _plate.AddToClassList(UiShell.LineTextClass);
            _plate.pickingMode = PickingMode.Ignore;
            _plate.style.fontSize = 24;
            _plate.style.letterSpacing = 2f;
            _plate.style.whiteSpace = WhiteSpace.NoWrap;
            _plate.style.unityTextAlign = TextAnchor.MiddleCenter;
            blind.Add(_plate);
        }

        private void BuildPlatform()
        {
            var platform = new VisualElement { pickingMode = PickingMode.Ignore };
            platform.style.position = Position.Absolute;
            platform.style.left = 0;
            platform.style.right = 0;
            platform.style.top = Length.Percent(58f);
            platform.style.bottom = 0;
            Add(platform);

            // The edge face: the drop from the platform surface to the track, seen straight on.
            var face = Block(Palette.Closed);
            face.style.height = Length.Percent(28f);
            platform.Add(face);

            // The tactile paving, in --warn, which is the same yellow the board's platform edge uses.
            var tactile = Block(Palette.Warn);
            tactile.style.height = Length.Percent(22f);
            tactile.style.flexDirection = FlexDirection.Row;
            tactile.style.alignItems = Align.Center;
            tactile.style.paddingLeft = 10;
            tactile.style.paddingRight = 10;
            platform.Add(tactile);

            // The studs. A tint of --ink rather than a second yellow, so the strip stays one token deep.
            var stud = new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.22f);
            for (var i = 0; i < 26; i++)
            {
                var dot = Block(stud);
                dot.style.flexGrow = 1f;
                dot.style.height = Length.Percent(52f);
                dot.style.marginLeft = i == 0 ? 0 : 10;
                tactile.Add(dot);
            }

            var surface = Block(Palette.ClosedLight);
            surface.style.flexGrow = 1f;
            platform.Add(surface);
        }

        // ---- small helpers, so the builders above read as layout rather than as style plumbing ----

        private static VisualElement Block(Color fill)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.style.backgroundColor = fill;
            element.style.flexShrink = 0f;
            return element;
        }

        private static void Place(VisualElement element, float left, float right, float top, float bottom)
        {
            element.style.position = Position.Absolute;
            element.style.left = Length.Percent(left);
            element.style.right = Length.Percent(right);
            if (!float.IsNaN(top)) element.style.top = Length.Percent(top);
            if (!float.IsNaN(bottom)) element.style.bottom = Length.Percent(bottom);
        }

        private static void Border(VisualElement element, Color colour)
        {
            element.style.borderLeftWidth = 4;
            element.style.borderRightWidth = 4;
            element.style.borderTopWidth = 4;
            element.style.borderBottomWidth = 4;
            element.style.borderLeftColor = colour;
            element.style.borderRightColor = colour;
            element.style.borderTopColor = colour;
            element.style.borderBottomColor = colour;
        }

        private static void Radius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }
    }
}
