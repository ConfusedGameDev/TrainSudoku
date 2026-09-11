using System.Collections.Generic;
using TrainSudoku.Game;
using UnityEngine;

namespace TrainSudoku.Editor
{
    /// <summary>
    /// Places new lines on the network map: where each one's nodes sit, and which existing node it hangs off.
    /// </summary>
    /// <remarks>
    /// <b>The map has to survive <see cref="LineMapValidation.ValidateNetwork"/>, and twenty freehand polylines in a
    /// shared plane will not.</b> Two strokes that run over each other are rejected, two nodes in the same place are
    /// rejected unless an interchange is declared, and a node may be claimed by only one interchange — so three lines
    /// can never meet at a point. Searching for a legal arrangement afterwards is the wrong shape of problem; this
    /// makes most of it impossible instead.
    ///
    /// <b>Each line gets a cell of its own.</b> The grid of cells is disjoint by construction and excludes the box the
    /// existing lines already occupy, so no two bodies can overlap and no two bodies can share a node. What is left to
    /// check is the <i>stem</i> — the run from the parent node out to the cell — which crosses open map and is the one
    /// piece of geometry the construction cannot make safe on its own. Every stem is tested against everything already
    /// placed, and a parent that will not work is passed over for the next one.
    ///
    /// <b>Cells are taken nearest-first, and that is the progression.</b> Lines unlock in array order
    /// (<c>GameFlow.IsLineUnlocked</c> opens line <i>n</i> off line <i>n-1</i>), and the network map frames only the
    /// lines revealed so far — so issuing cells outward from the middle is what makes the view widen as the player
    /// earns it, rather than jumping about.
    /// </remarks>
    public static class NetworkMapLayout
    {
        /// <summary>The lattice every node lands on. The shipped lines are authored on this pitch.</summary>
        public const float Grid = 150f;

        /// <summary>
        /// One line's patch of map, five lattice steps square. Big enough for the widest body below with room to
        /// spare, small enough that twenty of them do not push the fully-revealed map past legibility.
        /// </summary>
        public const float Cell = Grid * 4f;

        /// <summary>Nodes per line. Every node is a station, so this is also the station count.</summary>
        public const int NodesPerLine = 9;

        /// <summary>A line, ready to be written onto a <see cref="LineDefinition"/>.</summary>
        public struct PlacedLine
        {
            /// <summary>Which line in the network this map belongs to.</summary>
            public int Line;

            /// <summary>The polyline, on the lattice. Node 0 sits on <see cref="ParentNode"/>.</summary>
            public Vector2[] Nodes;

            /// <summary>The line this one hangs off, as an index into the network.</summary>
            public int ParentLine;

            /// <summary>The parent's node that node 0 shares. One interchange per line, so the network stays a tree.</summary>
            public int ParentNode;
        }

        /// <summary>
        /// Everything already on the map, so a candidate can be checked against it. Keyed by <b>network line
        /// index</b>, not by insertion order: a half-built network has lines with no map yet, and an interchange
        /// that named a position in a compacted list would point at the wrong line.
        /// </summary>
        private sealed class Placed
        {
            public readonly List<int> Lines = new List<int>();
            public readonly List<Vector2[]> Nodes = new List<Vector2[]>();
            public readonly List<MapShape> Shapes = new List<MapShape>();
            public readonly HashSet<(int Line, int Node)> Claimed = new HashSet<(int, int)>();

            public void Add(int line, Vector2[] nodes, MapShape shape)
            {
                Lines.Add(line);
                Nodes.Add(nodes);
                Shapes.Add(shape);
            }
        }

