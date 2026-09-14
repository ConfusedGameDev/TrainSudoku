using System.Collections.Generic;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The flat shapes printed on the platform's map card (XR-PRD 6.2): strokes, dots, rings, stars and the padlock.
    /// Every one lies in the XZ plane facing up, one unit per cell like the board, wound so that
    /// <c>cross(p1 - p0, p2 - p0)</c> points up: the way the built-in primitives face, so none is culled from above.
    /// </summary>
    internal sealed class XRMapMesh
    {
        private const int CircleSegments = 28;

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<int> _triangles = new List<int>();
        private readonly float _height;

        /// <param name="height">How far above the card's top the shapes are printed, in cells.</param>
        public XRMapMesh(float height = 0f) => _height = height;

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (_vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            var normals = new Vector3[_vertices.Count];
            for (var i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.SetNormals(normals);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A stroke along a polyline of (x, z) points: a quad per segment and a disc at every point, which rounds the joins and the ends.</summary>
        public XRMapMesh Stroke(IReadOnlyList<Vector2> points, float width)
        {
            var half = width / 2f;
            for (var i = 0; i + 1 < points.Count; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                var along = b - a;
                if (along.sqrMagnitude < 1e-10f) continue;
                var side = new Vector2(-along.y, along.x).normalized * half;
                Quad(a + side, b + side, b - side, a - side);
            }

            foreach (var point in points) Disc(point, half);
            return this;
        }

        /// <summary>The share of a stroke from its start that <paramref name="fraction"/> of its length covers.</summary>
        public static List<Vector2> Portion(IReadOnlyList<Vector2> points, float fraction)
        {
            var result = new List<Vector2>();
            if (points.Count == 0) return result;
            var total = 0f;
            for (var i = 0; i + 1 < points.Count; i++) total += Vector2.Distance(points[i], points[i + 1]);
            var target = total * Mathf.Clamp01(fraction);

            result.Add(points[0]);
            var drawn = 0f;
            for (var i = 0; i + 1 < points.Count; i++)
            {
                var length = Vector2.Distance(points[i], points[i + 1]);
                if (drawn + length <= target)
                {
                    result.Add(points[i + 1]);
                    drawn += length;
                    continue;
                }

                if (length > 0f) result.Add(Vector2.Lerp(points[i], points[i + 1], (target - drawn) / length));
                break;
            }

            return result;
        }

        public XRMapMesh Disc(Vector2 centre, float radius)
        {
            var first = _vertices.Count;
            _vertices.Add(At(centre));
            for (var k = 0; k <= CircleSegments; k++) _vertices.Add(At(centre + Polar(radius, 2f * Mathf.PI * k / CircleSegments)));
            // Centre, then the rim clockwise seen from above (the next angle before this one) faces up.
            for (var k = 0; k < CircleSegments; k++) Triangle(first, first + 2 + k, first + 1 + k);
            return this;
        }

        /// <summary>A ring, or a share of one running anticlockwise from <paramref name="startDegrees"/> (0 is +X, 90 is +Z).</summary>
        public XRMapMesh Ring(Vector2 centre, float inner, float outer, float startDegrees = 0f, float sweepDegrees = 360f)
        {
            var segments = Mathf.Max(2, Mathf.CeilToInt(CircleSegments * sweepDegrees / 360f));
            var first = _vertices.Count;
            for (var k = 0; k <= segments; k++)
            {
                var angle = (startDegrees + sweepDegrees * k / segments) * Mathf.Deg2Rad;
                _vertices.Add(At(centre + Polar(outer, angle)));
                _vertices.Add(At(centre + Polar(inner, angle)));
            }

            for (var k = 0; k < segments; k++)
            {
                int outerA = first + 2 * k, innerA = outerA + 1, outerB = outerA + 2, innerB = outerA + 3;
                Triangle(outerA, innerA, innerB);
                Triangle(outerA, innerB, outerB);
            }

            return this;
        }

        /// <summary>A five-pointed star, one point towards +Z: the far side, which reads as up from the near edge.</summary>
        public XRMapMesh Star(Vector2 centre, float outer, float inner = -1f)
        {
            if (inner < 0f) inner = outer * 0.45f;
            var first = _vertices.Count;
            _vertices.Add(At(centre));
            for (var k = 0; k <= 10; k++)
                _vertices.Add(At(centre + Polar(k % 2 == 0 ? outer : inner, Mathf.PI / 2f + Mathf.PI * k / 5f)));
            for (var k = 0; k < 10; k++) Triangle(first, first + 2 + k, first + 1 + k);
            return this;
        }

        public XRMapMesh Rect(Vector2 centre, Vector2 size)
        {
            var h = size / 2f;
            return Quad(centre + new Vector2(-h.x, -h.y), centre + new Vector2(-h.x, h.y), centre + new Vector2(h.x, h.y), centre + new Vector2(h.x, -h.y));
        }

        /// <summary>A padlock <paramref name="size"/> across: the body, and the shackle arching over it towards +Z.</summary>
        public XRMapMesh Padlock(Vector2 centre, float size)
        {
            Rect(centre + new Vector2(0f, -size * 0.2f), new Vector2(size * 0.84f, size * 0.62f));
            var shackle = size * 0.27f;
            var bar = size * 0.08f;
            Ring(centre + new Vector2(0f, size * 0.11f), shackle - bar, shackle + bar, 0f, 180f);
            return this;
        }

        /// <summary>Four corners in order round the quad, clockwise seen from above.</summary>
        private XRMapMesh Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var first = _vertices.Count;
            _vertices.Add(At(a));
            _vertices.Add(At(b));
            _vertices.Add(At(c));
            _vertices.Add(At(d));
            Triangle(first, first + 1, first + 2);
            Triangle(first, first + 2, first + 3);
            return this;
        }

        private void Triangle(int a, int b, int c)
        {
            _triangles.Add(a);
            _triangles.Add(b);
            _triangles.Add(c);
        }

        private Vector3 At(Vector2 point) => new Vector3(point.x, _height, point.y);

        private static Vector2 Polar(float radius, float radians) => new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
    }
}
