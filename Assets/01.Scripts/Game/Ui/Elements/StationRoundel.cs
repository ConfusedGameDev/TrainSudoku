using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>How a station stands with the player.</summary>
    public enum StationState
    {
        /// <summary>Reached but not yet finished, or the one the player is looking at.</summary>
        Current,

        /// <summary>Finished at least once.</summary>
        Cleared,

        /// <summary>Not yet reachable.</summary>
        Closed,
    }

    /// <summary>
    /// The station roundel: a ring with a number or a line code in it. A perfect circle — the one shape in this UI
    /// that is not square-cornered (work order 6).
    /// </summary>
    /// <remarks>
    /// The ring is stroked with <see cref="Painter2D"/> so it stays crisp at any size and can be re-tinted for free.
    /// The label is a real <see cref="Label"/> child rather than painted text, because Painter2D cannot draw text and
    /// because the numerals must be the tabular ones from the font table.
    ///
    /// **The accent colour is the element's resolved <c>color</c>**, so tagging a roundel
    /// <see cref="UiShell.LineBackgroundClass"/>-style with <see cref="UiShell.LineTextClass"/> lets the shell tint it
    /// with the active line and the roundel never names a line's colour itself.
    /// </remarks>
    public class StationRoundel : VisualElement
    {
        private readonly Label _label = new Label();
        private string _code = "";
        private int _number = -1;
        private StationState _state = StationState.Current;

        public StationRoundel()
        {
            AddToClassList("station-roundel");
            AddToClassList(UiShell.LineTextClass);   // the shell paints the accent

            _label.pickingMode = PickingMode.Ignore;
            _label.style.unityTextAlign = TextAnchor.MiddleCenter;
            _label.style.flexGrow = 1f;
            _label.style.color = Palette.Ink;
            Add(_label);

            style.justifyContent = Justify.Center;
            style.alignItems = Align.Center;

            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        /// <summary>The line's two letters, shown when no <see cref="Number"/> is set.</summary>
        public string Code
        {
            get => _code;
            set { _code = value ?? ""; Refresh(); }
        }

        /// <summary>The station's position on the line, 1-based. Negative shows the <see cref="Code"/> instead.</summary>
        public int Number
        {
            get => _number;
            set { _number = value; Refresh(); }
        }

        public StationState State
        {
            get => _state;
            set
            {
                _state = value;
                EnableInClassList("station-roundel--cleared", value == StationState.Cleared);
                EnableInClassList("station-roundel--closed", value == StationState.Closed);
                Refresh();
            }
        }

        private void Refresh()
        {
            _label.text = _number >= 0 ? _number.ToString() : _code;
            _label.style.color = _state == StationState.Closed ? Palette.Closed : Palette.Ink;

            var size = resolvedStyle.width;
            if (size > 0f) _label.style.fontSize = Mathf.Round(size * (_number >= 0 ? 0.44f : 0.38f));
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            var size = Mathf.Min(rect.width, rect.height);
            if (size <= 2f) return;

            var painter = context.painter2D;
            var centre = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);

            // A closed station is drawn in the grey of everything the player cannot reach yet, never in the line
            // colour: a locked line must not advertise itself with its own livery.
            var accent = _state == StationState.Closed ? Palette.Closed : resolvedStyle.color;
            var ringWidth = size * (_state == StationState.Current ? 0.16f : 0.12f);
            var radius = (size - ringWidth) * 0.5f;

            // A current station gets a soft outer halo so the eye lands on it first.
            if (_state == StationState.Current)
            {
                painter.strokeColor = new Color(accent.r, accent.g, accent.b, 0.28f);
                painter.lineWidth = ringWidth * 2.1f;
                painter.BeginPath();
                painter.Arc(centre, radius, 0f, 360f);
                painter.Stroke();
            }

            painter.fillColor = _state == StationState.Closed ? Palette.ClosedLight : Palette.Paper;
            painter.BeginPath();
            painter.Arc(centre, radius, 0f, 360f);
            painter.Fill();

            painter.strokeColor = accent;
            painter.lineWidth = ringWidth;
            painter.BeginPath();
            painter.Arc(centre, radius, 0f, 360f);
            painter.Stroke();
        }
    }
}