        /// <summary>
        /// Lays out the lines named by <paramref name="targets"/>, which must already be in the network and carry no
        /// map of their own. They are laid in the order given — which is the order they will be unlocked in — each
        /// one hanging off a line laid before it, so the network grows outward as a tree.
        /// </summary>
        /// <remarks>
        /// Returns one entry per target. <b>Fewer entries than targets means the search ran out of legal room</b>;
        /// that is a failure to report, not a shorter network to write, because a line with no map draws nothing.
        /// </remarks>
        public static List<PlacedLine> Place(NetworkDefinition network, IReadOnlyList<int> targets)
        {
            var results = new List<PlacedLine>();
            if (network == null || targets == null || targets.Count == 0) return results;

            var placed = new Placed();
            var coreMin = new Vector2(float.MaxValue, float.MaxValue);
            var coreMax = new Vector2(float.MinValue, float.MinValue);

            for (var i = 0; i < network.LineCount; i++)
            {
                var line = network.Line(i);
                if (line == null || line.MapNodes.Count == 0) continue;
                var nodes = new Vector2[line.MapNodes.Count];
                for (var n = 0; n < nodes.Length; n++)
                {
                    nodes[n] = line.MapNodes[n];
                    coreMin = Vector2.Min(coreMin, nodes[n]);
                    coreMax = Vector2.Max(coreMax, nodes[n]);
                }

                placed.Add(i, nodes, line.MapShape);
            }

            if (placed.Nodes.Count == 0) return results;

            // An interchange already declared has spent its node: a second claim on it is a validation error.
            foreach (var interchange in network.Interchanges)
            {
                placed.Claimed.Add((interchange.lineA, interchange.nodeA));
                placed.Claimed.Add((interchange.lineB, interchange.nodeB));
            }

            var core = new Rect(coreMin, coreMax - coreMin);
            var cells = Cells(core, targets.Count);

            for (var i = 0; i < targets.Count && i < cells.Count; i++)
            {
                var line = TryPlaceOne(placed, cells[i], i);
                if (line == null) break;

                var entry = line.Value;
                entry.Line = targets[i];
                results.Add(entry);
                placed.Add(entry.Line, entry.Nodes, MapShape.Route);
                placed.Claimed.Add((entry.ParentLine, entry.ParentNode));
                placed.Claimed.Add((entry.Line, 0));
            }

            return results;
        }

        // ------------------------------------------------------------------ cells

        /// <summary>
        /// Patches of empty map, nearest the middle first. The core's own box is skipped, and so is a one-cell
        /// margin around it, so a body can never land on top of the lines that are already there.
        /// </summary>
        private static List<Rect> Cells(Rect core, int count)
        {
            var centre = core.center;
            var reserved = new Rect(core.xMin - Grid, core.yMin - Grid, core.width + Grid * 2f, core.height + Grid * 2f);

            // Anchor the lattice of cells to the grid so every cell origin is itself a lattice point.
            var origin = new Vector2(
                Mathf.Round(centre.x / Grid) * Grid - Cell * 0.5f,
                Mathf.Round(centre.y / Grid) * Grid - Cell * 0.5f);

            var found = new List<Rect>();
            for (var ring = 1; ring < 12 && found.Count < count * 3; ring++)
                for (var j = -ring; j <= ring; j++)
                    for (var i = -ring; i <= ring; i++)
                    {
                        // Only the newly reached edge of the square, so cells come out roughly nearest-first.
                        if (Mathf.Max(Mathf.Abs(i), Mathf.Abs(j)) != ring) continue;
                        var cell = new Rect(origin.x + i * Cell, origin.y + j * Cell, Cell, Cell);
                        if (cell.Overlaps(reserved)) continue;
                        found.Add(cell);
                    }

            found.Sort((a, b) =>
                (a.center - centre).sqrMagnitude.CompareTo((b.center - centre).sqrMagnitude));
            return found;
        }

        // ------------------------------------------------------------------ one line

        /// <summary>
        /// Tries every body shape in the cell against every free parent node, nearest parent first, and takes the
        /// first combination that is legal against everything already on the map.
        /// </summary>
        private static PlacedLine? TryPlaceOne(Placed placed, Rect cell, int offset)
        {
            foreach (var body in Bodies(cell, offset))
                foreach (var (parentLine, parentNode, parentPoint) in Parents(placed, cell))
                {
                    var nodes = Route(parentPoint, body);
                    if (nodes == null) continue;
                    if (!IsLegal(nodes, placed, parentLine, parentNode)) continue;
                    return new PlacedLine { Nodes = nodes, ParentLine = parentLine, ParentNode = parentNode };
                }

            return null;
        }

