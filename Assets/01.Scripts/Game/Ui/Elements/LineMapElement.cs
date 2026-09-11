using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// One line drawn from its <see cref="LineDefinition"/>: the route stroked in the line colour, a station marker at
    /// every stop and its name beside it. This is the element that decided the whole UI toolkit choice (7.1) — uGUI
    /// would need a custom mesh or a pre-baked sprite for it, and the map would stop being authorable data.
    /// </summary>
    /// <remarks>
    /// The route is painted; the stations are <b>child elements</b>, not paint. A station carries a tick, a padlock or
    /// its number, and its name sits beside it: text and glyphs that want a real font and a real layout pass, and a
    /// tap target that does not need a distance test. Only the line under them is a thing Painter2D is better at.
    ///
    /// Everything is authored and sized in the mockup's 1080x1920 design space and multiplied by one fitted scale, so
    /// the map reads the same at 9:16 and 9:21 (work order 10) and the numbers below can be compared against the
    /// artboard directly.
    /// </remarks>
    public class LineMapElement : VisualElement
    {
        /// <summary>Raised with the station index when a station is tapped.</summary>
        public event Action<int> StationClicked;

        // ---- the artboard's own measurements, in design-space units ----

        /// <summary>The route's stroke.</summary>
        private const float DesignStroke = 30f;

        /// <summary>A station marker, edge to edge, and the ring around it.</summary>
        private const float DesignNode = 92f;
        private const float DesignNodeRing = 9f;

        /// <summary>The halo behind the station being played.</summary>
        private const float DesignHalo = 132f;

        /// <summary>A station name, and how far its block sits from the marker.</summary>
        private const float DesignLabel = 31f;
        private const float DesignLabelGap = 16f;

        /// <summary>
        /// Room kept around the route for the markers and their names, which reach well outside the node bounding box:
        /// a name column is about 210 units wide and the marker itself another 46 either side.
        /// </summary>
        private const float DesignMarginX = 300f;
        private const float DesignMarginY = 140f;

        private LineDefinition _line;
        private Func<int, StationState> _stateOf;
        private int _selected = -1;
        private readonly List<StationMarker> _markers = new List<StationMarker>();

        public LineMapElement()
        {
            AddToClassList("line-map");
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => Layout());
        }

        /// <summary>Kept for the Line Map Editor's preview, which sets it; the fit now reserves its own margin.</summary>
        public float Padding { get; set; } = 56f;

        /// <summary>The line to draw, and how each of its stations stands with the player.</summary>
        public void SetLine(LineDefinition line, Func<int, StationState> stateOf = null)
        {
            _line = line;
            _stateOf = stateOf;
            Rebuild();
        }

        /// <summary>The station drawn as the player's current choice. -1 for none.</summary>
        public int Selected
        {
            get => _selected;
            set { _selected = value; Dress(); }
        }

        /// <summary>Recomputes the station states without rebuilding the markers.</summary>
        public void Refresh()
        {
            Dress();
            MarkDirtyRepaint();
        }

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

            // The margin is part of what has to fit, in the same units, so the names cannot be scaled off the element.
            var span = max - min + new Vector2(DesignMarginX * 2f, DesignMarginY * 2f);
            if (rect.width <= 0f || rect.height <= 0f) return false;

            var sx = span.x > 0.0001f ? rect.width / span.x : float.MaxValue;
            var sy = span.y > 0.0001f ? rect.height / span.y : float.MaxValue;
            scale = Mathf.Min(sx, sy);
            if (float.IsInfinity(scale) || scale == float.MaxValue) scale = 1f;

            var drawn = span * scale;
            var origin = min - new Vector2(DesignMarginX, DesignMarginY);
            offset = new Vector2(
                rect.x + (rect.width - drawn.x) * 0.5f - origin.x * scale,
                rect.y + (rect.height - drawn.y) * 0.5f - origin.y * scale);
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

        /// <summary>The centre of the node bounding box, which decides whether a name sits left or right of its marker.</summary>
        private Vector2 NodeCentre()
        {
            var nodes = _line.MapNodes;
            var min = nodes[0];
            var max = nodes[0];
            foreach (var node in nodes)
            {
                min = Vector2.Min(min, node);
                max = Vector2.Max(max, node);
            }

            return (min + max) * 0.5f;
        }

        // ---- the markers ----

        /// <summary>Builds one marker per station. Only the line changing costs a rebuild; state changes only re-dress.</summary>
        private void Rebuild()
        {
            foreach (var marker in _markers) marker.RemoveFromHierarchy();
            _markers.Clear();

            var count = _line != null ? _line.StationNodeIndices.Count : 0;
            for (var i = 0; i < count; i++)
            {
                var index = i;
                var marker = new StationMarker(_line.Code, i + 1, NameOf(i));
                marker.Clicked += () => StationClicked?.Invoke(index);
                Add(marker);
                _markers.Add(marker);
            }

            Dress();
            Layout();
            MarkDirtyRepaint();
        }

        private string NameOf(int station)
        {
            var level = _line != null ? _line.Station(station) : null;
            return level != null ? level.DisplayName : "";
        }

        /// <summary>Colours every marker for its state and the current selection.</summary>
        private void Dress()
        {
            var accent = resolvedStyle.color;
            for (var i = 0; i < _markers.Count; i++)
            {
                var state = _stateOf != null ? _stateOf(i) : StationState.Current;
                _markers[i].Dress(state, accent, i == _selected);
            }
        }

        /// <summary>Places every marker over its point on the route. Runs on every geometry change, layout included.</summary>
        private void Layout()
        {
            MarkDirtyRepaint();
            if (_markers.Count == 0 || !TryGetTransform(out var offset, out var scale)) return;

            var points = StationPoints();
            if (points.Count != _markers.Count) return;

            var centre = ToLocal(NodeCentre(), offset, scale);
            var node = DesignNode * scale;
            for (var i = 0; i < _markers.Count; i++)
            {
                var side = Side(points[i], centre, node);
                _markers[i].Place(points[i], side, node, DesignNodeRing * scale, DesignHalo * scale,
                    DesignLabel * scale, DesignLabelGap * scale);
            }
        }

        /// <summary>
        /// Which way a station's name hangs off its marker: out to the side it sits on, or straight above and below at
        /// the two ends of the route, where there is no side to hang from.
        /// </summary>
        private static MarkerSide Side(Vector2 point, Vector2 centre, float node)
        {
            if (Mathf.Abs(point.x - centre.x) < node * 0.5f) return point.y < centre.y ? MarkerSide.Above : MarkerSide.Below;
            return point.x > centre.x ? MarkerSide.Right : MarkerSide.Left;
        }

        // ---- drawing ----

        private void Draw(MeshGenerationContext context)
        {
            if (!TryGetTransform(out var offset, out var scale)) return;

            var painter = context.painter2D;
            var nodes = _line.MapNodes;

            painter.strokeColor = resolvedStyle.color;
            painter.lineWidth = Mathf.Max(2f, DesignStroke * scale);
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();

            if (_line.MapShape == MapShape.Stadium) StadiumPath(painter, offset, scale);
            else
            {
                painter.MoveTo(ToLocal(nodes[0], offset, scale));
                for (var i = 1; i < nodes.Count; i++) painter.LineTo(ToLocal(nodes[i], offset, scale));
                if (_line.MapShape == MapShape.Loop) painter.ClosePath();
            }

            painter.Stroke();
        }

        /// <summary>
        /// The stadium: the node bounding box with its corners rounded by half its shorter side, which turns the two
        /// short ends into semicircles. <see cref="Painter2D.ArcTo"/> is what rounds a corner, so four of them starting
        /// from the middle of one edge give the whole loop.
        /// </summary>
        private void StadiumPath(Painter2D painter, Vector2 offset, float scale)
        {
            var nodes = _line.MapNodes;
            var min = nodes[0];
            var max = nodes[0];
            foreach (var node in nodes)
            {
                min = Vector2.Min(min, node);
                max = Vector2.Max(max, node);
            }

            var a = ToLocal(min, offset, scale);
            var b = ToLocal(max, offset, scale);
            var radius = Mathf.Min(b.x - a.x, b.y - a.y) * 0.5f;
            if (radius <= 0f) return;

            var topLeft = new Vector2(a.x, a.y);
            var topRight = new Vector2(b.x, a.y);
            var bottomRight = new Vector2(b.x, b.y);
            var bottomLeft = new Vector2(a.x, b.y);

            painter.MoveTo(new Vector2((a.x + b.x) * 0.5f, a.y));
            painter.ArcTo(topRight, bottomRight, radius);
            painter.ArcTo(bottomRight, bottomLeft, radius);
            painter.ArcTo(bottomLeft, topLeft, radius);
            painter.ArcTo(topLeft, topRight, radius);
            painter.ClosePath();
        }
    }

    /// <summary>Which way a station's name hangs off its marker.</summary>
    public enum MarkerSide
    {
        Right,
        Left,
        Above,
        Below,
    }

    /// <summary>
    /// One station on the line map: the disc, what it carries (a tick when cleared, a padlock when closed, its number
    /// while it is the one in service) and its name beside it. Absolutely positioned by
    /// <see cref="LineMapElement"/>, which owns the fit.
    /// </summary>
    public sealed class StationMarker : VisualElement
    {
        /// <summary>The halo's opacity behind the station in service, from the artboard.</summary>
        private const float HaloAlpha = 0.55f;

        public event Action Clicked;

        private readonly VisualElement _halo = new VisualElement();
        private readonly VisualElement _disc = new VisualElement();
        private readonly Label _number;
        private readonly Icon _tick = Icons.Tick();
        private readonly Icon _padlock = Icons.Padlock();
        private readonly Label _name;

        public StationMarker(string code, int number, string stationName)
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;

            _halo.style.position = Position.Absolute;
            _halo.pickingMode = PickingMode.Ignore;
            Add(_halo);

            _disc.style.position = Position.Absolute;
            _disc.style.alignItems = Align.Center;
            _disc.style.justifyContent = Justify.Center;
            _disc.RegisterCallback<PointerUpEvent>(evt =>
            {
                evt.StopPropagation();
                Clicked?.Invoke();
            });
            Add(_disc);

            _number = Signage.SignageLabel(number.ToString("00"), 44);
            _disc.Add(_number);
            _disc.Add(_tick);
            _disc.Add(_padlock);

            // The code and the station's own name, stacked the way a platform sign stacks them.
            var label = string.IsNullOrEmpty(stationName)
                ? $"{code}{number:00}"
                : $"{code}{number:00}\n{stationName.ToUpperInvariant()}";
            _name = Signage.SignageLabel(label, 31);
            _name.style.position = Position.Absolute;
            _name.pickingMode = PickingMode.Ignore;
            Add(_name);
        }

        /// <summary>Colours the marker for its state. The accent is the active line's colour, read from the map.</summary>
        public void Dress(StationState state, Color accent, bool selected)
        {
            var cleared = state == StationState.Cleared;
            var closed = state == StationState.Closed;

            _disc.style.backgroundColor = cleared ? accent : Palette.Paper;
            _disc.style.borderTopColor = _disc.style.borderRightColor = _disc.style.borderBottomColor =
                _disc.style.borderLeftColor = closed ? Palette.ClosedLight : cleared ? Palette.Ink : accent;

            _tick.style.display = cleared ? DisplayStyle.Flex : DisplayStyle.None;
            _tick.style.color = Palette.Ink;
            _padlock.style.display = closed ? DisplayStyle.Flex : DisplayStyle.None;
            _padlock.style.color = Palette.Closed;
            _number.style.display = cleared || closed ? DisplayStyle.None : DisplayStyle.Flex;
            _number.style.color = Palette.Ink;

            // The halo marks the one station the card is showing, which is the one in service unless the player moves.
            var halo = selected && !closed;
            _halo.style.display = halo ? DisplayStyle.Flex : DisplayStyle.None;
            _halo.style.backgroundColor = new Color(accent.r, accent.g, accent.b, HaloAlpha);

            _name.style.color = closed ? Palette.Closed : cleared ? Palette.Ink : Palette.Ink;
        }

        /// <summary>Centres the marker on a point and sizes everything from the fitted scale.</summary>
        public void Place(Vector2 point, MarkerSide side, float diameter, float ring, float halo, float labelSize, float gap)
        {
            style.left = point.x;
            style.top = point.y;
            style.width = 0;
            style.height = 0;

            Round(_disc, diameter);
            _disc.style.left = -diameter * 0.5f;
            _disc.style.top = -diameter * 0.5f;
            _disc.style.borderTopWidth = _disc.style.borderRightWidth =
                _disc.style.borderBottomWidth = _disc.style.borderLeftWidth = ring;

            Round(_halo, halo);
            _halo.style.left = -halo * 0.5f;
            _halo.style.top = -halo * 0.5f;

            var glyph = diameter * 0.5f;
            foreach (var icon in new VisualElement[] { _tick, _padlock })
            {
                icon.style.width = glyph;
                icon.style.height = glyph;
            }

            _number.style.fontSize = labelSize * 1.42f;
            _name.style.fontSize = labelSize;
            _name.style.letterSpacing = labelSize * 0.05f;

            var out_ = diameter * 0.5f + gap;
            switch (side)
            {
                case MarkerSide.Right:
                    _name.style.left = out_;
                    _name.style.top = -labelSize * 1.1f;
                    _name.style.unityTextAlign = TextAnchor.MiddleLeft;
                    break;
                case MarkerSide.Left:
                    _name.style.right = out_;
                    _name.style.left = StyleKeyword.Auto;
                    _name.style.top = -labelSize * 1.1f;
                    _name.style.unityTextAlign = TextAnchor.MiddleRight;
                    break;
                case MarkerSide.Above:
                    _name.style.left = -diameter * 2f;
                    _name.style.width = diameter * 4f;
                    _name.style.bottom = out_;
                    _name.style.top = StyleKeyword.Auto;
                    _name.style.unityTextAlign = TextAnchor.MiddleCenter;
                    break;
                default:
                    _name.style.left = -diameter * 2f;
                    _name.style.width = diameter * 4f;
                    _name.style.top = out_;
                    _name.style.unityTextAlign = TextAnchor.MiddleCenter;
                    break;
            }
        }

        private static void Round(VisualElement element, float diameter)
        {
            element.style.width = diameter;
            element.style.height = diameter;
            var radius = diameter * 0.5f;
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }
    }
}
