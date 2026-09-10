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
    ///
    /// It also owns the line opening of work order 9: a line the player has just earned draws itself outward from
    /// the interchange it hangs off while its lock badge falls away. With one line shipping, nothing triggers it in
    /// v1 (D10, D20) — it is here because the second line is the moment it exists for.
    /// </remarks>
    public class NetworkMapElement : VisualElement
    {
        /// <summary>Raised with the line index when an open line is tapped.</summary>
        public event Action<int> LineClicked;

        /// <summary>The line opening, work order 9. The badge falls away over the first share of it.</summary>
        private const float OpeningDuration = 1.8f;
        private const float LockFallShare = 0.35f;

        private NetworkDefinition _network;
        private Func<int, bool> _unlocked;
        private float _padding = 72f;
        private int _openingLine = -1;
        private float _openingProgress = 1f;

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

        /// <summary>
        /// Draws a newly earned line onto the map: outward from its interchange, over 1.8 seconds, with the lock
        /// badge falling away as it goes. A line with no interchange — the first line of a network — opens from its
        /// first node instead.
        /// </summary>
        public void PlayOpening(int lineIndex)
        {
            _openingLine = lineIndex;
            _openingProgress = 0f;
            Motion.Play(this, OpeningDuration, t =>
            {
                _openingProgress = t;
                MarkDirtyRepaint();
            }, () =>
            {
                _openingLine = -1;
                _openingProgress = 1f;
                MarkDirtyRepaint();
            });
        }

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

                var points = new Vector2[nodes.Count];
                for (var i = 0; i < nodes.Count; i++) points[i] = offset + nodes[i] * scale;

                painter.strokeColor = colour;
                painter.lineWidth = open ? activeWidth : inactiveWidth;
                painter.lineCap = LineCap.Round;
                painter.lineJoin = LineJoin.Round;

                if (index == _openingLine)
                {
                    var from = OpeningNode(index);
                    StrokeRun(painter, points, from, 1, _openingProgress);
                    StrokeRun(painter, points, from, -1, _openingProgress);
                }
                else
                {
                    painter.BeginPath();
                    painter.MoveTo(points[0]);
                    for (var i = 1; i < points.Length; i++) painter.LineTo(points[i]);
                    if (line.MapShape == MapShape.Loop) painter.ClosePath();
                    painter.Stroke();
                }

                // The badge is drawn for a closed line, and once more on the way out while the line opens under it.
                if (!open) DrawLockBadge(painter, points[points.Length - 1], activeWidth * 1.5f, Palette.Closed, 0f);
                else if (index == _openingLine && _openingProgress < LockFallShare)
                {
                    var fall = _openingProgress / LockFallShare;
                    var fading = Palette.Closed;
                    fading.a = 1f - fall;
                    DrawLockBadge(painter, points[points.Length - 1], activeWidth * 1.5f, fading, fall * activeWidth * 3f);
                }
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

        /// <summary>
        /// The lines this line runs out from: the node it shares with another line, or its first node when it shares
        /// none. The opening draws outward from there in both directions at once.
        /// </summary>
        private int OpeningNode(int lineIndex)
        {
            if (_network == null) return 0;
            foreach (var interchange in _network.Interchanges)
            {
                if (interchange.lineA == lineIndex) return Mathf.Max(0, interchange.nodeA);
                if (interchange.lineB == lineIndex) return Mathf.Max(0, interchange.nodeB);
            }

            return 0;
        }

        /// <summary>
        /// Strokes a share of the polyline running away from <paramref name="start"/>, measured along its length so
        /// the line grows at an even speed rather than a node at a time.
        /// </summary>
        private static void StrokeRun(Painter2D painter, Vector2[] points, int start, int step, float fraction)
        {
            var total = 0f;
            for (var i = start; i + step >= 0 && i + step < points.Length; i += step)
                total += Vector2.Distance(points[i], points[i + step]);
            if (total <= 0f || fraction <= 0f) return;

            var target = total * Mathf.Clamp01(fraction);
            var drawn = 0f;
            painter.BeginPath();
            painter.MoveTo(points[start]);
            for (var i = start; i + step >= 0 && i + step < points.Length; i += step)
            {
                var a = points[i];
                var b = points[i + step];
                var length = Vector2.Distance(a, b);
                if (drawn + length <= target)
                {
                    painter.LineTo(b);
                    drawn += length;
                    continue;
                }

                painter.LineTo(Vector2.Lerp(a, b, (target - drawn) / length));
                break;
            }

            painter.Stroke();
        }

        /// <summary>The padlock at a closed line's terminus, drawn straight rather than through an Icon child.</summary>
        private static void DrawLockBadge(Painter2D painter, Vector2 centre, float size, Color colour, float drop)
        {
            centre.y += drop;
            var body = new Rect(centre.x - size * 0.42f, centre.y - size * 0.10f, size * 0.84f, size * 0.62f);

            painter.fillColor = colour;
            painter.BeginPath();
            painter.MoveTo(new Vector2(body.xMin, body.yMin));
            painter.LineTo(new Vector2(body.xMax, body.yMin));
            painter.LineTo(new Vector2(body.xMax, body.yMax));
            painter.LineTo(new Vector2(body.xMin, body.yMax));
            painter.ClosePath();
            painter.Fill();

            painter.strokeColor = colour;
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
