using System.Collections.Generic;
using TrainSudoku.Game;
using UnityEngine;

namespace TrainSudoku.Editor
{
    /// <summary>
    /// The four checks the work order (7.3) asks the Line Map Editor for: every station on a node, no overlapping
    /// strokes, every segment on the 45-degree grid, and each interchange shared by exactly two lines.
    /// </summary>
    /// <remarks>
    /// Plain lists in, plain strings out. The window shows them and the EditMode suite asserts on them, which is why
    /// the line check takes the four values it needs rather than a <see cref="LineDefinition"/>: an unsaved edit in
    /// the window is not an asset yet.
    /// </remarks>
    public static class LineMapValidation
    {
        /// <summary>Two points closer than this, in map units, are the same point.</summary>
        private const float Coincident = 0.5f;

        /// <summary>How far a point may sit off a segment's infinite line and still count as lying on it.</summary>
        private const float Collinear = 0.5f;

        /// <summary>One segment of a polyline, kept with its index so a problem can name it.</summary>
        private readonly struct Segment
        {
            public readonly Vector2 A;
            public readonly Vector2 B;
            public readonly int Index;

            public Segment(Vector2 a, Vector2 b, int index)
            {
                A = a;
                B = b;
                Index = index;
            }
        }

        /// <summary>Everything wrong with one line's map. An empty list means it is fit to ship.</summary>
        public static List<string> ValidateLine(
            IReadOnlyList<Vector2> nodes, IReadOnlyList<int> stationNodes, int stationCount, MapShape shape)
        {
            var problems = new List<string>();
            if (nodes == null || nodes.Count < 2)
            {
                problems.Add("The map needs at least two nodes.");
                return problems;
            }

            CheckNodes(nodes, problems);
            // A stadium's nodes are station positions on a curve, not the ends of drawn segments, so the lattice rule
            // has nothing to check: there are no segments between them to be off-grid or to run over one another.
            if (shape != MapShape.Stadium) CheckSegments(Segments(nodes, shape), problems);
            CheckStations(nodes, stationNodes, stationCount, problems);
            return problems;
        }

        /// <summary>The same for a saved line, taking its station count from the levels hanging off it.</summary>
        public static List<string> ValidateLine(LineDefinition line)
        {
            if (line == null) return new List<string> { "No line assigned." };
            return ValidateLine(line.MapNodes, line.StationNodeIndices, line.StationCount, line.MapShape);
        }

        /// <summary>
        /// Every line in the network, plus the checks that only make sense across lines: strokes that lie on top of
        /// one another, and interchanges. Each problem is prefixed with the line it belongs to.
        /// </summary>
        public static List<string> ValidateNetwork(NetworkDefinition network)
        {
            var problems = new List<string>();
            if (network == null)
            {
                problems.Add("No network assigned.");
                return problems;
            }

            for (var i = 0; i < network.LineCount; i++)
            {
                var line = network.Line(i);
                if (line == null)
                {
                    problems.Add($"Line {i} is missing.");
                    continue;
                }

                // A line with no content is not drawn (D10), so an unauthored map on one is not yet a defect.
                if (!line.HasContent && line.MapNodes.Count == 0) continue;

                foreach (var problem in ValidateLine(line)) problems.Add($"{LineLabel(line, i)}: {problem}");
            }

            CheckCrossLineOverlaps(network, problems);
            CheckInterchanges(network, problems);
            return problems;
        }

        /// <summary>How a line is named in a problem: its code, then its id, then its place in the network.</summary>
        public static string LineLabel(LineDefinition line, int index) =>
            !string.IsNullOrEmpty(line.Code) ? line.Code : !string.IsNullOrEmpty(line.Id) ? line.Id : $"Line {index}";

        // ------------------------------------------------------------------ one line

        private static void CheckNodes(IReadOnlyList<Vector2> nodes, List<string> problems)
        {
            for (var i = 0; i < nodes.Count; i++)
                for (var j = i + 1; j < nodes.Count; j++)
                {
                    if (Vector2.Distance(nodes[i], nodes[j]) > Coincident) continue;
                    problems.Add($"Nodes {i} and {j} sit on the same point ({nodes[i].x:0.#}, {nodes[i].y:0.#}).");
                }
        }

        private static void CheckSegments(List<Segment> segments, List<string> problems)
        {
            foreach (var segment in segments)
            {
                if (MapGrid.IsAligned(segment.A, segment.B)) continue;
                problems.Add($"Segment {segment.Index} is neither axis-aligned nor at 45 degrees.");
            }

            for (var i = 0; i < segments.Count; i++)
                for (var j = i + 1; j < segments.Count; j++)
                {
                    if (!Overlaps(segments[i], segments[j])) continue;
                    problems.Add($"Segments {segments[i].Index} and {segments[j].Index} run over each other.");
                }
        }

        private static void CheckStations(
            IReadOnlyList<Vector2> nodes, IReadOnlyList<int> stationNodes, int stationCount, List<string> problems)
        {
            var count = stationNodes?.Count ?? 0;
            if (stationCount > 0 && count != stationCount)
                problems.Add($"The line has {stationCount} station{(stationCount == 1 ? "" : "s")} but {count} " +
                             $"node assignment{(count == 1 ? "" : "s")}.");
            if (count == 0) return;

            var previous = int.MinValue;
            var ascending = true;
            for (var i = 0; i < count; i++)
            {
                var node = stationNodes[i];
                if (node < 0 || node >= nodes.Count)
                {
                    problems.Add($"Station {i + 1} is not on a node (node {node} of {nodes.Count}).");
                    continue;
                }

                for (var j = i + 1; j < count; j++)
                {
                    if (stationNodes[j] != node) continue;
                    problems.Add($"Stations {i + 1} and {j + 1} share node {node}.");
                }

                if (node <= previous) ascending = false;
                previous = node;
            }

            // Stations are the levels in order, so a map that visits them out of order draws a line whose numbers
            // run backwards along the route.
            if (!ascending) problems.Add("Station order does not follow the route: their nodes are not ascending.");
        }

