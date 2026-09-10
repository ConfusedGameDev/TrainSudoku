using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The whole network: every line that has content, each in its own colour, with the ones the player has not
    /// earned tinted back and a lock badge at their terminus.
    /// </summary>
    /// <remarks>
    /// This is the one element allowed to name several lines' colours at once, because drawing them together is its
    /// entire job (work order 4.1). It reads each colour from that line's own <see cref="LineDefinition"/>, never
    /// from a constant.
    ///
    /// **Only lines with content are drawn** (D10). v1 ships one line, and a map showing it beside four empty
    /// promises would read as a game that is four-fifths missing rather than one line long.
    /// </remarks>
    public class NetworkMapElement : VisualElement
    {
        /// <summary>Raised with the line index when an open line is tapped.</summary>
        public event Action<int> LineClicked;

        private NetworkDefinition _network;
        private Func<int, bool> _unlocked;
        private float _padding = 72f;

        public float HitRadius { get; set; } = 60f;

        public NetworkMapElement()
        {
            AddToClassList("network-map");
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
            RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        public float Padding
        {
            get => _padding;
            set { _padding = value; MarkDirtyRepaint(); }
        }

        public void SetNetwork(NetworkDefinition network, Func<int, bool> unlocked = null)
        {
            _network = network;
            _unlocked = unlocked;
            MarkDirtyRepaint();
        }

        public void Refresh() => MarkDirtyRepaint();

        /// <summary>The lines worth drawing, as indices into the network, in draw order.</summary>
        private List<int> DrawnLines()
        {
            var drawn = new List<int>();
            if (_network == null) return drawn;
            for (var i = 0; i < _network.LineCount; i++)
            {
                var line = _network.Line(i);
                if (line != null && line.HasContent && line.MapNodes.Count > 1) drawn.Add(i);
            }

            return drawn;
        }

        private bool TryGetTransform(out Vector2 offset, out float scale)
        {
            offset = Vector2.zero;
            scale = 1f;

            var rect = contentRect;
            var lines = DrawnLines();
            if (lines.Count == 0 || rect.width <= 0f || rect.height <= 0f) return false;

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var index in lines)
                foreach (var node in _network.Line(index).MapNodes)
                {
                    min = Vector2.Min(min, node);
                    max = Vector2.Max(max, node);
                }

            var span = max - min;
            var available = new Vector2(rect.width - _padding * 2f, rect.height - _padding * 2f);
            if (available.x <= 0f || available.y <= 0f) return false;

            var sx = span.x > 0.0001f ? available.x / span.x : float.MaxValue;
            var sy = span.y > 0.0001f ? available.y / span.y : float.MaxValue;
            scale = Mathf.Min(sx, sy);
            if (float.IsInfinity(scale) || scale == float.MaxValue) scale = 1f;

            var drawnSpan = span * scale;
            offset = new Vector2(
                rect.x + (rect.width - drawnSpan.x) * 0.5f - min.x * scale,
                rect.y + (rect.height - drawnSpan.y) * 0.5f - min.y * scale);
            return true;
        }

        private void Draw(MeshGenerationContext context)
        {
            if (!TryGetTransform(out var offset, out var scale)) return;

            var painter = context.painter2D;
            var size = Mathf.Min(contentRect.width, contentRect.height);
            var activeWidth = Mathf.Max(6f, size * 0.042f);
            var inactiveWidth = activeWidth * 0.79f;   // section 6: 26-30 active against 22 inactive

            foreach (var index in DrawnLines())
            {
                var line = _network.Line(index);
                var open = _unlocked == null || _unlocked(index);
                var nodes = line.MapNodes;

                // A locked line is tinted towards the paper rather than greyed flat, so its identity is still
                // legible - the player should be able to see which line they are working towards.
                var colour = open ? line.Color : Color.Lerp(Palette.Paper, line.Color, 0.25f);

                painter.strokeColor = colour;
                painter.lineWidth = open ? activeWidth : inactiveWidth;
                painter.lineCap = LineCap.Round;
                painter.lineJoin = LineJoin.Round;
                painter.BeginPath();
                painter.MoveTo(offset + nodes[0] * scale);
                for (var i = 1; i < nodes.Count; i++) painter.LineTo(offset + nodes[i] * scale);
                if (line.MapShape == MapShape.Loop) painter.ClosePath();
                painter.Stroke();

                if (!open) DrawLockBadge(painter, offset + nodes[nodes.Count - 1] * scale, activeWidth * 1.5f);
            }

            DrawInterchanges(painter, offset, scale, activeWidth);
        }

        /// <summary>A ring at every node two lines share, drawn last so it sits above both.</summary>
        private void DrawInterchanges(Painter2D painter, Vector2 offset, float scale, float strokeWidth)
        {
            foreach (var interchange in _network.Interchanges)
            {
                var a = _network.Line(interchange.lineA);
                if (a == null || interchange.nodeA < 0 || interchange.nodeA >= a.MapNodes.Count) continue;

                var centre = offset + a.MapNodes[interchange.nodeA] * scale;
                painter.fillColor = Palette.Paper;
                painter.BeginPath();
                painter.Arc(centre, strokeWidth * 0.95f, 0f, 360f);
                painter.Fill();

                painter.strokeColor = Palette.Ink;
                painter.lineWidth = strokeWidth * 0.4f;
                painter.BeginPath();
                painter.Arc(centre, strokeWidth * 0.95f, 0f, 360f);
                painter.Stroke();
            }
        }

        /// <summary>The padlock at a closed line's terminus, drawn straight rather than through an Icon child.</summary>
        private static void DrawLockBadge(Painter2D painter, Vector2 centre, float size)
        {
            var body = new Rect(centre.x - size * 0.42f, centre.y - size * 0.10f, size * 0.84f, size * 0.62f);

            painter.fillColor = Palette.Closed;
            painter.BeginPath();
            painter.MoveTo(new Vector2(body.xMin, body.yMin));
            painter.LineTo(new Vector2(body.xMax, body.yMin));
            painter.LineTo(new Vector2(body.xMax, body.yMax));
            painter.LineTo(new Vector2(body.xMin, body.yMax));
            painter.ClosePath();
            painter.Fill();

            painter.strokeColor = Palette.Closed;
            painter.lineWidth = size * 0.16f;
            painter.lineCap = LineCap.Round;
            painter.BeginPath();
            painter.Arc(new Vector2(centre.x, body.yMin), size * 0.27f, 180f, 360f);
            painter.Stroke();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (LineClicked == null || !TryGetTransform(out var offset, out var scale)) return;

            var best = -1;
            var bestDistance = float.MaxValue;
            foreach (var index in DrawnLines())
            {
                if (_unlocked != null && !_unlocked(index)) continue;   // a locked line is not a target
                foreach (var node in _network.Line(index).MapNodes)
                {
                    var distance = Vector2.Distance(offset + node * scale, evt.localPosition);
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = index;
                }
            }

            if (best < 0 || bestDistance > HitRadius) return;
            evt.StopPropagation();
            LineClicked(best);
        }
    }
}
