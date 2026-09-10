using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Replays SVG path data into a <see cref="Painter2D"/>. It exists so an icon can hold the <c>d</c> attribute
    /// exactly as it was copied from the source drawing, rather than being hand-translated into painter calls and
    /// quietly drifting from it (work order 7.2).
    /// </summary>
    /// <remarks>
    /// Supports the subset the icon set actually uses: <c>M m L l H h V v C c S s Q q T t A a Z z</c>. Elliptical
    /// arcs are converted through the standard endpoint-to-centre parameterisation; every arc in this UI is circular,
    /// but the ellipse and x-rotation terms are handled anyway so a future icon cannot silently come out wrong.
    /// </remarks>
    public static class VectorPath
    {
        /// <summary>Replays <paramref name="data"/> scaled from a <paramref name="viewBox"/>-square into <paramref name="rect"/>.</summary>
        public static void Replay(Painter2D painter, string data, float viewBox, Rect rect)
        {
            if (painter == null || string.IsNullOrEmpty(data)) return;

            var scale = Mathf.Min(rect.width, rect.height) / viewBox;
            var offset = new Vector2(
                rect.x + (rect.width - viewBox * scale) * 0.5f,
                rect.y + (rect.height - viewBox * scale) * 0.5f);

            Vector2 Map(Vector2 p) => new Vector2(offset.x + p.x * scale, offset.y + p.y * scale);

            var tokens = Tokenise(data);
            var index = 0;
            var current = Vector2.zero;
            var start = Vector2.zero;
            var lastControl = Vector2.zero;
            var previous = '\0';
            var open = false;

            while (index < tokens.Count)
            {
                var command = tokens[index] as string;
                if (command == null)
                {
                    // A repeated coordinate run: it implicitly repeats the previous command (M becomes L).
                    command = previous == 'M' ? "L" : previous == 'm' ? "l" : previous.ToString();
                }
                else index++;

                var c = command[0];
                var relative = char.IsLower(c);
                var upper = char.ToUpperInvariant(c);

                float Next() => index < tokens.Count && tokens[index] is float f ? (float)tokens[index++] : 0f;
                Vector2 NextPoint()
                {
                    var p = new Vector2(Next(), Next());
                    return relative ? current + p : p;
                }

                switch (upper)
                {
                    case 'M':
                        current = NextPoint();
                        start = current;
                        if (open) painter.ClosePath();
                        painter.MoveTo(Map(current));
                        open = true;
                        break;

                    case 'L':
                        current = NextPoint();
                        painter.LineTo(Map(current));
                        break;

                    case 'H':
                        current = new Vector2(relative ? current.x + Next() : Next(), current.y);
                        painter.LineTo(Map(current));
                        break;

                    case 'V':
                        current = new Vector2(current.x, relative ? current.y + Next() : Next());
                        painter.LineTo(Map(current));
                        break;

                    case 'C':
                    {
                        var c1 = NextPoint();
                        var c2 = NextPoint();
                        var to = NextPoint();
                        painter.BezierCurveTo(Map(c1), Map(c2), Map(to));
                        lastControl = c2;
                        current = to;
                        break;
                    }

                    case 'S':
                    {
                        var c1 = upper == 'S' && (previous == 'C' || previous == 'c' || previous == 'S' || previous == 's')
                            ? current * 2f - lastControl
                            : current;
                        var c2 = NextPoint();
                        var to = NextPoint();
                        painter.BezierCurveTo(Map(c1), Map(c2), Map(to));
                        lastControl = c2;
                        current = to;
                        break;
                    }

                    case 'Q':
                    {
                        var control = NextPoint();
                        var to = NextPoint();
                        painter.QuadraticCurveTo(Map(control), Map(to));
                        lastControl = control;
                        current = to;
                        break;
                    }

                    case 'T':
                    {
                        var control = previous == 'Q' || previous == 'q' || previous == 'T' || previous == 't'
                            ? current * 2f - lastControl
                            : current;
                        var to = NextPoint();
                        painter.QuadraticCurveTo(Map(control), Map(to));
                        lastControl = control;
                        current = to;
                        break;
                    }

                    case 'A':
                    {
                        var rx = Next();
                        var ry = Next();
                        var rotation = Next();
                        var largeArc = Next() != 0f;
                        var sweep = Next() != 0f;
                        var to = NextPoint();
                        Arc(painter, current, to, rx, ry, rotation, largeArc, sweep, Map);
                        current = to;
                        break;
                    }

                    case 'Z':
                        painter.ClosePath();
                        current = start;
                        open = false;
                        break;

                    default:
                        return; // unknown command: stop rather than draw nonsense
                }

                previous = c;
            }
        }

        /// <summary>
        /// Endpoint to centre parameterisation (SVG spec, F.6.5), then flattened into short line segments. Painter2D
        /// has its own arc call, but it takes a circle; going through segments keeps ellipses and x-rotation correct
        /// and costs nothing at icon size.
        /// </summary>
        private static void Arc(Painter2D painter, Vector2 from, Vector2 to, float rx, float ry, float rotationDegrees,
            bool largeArc, bool sweep, Func<Vector2, Vector2> map)
        {
            if (Mathf.Approximately(rx, 0f) || Mathf.Approximately(ry, 0f) || from == to)
            {
                painter.LineTo(map(to));
                return;
            }

            rx = Mathf.Abs(rx);
            ry = Mathf.Abs(ry);
            var phi = rotationDegrees * Mathf.Deg2Rad;
            var cosPhi = Mathf.Cos(phi);
            var sinPhi = Mathf.Sin(phi);

            var dx = (from.x - to.x) * 0.5f;
            var dy = (from.y - to.y) * 0.5f;
            var x1 = cosPhi * dx + sinPhi * dy;
            var y1 = -sinPhi * dx + cosPhi * dy;

            // Scale the radii up if they are too small to span the chord.
            var lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
            if (lambda > 1f)
            {
                var root = Mathf.Sqrt(lambda);
                rx *= root;
                ry *= root;
            }

            var numerator = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
            var denominator = rx * rx * y1 * y1 + ry * ry * x1 * x1;
            var factor = denominator <= 0f ? 0f : Mathf.Sqrt(Mathf.Max(0f, numerator / denominator));
            if (largeArc == sweep) factor = -factor;

            var cx1 = factor * rx * y1 / ry;
            var cy1 = -factor * ry * x1 / rx;
            var cx = cosPhi * cx1 - sinPhi * cy1 + (from.x + to.x) * 0.5f;
            var cy = sinPhi * cx1 + cosPhi * cy1 + (from.y + to.y) * 0.5f;

            float AngleOf(float ux, float uy) => Mathf.Atan2(uy, ux);
            var startAngle = AngleOf((x1 - cx1) / rx, (y1 - cy1) / ry);
            var endAngle = AngleOf((-x1 - cx1) / rx, (-y1 - cy1) / ry);
            var sweepAngle = endAngle - startAngle;

            if (!sweep && sweepAngle > 0f) sweepAngle -= 2f * Mathf.PI;
            else if (sweep && sweepAngle < 0f) sweepAngle += 2f * Mathf.PI;

            var steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(sweepAngle) / (Mathf.PI / 16f)), 2, 64);
            for (var i = 1; i <= steps; i++)
            {
                var angle = startAngle + sweepAngle * i / steps;
                var px = cx + rx * Mathf.Cos(angle) * cosPhi - ry * Mathf.Sin(angle) * sinPhi;
                var py = cy + rx * Mathf.Cos(angle) * sinPhi + ry * Mathf.Sin(angle) * cosPhi;
                painter.LineTo(map(new Vector2(px, py)));
            }
        }

        /// <summary>Splits path data into command letters (string) and numbers (float).</summary>
        private static List<object> Tokenise(string data)
        {
            var tokens = new List<object>();
            var i = 0;
            while (i < data.Length)
            {
                var c = data[i];
                if (char.IsWhiteSpace(c) || c == ',') { i++; continue; }

                if (char.IsLetter(c))
                {
                    tokens.Add(c.ToString());
                    i++;
                    continue;
                }

                var startIndex = i;
                if (c == '-' || c == '+') i++;
                while (i < data.Length && (char.IsDigit(data[i]) || data[i] == '.')) i++;
                if (i < data.Length && (data[i] == 'e' || data[i] == 'E'))
                {
                    i++;
                    if (i < data.Length && (data[i] == '-' || data[i] == '+')) i++;
                    while (i < data.Length && char.IsDigit(data[i])) i++;
                }

                if (i == startIndex) { i++; continue; }   // nothing consumed: skip the character
                if (float.TryParse(data.Substring(startIndex, i - startIndex), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var value))
                    tokens.Add(value);
            }

            return tokens;
        }
    }
}