        /// <summary>Free nodes on lines already placed, nearest the cell first — so a line hangs off its neighbour.</summary>
        private static List<(int Line, int Node, Vector2 Point)> Parents(Placed placed, Rect cell)
        {
            var centre = cell.center;
            var parents = new List<(int Line, int Node, Vector2 Point)>();
            for (var slot = 0; slot < placed.Nodes.Count; slot++)
            {
                var line = placed.Lines[slot];
                var nodes = placed.Nodes[slot];
                for (var node = 0; node < nodes.Length; node++)
                {
                    if (placed.Claimed.Contains((line, node))) continue;
                    // The terminus carries the closed line's padlock; a branch there collides with the badge.
                    if (node == nodes.Length - 1) continue;
                    parents.Add((line, node, nodes[node]));
                }
            }

            parents.Sort((a, b) =>
                (a.Point - centre).sqrMagnitude.CompareTo((b.Point - centre).sqrMagnitude));
            return parents;
        }

        /// <summary>
        /// The eight compass steps, in <see cref="MapGrid.Directions"/> order: E, SE, S, SW, W, NW, N, NE. Map y
        /// grows downward, so "S" is down the screen.
        /// </summary>
        private static readonly int[][] Shapes =
        {
            new[] { 0, 0, 2, 4, 4, 2 },   // boustrophedon: across, down, back, down
            new[] { 2, 2, 0, 6, 6, 0 },   // the same turned on its side
            new[] { 0, 1, 2, 3, 4, 6 },   // a hook with two mitred corners
            new[] { 1, 0, 7, 6, 4, 3 },   // a chevron climbing and returning
            new[] { 0, 0, 1, 4, 4, 3 },   // two runs stepped apart by a diagonal
            new[] { 2, 1, 0, 7, 6, 4 },   // a loop back on itself, opened out
            new[] { 0, 2, 0, 2, 4, 4 },   // a staircase, then a run back
            new[] { 1, 1, 7, 7, 4, 4 },   // a shallow V under a straight
        };

        /// <summary>
        /// The body shapes worth trying in a cell, laid about its middle. Every step is one of the eight compass
        /// directions by a whole lattice cell, so the result is on the lattice and at 45 degrees by construction
        /// rather than by a later snap — and anything that folds back over itself is caught by
        /// <see cref="IsLegal"/> rather than having to be reasoned about here.
        /// </summary>
        /// <param name="offset">
        /// Where in the repertoire to start. Twenty lines all taking the first shape that fits would draw the same
        /// motif twenty times, and a map that repeats itself reads as wallpaper rather than as a place.
        /// </param>
        private static IEnumerable<Vector2[]> Bodies(Rect cell, int offset)
        {
            for (var n = 0; n < Shapes.Length; n++)
            {
                var shape = Shapes[(offset + n) % Shapes.Length];
                var sequence = new Vector2[shape.Length];
                for (var i = 0; i < shape.Length; i++)
                {
                    var direction = MapGrid.Directions[shape[i]];
                    sequence[i] = Step(direction.x, direction.y);
                }

                var points = new Vector2[sequence.Length + 1];
                points[0] = Vector2.zero;
                for (var i = 0; i < sequence.Length; i++) points[i + 1] = points[i] + sequence[i];

                // Centre the shape in its cell, then put it back on the lattice.
                var min = points[0];
                var max = points[0];
                foreach (var point in points)
                {
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }

                var shift = MapGrid.SnapToGrid(cell.center - (min + max) * 0.5f, Grid);
                var placed = new Vector2[points.Length];
                for (var i = 0; i < points.Length; i++) placed[i] = points[i] + shift;
                yield return placed;
            }
        }

        private static Vector2 Step(int x, int y) => new Vector2(x * Grid, y * Grid);

        /// <summary>
        /// The whole polyline: the parent node, the run out to the body, then the body. The run is one segment when
        /// the parent already lies on an axis or diagonal through the body's first node, and two — through an elbow —
        /// otherwise. Nine nodes either way, so every node is a station.
        /// </summary>
        private static Vector2[] Route(Vector2 parent, Vector2[] body)
        {
            var entry = body[0];
            var nodes = new List<Vector2> { parent };

            if (!MapGrid.IsAligned(parent, entry))
            {
                var elbow = Elbow(parent, entry);
                if (!MapGrid.IsAligned(parent, elbow) || !MapGrid.IsAligned(elbow, entry)) return null;
                nodes.Add(elbow);
            }

            nodes.AddRange(body);

            // A body is six steps; with an elbow that is nine nodes, without it eight. Pad the short case by
            // carrying the last step one cell further rather than leaving the line a station short.
            while (nodes.Count < NodesPerLine)
            {
                var last = nodes[nodes.Count - 1];
                var previous = nodes[nodes.Count - 2];
                nodes.Add(last + (last - previous));
            }

            return nodes.Count == NodesPerLine ? nodes.ToArray() : null;
        }

