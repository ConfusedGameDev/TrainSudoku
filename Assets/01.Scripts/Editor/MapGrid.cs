using System.Collections.Generic;
using UnityEngine;

namespace TrainSudoku.Editor
{
    /// <summary>
    /// The lattice map nodes are authored on: a square grid, with segments held to the eight compass directions so a
    /// line reads like a transit diagram rather than a scribble (work order 7.3).
    /// </summary>
    /// <remarks>
    /// Pure maths on purpose. <see cref="LineMapEditorWindow"/> drags pixels; this decides where the node actually
    /// lands, which is the part worth a test.
    /// </remarks>
    public static class MapGrid
    {
        /// <summary>How far off true a segment may be, in map units, and still count as axis-aligned or diagonal.</summary>
        public const float AlignmentTolerance = 0.01f;

        /// <summary>
        /// The eight legal segment directions, as integer steps. Stepping a whole number of grid cells along one of
        /// these from a node that is already on the grid lands on the grid again, which is what lets the snap satisfy
        /// "on the lattice" and "at 45 degrees" at the same time instead of trading one against the other.
        /// </summary>
        public static readonly Vector2Int[] Directions =
        {
            new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1), new Vector2Int(-1, 1),
            new Vector2Int(-1, 0), new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
        };

        /// <summary>The nearest lattice point, ignoring any neighbour.</summary>
        public static Vector2 SnapToGrid(Vector2 point, float grid)
        {
            if (grid <= 0f) return point;
            return new Vector2(Mathf.Round(point.x / grid) * grid, Mathf.Round(point.y / grid) * grid);
        }

        /// <summary>True when the segment runs along one of the eight directions. A zero-length segment does not.</summary>
        public static bool IsAligned(Vector2 from, Vector2 to)
        {
            var delta = to - from;
            if (delta.sqrMagnitude <= AlignmentTolerance * AlignmentTolerance) return false;

            var x = Mathf.Abs(delta.x);
            var y = Mathf.Abs(delta.y);
            if (x <= AlignmentTolerance || y <= AlignmentTolerance) return true;
            return Mathf.Abs(x - y) <= AlignmentTolerance;
        }

        /// <summary>
        /// Where a node dragged to <paramref name="raw"/> lands. Always on the grid; when <paramref name="align"/>
        /// is on it is also always on one of the eight directions out of a neighbour, so a drag cannot author a
        /// segment the validator would then reject.
        /// </summary>
        /// <remarks>
        /// The pull can be long — a pointer 20 degrees off the axis at arm's length is a long way from the ray it
        /// belongs to — and that is the point: the toggle is the escape hatch, not a tolerance. Where a candidate
        /// satisfies <i>both</i> neighbours it wins over a merely closer one, but only while it is within a cell and
        /// a half of the closest legal answer, so a corner never yanks the node across the diagram.
        /// </remarks>
        public static Vector2 Snap(Vector2 raw, Vector2? previous, Vector2? next, float grid, bool align)
        {
            var onGrid = SnapToGrid(raw, grid);
            if (!align || grid <= 0f || (previous == null && next == null)) return onGrid;

            var neighbours = new List<Vector2>(2);
            if (previous.HasValue) neighbours.Add(previous.Value);
            if (next.HasValue) neighbours.Add(next.Value);

            var aligned = Vector2.zero;
            var alignedDistance = float.MaxValue;
            var corner = Vector2.zero;
            var cornerDistance = float.MaxValue;

            foreach (var candidate in Candidates(raw, neighbours, grid))
            {
                var score = Score(candidate, neighbours);
                if (score < 1) continue;

                var distance = Vector2.Distance(candidate, raw);
                if (distance < alignedDistance)
                {
                    aligned = candidate;
                    alignedDistance = distance;
                }

                if (score < 2 || distance >= cornerDistance) continue;
                corner = candidate;
                cornerDistance = distance;
            }

            if (cornerDistance < float.MaxValue && cornerDistance <= alignedDistance + grid * 1.5f) return corner;
            if (alignedDistance < float.MaxValue) return aligned;

            // Nothing legal at all: the node was dropped on a neighbour, and anywhere is better than nowhere.
            return Score(onGrid, neighbours) < 0 ? PushOff(raw, neighbours, grid) : onGrid;
        }

        /// <summary>
        /// A node dragged onto one of its neighbours has nowhere legal to be: put it one cell away, along whichever
        /// direction the pointer is leaning. A zero-length segment is never a useful answer.
        /// </summary>
        private static Vector2 PushOff(Vector2 raw, List<Vector2> neighbours, float grid)
        {
            var anchor = neighbours[0];
            foreach (var neighbour in neighbours)
                if (Vector2.Distance(raw, neighbour) < Vector2.Distance(raw, anchor)) anchor = neighbour;

            var lean = raw - anchor;
            var best = Directions[0];
            var bestDot = float.MinValue;
            foreach (var direction in Directions)
            {
                var dot = Vector2.Dot(lean, ((Vector2)direction).normalized);
                if (dot <= bestDot) continue;
                bestDot = dot;
                best = direction;
            }

            return anchor + (Vector2)best * grid;
        }

        /// <summary>How many neighbour segments a candidate keeps legal; below zero when it lands on a neighbour.</summary>
        private static int Score(Vector2 candidate, List<Vector2> neighbours)
        {
            var score = 0;
            foreach (var neighbour in neighbours)
            {
                if (Vector2.Distance(candidate, neighbour) <= AlignmentTolerance) return -1;
                if (IsAligned(neighbour, candidate)) score++;
            }

            return score;
        }

        /// <summary>
        /// Points worth considering: on each ray out of each neighbour, and where two such rays cross. The crossings
        /// are what let a node satisfy both of its neighbours at once — a corner of the diagram.
        /// </summary>
        private static IEnumerable<Vector2> Candidates(Vector2 raw, List<Vector2> neighbours, float grid)
        {
            foreach (var neighbour in neighbours)
                foreach (var direction in Directions)
                {
                    var step = (Vector2)direction;
                    var count = Mathf.Round(Vector2.Dot(raw - neighbour, step) / (step.sqrMagnitude * grid));
                    if (Mathf.Approximately(count, 0f)) continue;
                    yield return neighbour + step * (count * grid);
                }

            if (neighbours.Count < 2) yield break;

            foreach (var first in Directions)
                foreach (var second in Directions)
                {
                    if (!TryIntersect(neighbours[0], first, neighbours[1], second, out var crossing)) continue;
                    var snapped = SnapToGrid(crossing, grid);
                    if (!IsAligned(neighbours[0], snapped) || !IsAligned(neighbours[1], snapped)) continue;
                    yield return snapped;
                }
        }

        /// <summary>Where the two infinite lines cross. False when they are parallel.</summary>
        private static bool TryIntersect(Vector2 a, Vector2Int da, Vector2 b, Vector2Int db, out Vector2 point)
        {
            point = Vector2.zero;
            float cross = da.x * db.y - da.y * db.x;
            if (Mathf.Abs(cross) < 0.0001f) return false;

            var offset = b - a;
            var t = (offset.x * db.y - offset.y * db.x) / cross;
            point = a + (Vector2)da * t;
            return true;
        }
    }
}
