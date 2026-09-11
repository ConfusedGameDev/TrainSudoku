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
    /// the interchange it hangs off while its lock badge falls away.
    ///
    /// <b>The framing is the progress bar.</b> The map does not show the whole network — it shows the lines the
    /// player has earned plus the one they are working towards, fitted to those and nothing else. Earning a line
    /// widens that set, so the view pans out to take in ground that was not on screen a moment ago and only then
    /// does the new line draw itself in. A player three lines deep sees a legible corner of a city; a player at the
    /// end sees the whole network, and the distance between those two pictures is the progression.
    /// </remarks>
    public class NetworkMapElement : VisualElement
    {
        /// <summary>Raised with the line index when an open line is tapped.</summary>
        public event Action<int> LineClicked;

        /// <summary>The line opening, work order 9. The badge falls away over the first share of it.</summary>
        private const float OpeningDuration = 1.8f;
        private const float LockFallShare = 0.35f;

        /// <summary>The pan-out when a line opens. Runs first; the opening then draws into the space it made.</summary>
        private const float ZoomDuration = 1.2f;

        /// <summary>
        /// The route's stroke in <b>map units</b>, not pixels. It has to be a map measurement: at four lines the
        /// element-derived width this used to use (<c>min(w,h) * 0.042</c>) gave ~45 px against a 150-unit node
        /// pitch drawn at ~112 px, which is the chunky transit-diagram look. Held at 45 px while the network grows
        /// to 24 lines, the same stroke ends up wider than the gap between neighbouring stations and the map reads
        /// as a blob. Scaling it with the fit keeps the ratio the diagram was drawn at.
        /// </summary>
        private const float DesignStroke = 54f;

        /// <summary>...but never thinner than this, or a far-zoomed line disappears.</summary>
        private const float MinStrokePixels = 5f;

        /// <summary>Screen margin as a share of the element's short side. 0.0667 is the 72 px this was authored at.</summary>
        private const float PaddingShare = 0.0667f;

        /// <summary>Tap radius in map units: half the 150 node pitch, so two neighbours never both answer a tap.</summary>
        private const float HitMapRadius = 75f;

        private NetworkDefinition _network;
        private Func<int, bool> _unlocked;
        private float _padding = -1f;
        private int _openingLine = -1;
        private float _openingProgress = 1f;

        /// <summary>
        /// The lines on screen, and the smaller set they were a moment ago. The fit is computed from
        /// <see cref="_revealed"/>; while <see cref="_previousRevealed"/> is set the draw interpolates between the
        /// two fits, which is the pan-out. Lists rather than cached pixel transforms, so a layout change mid-tween
        /// re-fits both ends instead of animating towards a stale rectangle.
        /// </summary>
        private List<int> _revealed;
        private List<int> _previousRevealed;
        private float _zoomProgress = 1f;

        /// <summary>An opening asked for while the pan-out is still running. It waits its turn.</summary>
        private int _pendingOpening = -1;

        /// <summary>
        /// The running tweens. Held rather than discarded because <c>NetworkScreen.Refresh</c> fires on every state
        /// change: two in quick succession would otherwise leave two schedulers writing the same progress field, and
        /// the first one's completion would clear the second's state from under it.
        /// </summary>
        private IVisualElementScheduledItem _zoomTween;
        private IVisualElementScheduledItem _openingTween;

        /// <summary>A tap this far from a node still counts, however far out the map is zoomed.</summary>
        public float MinHitPixels { get; set; } = 28f;

        public NetworkMapElement()
        {
            AddToClassList("network-map");
            // The pan-out draws the new revealed set through a transform that is still partway from the old fit, so
            // for those 1.2 seconds the incoming line sits outside the element. Without this it paints over the
            // signage band and the legend on its way in.
            style.overflow = Overflow.Hidden;
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<DetachFromPanelEvent>(_ => Settle());
        }

        /// <summary>
        /// Ends every animation where it was heading. A tween runs on the element's own scheduler, which stops when
        /// the element leaves the panel — so a screen rebuilt mid-pan would otherwise never run the completion, and
        /// the line waiting on it would stay pending and never be drawn again.
        /// </summary>
        private void Settle()
        {
            _zoomTween?.Pause();
            _openingTween?.Pause();
            _zoomTween = null;
            _openingTween = null;
            _zoomProgress = 1f;
            _previousRevealed = null;
            _openingProgress = 1f;
            _openingLine = -1;
            _pendingOpening = -1;
        }

        /// <summary>Screen margin in pixels. Negative derives it from the element, which is the default.</summary>
        public float Padding
        {
            get => _padding;
            set { _padding = value; MarkDirtyRepaint(); }
        }

        public void SetNetwork(NetworkDefinition network, Func<int, bool> unlocked = null)
        {
            // A different network is a different world, not a progression step: drop the framing rather than pan
            // between two unrelated maps.
            if (!ReferenceEquals(_network, network))
            {
                _revealed = null;
                _previousRevealed = null;
                _zoomProgress = 1f;
            }

            _network = network;
            _unlocked = unlocked;
            MarkDirtyRepaint();
        }

        /// <summary>
        /// Re-reads which lines are revealed and, when that set has grown since the last look, pans out to take in
        /// the new one. The first look of a session commits its framing without a tween — there is no "since" to
        /// animate from, and a player returning to the map should not be shown the whole reveal again.
        /// </summary>
        public void Refresh()
        {
            var revealed = RevealedLines();
            if (_revealed != null && revealed.Count > _revealed.Count)
            {
                _previousRevealed = _revealed;
                _revealed = revealed;
                StartZoom();
            }
            else
            {
                _revealed = revealed;
            }

            MarkDirtyRepaint();
        }

        /// <summary>
        /// Draws a newly earned line onto the map: outward from its interchange, over 1.8 seconds, with the lock
        /// badge falling away as it goes. A line with no interchange — the first line of a network — opens from its
        /// first node instead. Held back until any pan-out has finished, so the line reaches into ground the player
        /// has already watched appear.
        /// </summary>
        public void PlayOpening(int lineIndex)
        {
            if (_zoomProgress < 1f)
            {
                _pendingOpening = lineIndex;
                return;
            }

            StartOpening(lineIndex);
        }

        private void StartOpening(int lineIndex)
        {
            _openingTween?.Pause();
            _openingLine = lineIndex;
            _openingProgress = 0f;
            _openingTween = Motion.Play(this, OpeningDuration, t =>
            {
                _openingProgress = t;
                MarkDirtyRepaint();
            }, () =>
            {
                _openingTween = null;
                _openingLine = -1;
                _openingProgress = 1f;
                MarkDirtyRepaint();
            });
        }

        private void StartZoom()
        {
            _zoomTween?.Pause();
            _zoomProgress = 0f;
            _zoomTween = Motion.Play(this, ZoomDuration, t =>
            {
                _zoomProgress = t;
                MarkDirtyRepaint();
            }, () =>
            {
                _zoomTween = null;
                _zoomProgress = 1f;
                _previousRevealed = null;
                MarkDirtyRepaint();
                if (_pendingOpening < 0) return;
                var line = _pendingOpening;
                _pendingOpening = -1;
                StartOpening(line);
            });
        }

        /// <summary>
        /// The lines on screen: every one the player has earned, plus the first they have not. That one closed line
        /// is drawn in the washed tint with its badge — it is what the player is working towards. Everything past it
        /// is not drawn at all, which is what leaves the map room to grow.
        /// </summary>
        /// <remarks>
        /// Unlocking runs in array order (<c>GameFlow.IsLineUnlocked</c> opens line <i>n</i> off line <i>n-1</i>),
        /// so the open lines are always a prefix and this can stop at the first closed one. With no unlock
        /// predicate — a preview, or a test — everything with content is revealed.
        /// </remarks>
        private List<int> RevealedLines()
        {
            var revealed = new List<int>();
            if (_network == null) return revealed;
            for (var i = 0; i < _network.LineCount; i++)
            {
                var line = _network.Line(i);
                if (line == null || !line.HasContent || line.MapNodes.Count <= 1) continue;
                revealed.Add(i);
                if (_unlocked != null && !_unlocked(i)) break;
            }

            return revealed;
        }

        /// <summary>The revealed lines, falling back to a fresh read for a draw that arrives before any refresh.</summary>
        private List<int> Revealed() => _revealed ?? (_revealed = RevealedLines());

        /// <summary>
        /// The lines currently on the map, so the chrome around it — the legend — can say the same thing the
        /// drawing does rather than listing a network the player cannot see.
        /// </summary>
        public IReadOnlyList<int> VisibleLines => Revealed();

        /// <summary>
        /// The transform in force this frame: the fit over the revealed lines, or — while a pan-out runs — a point
        /// between that and the fit over the smaller set it grew from.
        /// </summary>
        private bool TryGetTransform(out Vector2 offset, out float scale)
        {
            offset = Vector2.zero;
            scale = 1f;
            if (!TryFit(Revealed(), out var centre, out scale)) return false;

            if (_zoomProgress < 1f && _previousRevealed != null &&
                TryFit(_previousRevealed, out var fromCentre, out var fromScale) && fromScale > 0f)
            {
                var t = Motion.EaseOut(_zoomProgress);
                // Zoom is a ratio, not a distance. Over a four-fold pan-out a straight lerp on scale spends most of
                // its time near the far end and reads as stopping early; interpolating the ratio keeps the apparent
                // speed even. The map centre is lerped in map space and the offset derived from both afterwards, so
                // the two interpolations cannot disagree and slide the map sideways.
                scale = fromScale * Mathf.Pow(scale / fromScale, t);
                centre = Vector2.Lerp(fromCentre, centre, t);
            }

            offset = contentRect.center - centre * scale;
            return true;
        }

        /// <summary>
        /// Fits the given lines' nodes into the element, preserving aspect. Reports the fit as the map-space point
        /// that lands in the middle plus a scale, rather than a pixel offset, because those are the two quantities
        /// that interpolate sensibly between one framing and the next.
        /// </summary>
        private bool TryFit(List<int> lines, out Vector2 mapCentre, out float scale)
        {
            mapCentre = Vector2.zero;
            scale = 1f;

            var rect = contentRect;
            if (lines == null || lines.Count == 0 || rect.width <= 0f || rect.height <= 0f) return false;

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var index in lines)
                foreach (var node in _network.Line(index).MapNodes)
                {
                    min = Vector2.Min(min, node);
                    max = Vector2.Max(max, node);
                }

            var span = max - min;
            mapCentre = (min + max) * 0.5f;

            var padding = _padding >= 0f ? _padding : Mathf.Min(rect.width, rect.height) * PaddingShare;
            var available = new Vector2(rect.width - padding * 2f, rect.height - padding * 2f);
            if (available.x <= 0f || available.y <= 0f) return false;

            var sx = span.x > 0.0001f ? available.x / span.x : float.MaxValue;
            var sy = span.y > 0.0001f ? available.y / span.y : float.MaxValue;
            scale = Mathf.Min(sx, sy);
            if (float.IsInfinity(scale) || scale == float.MaxValue) scale = 1f;
            return true;
        }

        private void Draw(MeshGenerationContext context)
        {
            if (!TryGetTransform(out var offset, out var scale)) return;

            var painter = context.painter2D;
            var activeWidth = Mathf.Max(MinStrokePixels, DesignStroke * scale);
            var inactiveWidth = activeWidth * 0.79f;   // section 6: 26-30 active against 22 inactive

            foreach (var index in Revealed())
            {
                var line = _network.Line(index);

                // A line waiting for its opening keeps the closed look it had a moment ago — the washed tint and its
                // badge — for the length of the pan-out. It was already on the map as the player's next target, so
                // hiding it here would blink it out and back; it changes hands when the opening actually starts.
                var open = (_unlocked == null || _unlocked(index)) && index != _pendingOpening;
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
                    if (line.MapShape == MapShape.Stadium) StadiumPath(painter, points);
                    else
                    {
                        painter.MoveTo(points[0]);
                        for (var i = 1; i < points.Length; i++) painter.LineTo(points[i]);
                        if (line.MapShape == MapShape.Loop) painter.ClosePath();
                    }

                    painter.Stroke();
                }

                DrawStations(painter, line, points, open, activeWidth);

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

        /// <summary>
        /// A dot at every station, laid along the line it belongs to. Without them a line is a bare stroke and the
        /// map says nothing about how far it runs; with them the length of a line reads as the number of stops it has.
        /// A locked line keeps its dots in the same washed tint as its stroke, so it reads as dimmed rather than as
        /// a different kind of thing.
        /// </summary>
        private static void DrawStations(Painter2D painter, LineDefinition line, Vector2[] points, bool open, float strokeWidth)
        {
            var indices = line.StationNodeIndices;
            if (indices.Count == 0) return;

            painter.fillColor = open ? Palette.Paper : Color.Lerp(Palette.Paper, Palette.ClosedLight, 0.4f);
            foreach (var node in indices)
            {
                if (node < 0 || node >= points.Length) continue;
                painter.BeginPath();
                painter.Arc(points[node], strokeWidth * 0.28f, 0f, 360f);
                painter.Fill();
            }
        }

        /// <summary>
        /// A ring at every node two lines share, drawn last so it sits above both. An interchange is only drawn once
        /// both its lines are on the map: a ring hanging off open air would promise a line the player cannot see.
        /// </summary>
        private void DrawInterchanges(Painter2D painter, Vector2 offset, float scale, float strokeWidth)
        {
            var revealed = Revealed();
            foreach (var interchange in _network.Interchanges)
            {
                if (!revealed.Contains(interchange.lineA) || !revealed.Contains(interchange.lineB)) continue;

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
        /// <summary>
        /// A stadium line, drawn the way <see cref="LineMapElement"/> draws it: the nodes' bounding box with its
        /// corners rounded by half the shorter side, which turns the two short ends into semicircles. Without this a
        /// closed curve is strung out as a jagged open polyline with a gap where it should meet itself.
        /// </summary>
        private static void StadiumPath(Painter2D painter, Vector2[] points)
        {
            var min = points[0];
            var max = points[0];
            foreach (var point in points)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            var radius = Mathf.Min(max.x - min.x, max.y - min.y) * 0.5f;
            if (radius <= 0f)
            {
                painter.MoveTo(points[0]);
                for (var i = 1; i < points.Length; i++) painter.LineTo(points[i]);
                return;
            }

            var topLeft = new Vector2(min.x, min.y);
            var topRight = new Vector2(max.x, min.y);
            var bottomRight = new Vector2(max.x, max.y);
            var bottomLeft = new Vector2(min.x, max.y);

            painter.MoveTo(new Vector2((min.x + max.x) * 0.5f, min.y));
            painter.ArcTo(topRight, bottomRight, radius);
            painter.ArcTo(bottomRight, bottomLeft, radius);
            painter.ArcTo(bottomLeft, topLeft, radius);
            painter.ArcTo(topLeft, topRight, radius);
            painter.ClosePath();
        }

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
            foreach (var index in Revealed())
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

            // The grab radius is a map measurement, so it shrinks with the fit and a zoomed-out map cannot hand a
            // tap to the wrong line — but never below a thumb's worth of pixels.
            var radius = Mathf.Max(MinHitPixels, HitMapRadius * scale);
            if (best < 0 || bestDistance > radius) return;
            evt.StopPropagation();
            LineClicked(best);
        }
    }
}
