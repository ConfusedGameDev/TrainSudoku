using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The current line flattened to a single horizontal run: one dot per station, joined by the track, filled as
    /// far as the player has travelled. The concourse's "how far along am I" read (work order 2).
    /// </summary>
    /// <remarks>
    /// It is deliberately <b>not</b> a <see cref="LineMapElement"/> with a straight polyline. That element draws a
    /// line's real geometry — loops, corners, the 45-degree map grid — and is the whole subject of the Line Map
    /// screen. This one answers a different question in a strip 60 px tall: how many stations, how many done, which
    /// one is next. Giving the map element a "pretend you are straight" mode would have made both worse.
    ///
    /// Like every element that carries the line, it names no colour: the accent is the resolved <c>color</c> the
    /// shell paints through <see cref="UiShell.LineTextClass"/>.
    /// </remarks>
    public class LineProgressStrip : VisualElement
    {
        private Func<int, StationState> _stateOf;
        private int _count;

        public LineProgressStrip()
        {
            AddToClassList("line-progress");
            AddToClassList(UiShell.LineTextClass);
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        /// <summary>Dot radius. Larger than the map's r9 because this strip is read at a glance, not studied.</summary>
        public float DotRadius { get; set; } = 15f;

        public float DotStroke { get; set; } = 6f;

        /// <summary>The track between the dots. The map's inactive stroke, which is what it is.</summary>
        public float TrackWidth { get; set; } = 8f;

        /// <summary>How many stations, and how each one stands. Both together, because neither is useful alone.</summary>
        public void SetStations(int count, Func<int, StationState> stateOf)
        {
            _count = Mathf.Max(0, count);
            _stateOf = stateOf;
            MarkDirtyRepaint();
        }

        private StationState StateOf(int index) => _stateOf != null ? _stateOf(index) : StationState.Closed;

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (_count <= 0 || rect.width <= 4f || rect.height <= 4f) return;

            var painter = context.painter2D;
            var accent = resolvedStyle.color;
            var y = rect.y + rect.height * 0.5f;

            // The dots must not clip on the ends, so the run is inset by a whole dot, stroke included.
            var radius = Mathf.Min(DotRadius, rect.height * 0.5f - DotStroke);
            if (radius <= 1f) return;
            var first = rect.x + radius + DotStroke * 0.5f;
            var last = rect.xMax - radius - DotStroke * 0.5f;
            var step = _count > 1 ? (last - first) / (_count - 1) : 0f;

            // Track first, dots over it: a dot always wins where the two overlap.
            painter.lineWidth = TrackWidth;
            for (var i = 0; i < _count - 1; i++)
            {
                // A segment is track the player has ridden only once the station behind it is cleared.
                painter.strokeColor = StateOf(i) == StationState.Cleared ? accent : Palette.ClosedLight;
                painter.BeginPath();
                painter.MoveTo(new Vector2(first + step * i, y));
                painter.LineTo(new Vector2(first + step * (i + 1), y));
                painter.Stroke();
            }

            for (var i = 0; i < _count; i++)
            {
                var centre = new Vector2(first + step * i, y);
                var state = StateOf(i);

                if (state == StationState.Cleared)
                {
                    painter.fillColor = accent;
                    painter.BeginPath();
                    painter.Arc(centre, radius, 0f, 360f);
                    painter.Fill();
                    continue;
                }

                // Unridden stations are hollow, in the line colour when they are next and grey when they are shut —
                // the same rule the roundel follows, so a locked station never advertises itself in the livery.
                painter.fillColor = Palette.Paper;
                painter.BeginPath();
                painter.Arc(centre, radius, 0f, 360f);
                painter.Fill();

                painter.strokeColor = state == StationState.Current ? accent : Palette.ClosedLight;
                painter.lineWidth = DotStroke;
                painter.BeginPath();
                painter.Arc(centre, radius, 0f, 360f);
                painter.Stroke();
                painter.lineWidth = TrackWidth;
            }
        }
    }
}