        /// <summary>
        /// A corner joining two points with one axis-aligned and one true diagonal segment. Both land on the lattice
        /// when the ends do, which is what keeps the snap and the 45-degree rule satisfiable at the same time.
        /// </summary>
        private static Vector2 Elbow(Vector2 from, Vector2 to)
        {
            var dx = to.x - from.x;
            var dy = to.y - from.y;
            return Mathf.Abs(dx) > Mathf.Abs(dy)
                ? new Vector2(from.x + Mathf.Sign(dx) * (Mathf.Abs(dx) - Mathf.Abs(dy)), from.y)
                : new Vector2(from.x, from.y + Mathf.Sign(dy) * (Mathf.Abs(dy) - Mathf.Abs(dx)));
        }

        // ------------------------------------------------------------------ the check

        /// <summary>
        /// Everything <see cref="LineMapValidation"/> would complain about, asked before the line is committed: every
        /// segment aligned, nothing running over anything, and no node landing on another line's node except the one
        /// shared with the parent, which is the interchange.
        /// </summary>
        private static bool IsLegal(Vector2[] nodes, Placed placed, int parentLine, int parentNode)
        {
            for (var i = 0; i + 1 < nodes.Length; i++)
            {
                if (!MapGrid.IsAligned(nodes[i], nodes[i + 1])) return false;

                // No segment shorter than a lattice step. The elbow is placed from the parent's own position, and a
                // parent that already sits nearly in line with the body leaves it a stub — legal, but drawn as two
                // station dots almost touching. One step is the shortest run that still reads as a run.
                if (Vector2.Distance(nodes[i], nodes[i + 1]) < Grid - 0.01f) return false;

                // From i + 1, not i + 2: the neighbouring segment has to be tested too. Two segments that carry
                // straight on through a node are collinear but share only that node, and SegmentsOverlap already
                // reads them as fine — what this catches is the other case, a run that turns back and retraces the
                // one before it. That is a real overlap, and skipping the neighbour was how it got through.
                for (var j = i + 1; j + 1 < nodes.Length; j++)
                    if (LineMapValidation.SegmentsOverlap(nodes[i], nodes[i + 1], nodes[j], nodes[j + 1])) return false;
            }

            for (var i = 0; i < nodes.Length; i++)
                for (var j = i + 1; j < nodes.Length; j++)
                    if (LineMapValidation.SamePoint(nodes[i], nodes[j])) return false;

            for (var slot = 0; slot < placed.Nodes.Count; slot++)
            {
                var line = placed.Lines[slot];
                var other = placed.Nodes[slot];

                for (var node = 0; node < other.Length; node++)
                    for (var i = 0; i < nodes.Length; i++)
                    {
                        if (!LineMapValidation.SamePoint(nodes[i], other[node])) continue;
                        // The one legal meeting: node 0 on the parent's node, which becomes the interchange.
                        if (i == 0 && line == parentLine && node == parentNode) continue;
                        return false;
                    }

                // A stadium is drawn as a curve fitted to its nodes, so nothing runs between them to collide with.
                if (placed.Shapes[slot] == MapShape.Stadium) continue;

                for (var j = 0; j + 1 < other.Length; j++)
                    for (var i = 0; i + 1 < nodes.Length; i++)
                        if (LineMapValidation.SegmentsOverlap(nodes[i], nodes[i + 1], other[j], other[j + 1]))
                            return false;
            }

            return true;
        }

        /// <summary>Every node carries a station, in route order — which is what the validator asks for.</summary>
        public static int[] StationNodes(int count)
        {
            var indices = new int[count];
            for (var i = 0; i < count; i++) indices[i] = i;
            return indices;
        }
    }
}
