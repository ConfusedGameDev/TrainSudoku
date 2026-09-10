using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// One line drawn from its <see cref="LineDefinition"/>: a stroked polyline through the map nodes, a dot at every
    /// station, and interchange rings on top. This is the element that decided the whole UI toolkit choice (7.1) —
    /// uGUI would need a custom mesh or a pre-baked sprite for it, and the map would stop being authorable data.
    /// </summary>
    /// <remarks>
    /// Map nodes are in whatever space the asset was authored in; the element fits their bounding box into itself at
    /// a fixed aspect and never stretches (work order 10), so the same line reads the same on 9:16 and 9:21.
    /// </remarks>
    public class LineMapElement : VisualElement
    {
        /// <summary>Raised with the station index when a station is tapped.</summary>
        public event Action<int> StationClicked;

        private LineDefinition _line;
        private Func<int, StationState> _stateOf;
        private float _padding = 56f;

        /// <summary>Tap tolerance: work order 4.2 asks for the nearest station node within 60 px.</summary>
        public float HitRadius { get; set; } = 60f;

        public LineMapElement()
        {
            AddToClassList("line-map");
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
            RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        /// <summary>Room left around the polyline so a terminus dot is never clipped by the element's edge.</summary>
        public float Padding
        {
            get => _padding;
            set { _padding = value; MarkDirtyRepaint(); }
        }

        /// <summary>The line to draw, and how each of its stations stands with the player.</summary>
        public void SetLine(LineDefinition line, Func<int, StationState> stateOf = null)
        {
            _line = line;
            _stateOf = stateOf;
            MarkDirtyRepaint();
        }

        /// <summary>Recomputes the station colours without rebuilding anything.</summary>
        public void Refresh() => MarkDirtyRepaint();

        // ---- layout ----

        /// <summary>
        /// Maps authored node space into the element, preserving aspect. Returns false when there is nothing to draw
        /// or no room to draw it in.
        /// </summary>
        private bool TryGetTransform(out Vector2 offset, out float scale)
        {
            offset = Vector2.zero;
            scale = 1f;

            var rect = contentRect;
            var nodes = _line != null ? _line.MapNodes : null;
            if (nodes == null || nodes.Count == 0 || rect.width <= 0f || rect.height <= 0f) return false;

            var min = nodes[0];
            var max = nodes[0];
            foreach (var node in nodes)
            {
                min = Vector2.Min(min, node);
                max = Vector2.Max(max, node);
            }

            var span = max - min;
            var available = new Vector2(rect.width - _padding * 2f, rect.height - _padding * 2f);
            if (available.x <= 0f || available.y <= 0f) return false;

            // A single-axis line (a straight run) has zero span on one axis; fall back to the other.
            var sx = span.x > 0.0001f ? available.x / span.x : float.MaxValue;
            var sy = span.y > 0.0001f ? available.y / span.y : float.MaxValue;
            scale = Mathf.Min(sx, sy);
            if (float.IsInfinity(scale) || scale == float.MaxValue) scale = 1f;

            var drawn = span * scale;
            offset = new Vector2(
                rect.x + (rect.width - drawn.x) * 0.5f - min.x * scale,
                rect.y + (rect.height - drawn.y) * 0.5f - min.y * scale);
            return true;
        }

        private Vector2 ToLocal(Vector2 node, Vector2 offset, float scale) => offset + node * scale;

        /// <summary>The element-space position of each station, in station order.</summary>
        private List<Vector2> StationPoints()
        {
            var points = new List<Vector2>();
            if (!TryGetTransform(out var offset, out var scale)) return points;

            var nodes = _line.MapNodes;
            var indices = _line.StationNodeIndices;
            for (var i = 0; i < indices.Count; i++)
            {
                var node = indices[i];
                if (node < 0 || node >= nodes.Count) continue;
                points.Add(ToLocal(nodes[node], offset, scale));
            }

            return points;
        }

        // ---- drawing ----

        private void Draw(MeshGenerationContext context)
        {
            if (!TryGetTransform(out var offset, out var scale)) return;

            var painter = context.painter2D;
            var nodes = _line.MapNodes;
            var accent = resolvedStyle.color;
            var size = Mathf.Min(contentRect.width, contentRect.height);

            var strokeWidth = Mathf.Max(6f, size * 0.055f);
            var dotRadius = strokeWidth * 0.42f;

            // ---- the route itself ----
            painter.strokeColor = accent;
            painter.lineWidth = strokeWidth;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            painter.MoveTo(ToLocal(nodes[0], offset, scale));
            for (var i = 1; i < nodes.Count; i++) painter.LineTo(ToLocal(nodes[i], offset, scale));
            if (_line.MapShape == MapShape.Loop) painter.ClosePath();
            painter.Stroke();

            // ---- a dot per station, on top of the route ----
            var points = StationPoints();
            for (var i = 0; i < points.Count; i++)
            {
                var state = _stateOf != null ? _stateOf(i) : StationState.Current;
                var closed = state == StationState.Closed;

                painter.fillColor = closed ? Palette.ClosedLight : Palette.Paper;
                painter.BeginPath();
                painter.Arc(points[i], dotRadius + strokeWidth * 0.30f, 0f, 360f);
                painter.Fill();

                painter.strokeColor = closed ? Palette.Closed : state == StationState.Cleared ? accent : Palette.Ink;
                painter.lineWidth = strokeWidth * 0.42f;
                painter.BeginPath();
                painter.Arc(points[i], dotRadius + strokeWidth * 0.30f, 0f, 360f);
                painter.Stroke();

                // A cleared station gets a solid centre, so progress reads at a glance without any text.
                if (state != StationState.Cleared) continue;
                painter.fillColor = accent;
                painter.BeginPath();
                painter.Arc(points[i], dotRadius * 0.55f, 0f, 360f);
                painter.Fill();
            }
        }

        // ---- interaction ----

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (StationClicked == null) return;

            var points = StationPoints();
            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < points.Count; i++)
            {
                var distance = Vector2.Distance(points[i], evt.localPosition);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }

            if (best < 0 || bestDistance > HitRadius) return;
            evt.StopPropagation();
            StationClicked(best);
        }
    }
}