        // ------------------------------------------------------------------ across lines

        private static void CheckCrossLineOverlaps(NetworkDefinition network, List<string> problems)
        {
            for (var i = 0; i < network.LineCount; i++)
            {
                var first = network.Line(i);
                if (first == null || first.MapNodes.Count < 2) continue;
                var firstSegments = Segments(first.MapNodes, first.MapShape);

                for (var j = i + 1; j < network.LineCount; j++)
                {
                    var second = network.Line(j);
                    if (second == null || second.MapNodes.Count < 2) continue;

                    foreach (var a in firstSegments)
                        foreach (var b in Segments(second.MapNodes, second.MapShape))
                        {
                            if (!Overlaps(a, b)) continue;
                            problems.Add($"{LineLabel(first, i)} segment {a.Index} runs over {LineLabel(second, j)} " +
                                         $"segment {b.Index}.");
                        }
                }
            }
        }

        private static void CheckInterchanges(NetworkDefinition network, List<string> problems)
        {
            var claimed = new HashSet<(int line, int node)>();
            var interchanges = network.Interchanges;

            for (var i = 0; i < interchanges.Count; i++)
            {
                var interchange = interchanges[i];
                var a = network.Line(interchange.lineA);
                var b = network.Line(interchange.lineB);
                if (a == null || b == null)
                {
                    problems.Add($"Interchange {i} names a line that does not exist.");
                    continue;
                }

                if (interchange.lineA == interchange.lineB)
                {
                    problems.Add($"Interchange {i} joins {LineLabel(a, interchange.lineA)} to itself.");
                    continue;
                }

                if (interchange.nodeA < 0 || interchange.nodeA >= a.MapNodes.Count ||
                    interchange.nodeB < 0 || interchange.nodeB >= b.MapNodes.Count)
                {
                    problems.Add($"Interchange {i} names a node that does not exist.");
                    continue;
                }

                if (Vector2.Distance(a.MapNodes[interchange.nodeA], b.MapNodes[interchange.nodeB]) > Coincident)
                    problems.Add($"Interchange {i} joins two nodes that are not in the same place.");

                // Two claims on one node means three lines meeting there, which the ring is not drawn for.
                if (!claimed.Add((interchange.lineA, interchange.nodeA)))
                    problems.Add($"Interchange {i} reuses {LineLabel(a, interchange.lineA)} node {interchange.nodeA}.");
                if (!claimed.Add((interchange.lineB, interchange.nodeB)))
                    problems.Add($"Interchange {i} reuses {LineLabel(b, interchange.lineB)} node {interchange.nodeB}.");
            }

            CheckUndeclaredMeetings(network, claimed, problems);
        }

        /// <summary>Two lines touching at a point with no interchange declared: drawn as a crossing, not a station.</summary>
        private static void CheckUndeclaredMeetings(
            NetworkDefinition network, HashSet<(int line, int node)> claimed, List<string> problems)
        {
            for (var i = 0; i < network.LineCount; i++)
            {
                var first = network.Line(i);
                if (first == null) continue;

                for (var j = i + 1; j < network.LineCount; j++)
                {
                    var second = network.Line(j);
                    if (second == null) continue;

                    for (var a = 0; a < first.MapNodes.Count; a++)
                        for (var b = 0; b < second.MapNodes.Count; b++)
                        {
                            if (Vector2.Distance(first.MapNodes[a], second.MapNodes[b]) > Coincident) continue;
                            if (claimed.Contains((i, a)) && claimed.Contains((j, b))) continue;
                            problems.Add($"{LineLabel(first, i)} node {a} and {LineLabel(second, j)} node {b} meet with no " +
                                         "interchange between them.");
                        }
                }
            }
        }

        // ------------------------------------------------------------------ geometry

        private static List<Segment> Segments(IReadOnlyList<Vector2> nodes, MapShape shape)
        {
            var segments = new List<Segment>();
            // A stadium is drawn as a curve fitted to the nodes, so nothing runs between them to cross another line.
            if (shape == MapShape.Stadium) return segments;
            for (var i = 0; i + 1 < nodes.Count; i++) segments.Add(new Segment(nodes[i], nodes[i + 1], i));
            if (shape == MapShape.Loop && nodes.Count > 2)
                segments.Add(new Segment(nodes[nodes.Count - 1], nodes[0], nodes.Count - 1));
            return segments;
        }

        /// <summary>
        /// True when the two segments are collinear and share more than a point. Segments that merely cross are fine:
        /// a diagram is allowed to have a bridge, but not two strokes hiding one another.
        /// </summary>
        private static bool Overlaps(Segment first, Segment second)
        {
            var direction = first.B - first.A;
            var length = direction.magnitude;
            if (length <= Coincident) return false;
            direction /= length;

            if (Mathf.Abs(Cross(direction, (second.B - second.A).normalized)) > 0.001f) return false;
            if (Mathf.Abs(Cross(direction, second.A - first.A)) > Collinear) return false;

            var start = Vector2.Dot(second.A - first.A, direction);
            var end = Vector2.Dot(second.B - first.A, direction);
            if (start > end) (start, end) = (end, start);

            return Mathf.Min(end, length) - Mathf.Max(start, 0f) > Coincident;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
