using System.Collections.Generic;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The board's own generated geometry: the chamfered platform slab, the swept-arch tunnel portal and the erase
    /// ring (work order 5.4 and 9).
    /// Like the track, it is parametric rather than authored art — there is no tile prefab and no mesh asset.
    /// </summary>
    /// <remarks>
    /// <b>Every mesh here spans the same extents as the primitive it replaces</b>, so <c>BoardView.ContentBounds</c>
    /// cannot shift and the camera fit stays where M17 put it. The chamfer only removes material from inside the
    /// unit cube; the ring is a flat disc the camera was already told about through <c>BoardLayout.Height</c>.
    /// </remarks>
    public static class ProceduralBoardMesh
    {
        private const int RingSegments = 64;
        private const int ArchSegments = 12;

        private static Mesh _tile;
        private static Mesh _portal;
        private static float _tileChamferXZ;
        private static float _tileChamferY;

        /// <summary>
        /// A unit cube with its top edges cut back, so a slab catches a highlight along its rim under the 60 degree
        /// pitch instead of reading as a flat rectangle. The two chamfers are given separately because the tile is
        /// scaled hard in Y: a single figure in mesh space would come out as a hairline once flattened.
        /// </summary>
        /// <param name="chamferXZ">How far the top face is inset, as a fraction of the cube's width.</param>
        /// <param name="chamferY">How far down the chamfer starts, as a fraction of the cube's height.</param>
        public static Mesh ChamferedTile(float chamferXZ, float chamferY)
        {
            if (_tile != null && Mathf.Approximately(_tileChamferXZ, chamferXZ) && Mathf.Approximately(_tileChamferY, chamferY))
                return _tile;

            var c = Mathf.Clamp(chamferXZ, 0.001f, 0.45f);
            var d = Mathf.Clamp(chamferY, 0.001f, 0.9f);
            var top = 0.5f;
            var shoulder = 0.5f - d;
            var inner = 0.5f - c;

            var builder = new QuadMeshBuilder();

            // Top face, inset by the chamfer.
            builder.AddQuad(
                new Vector3(-inner, top, -inner), new Vector3(inner, top, -inner),
                new Vector3(inner, top, inner), new Vector3(-inner, top, inner), Vector3.up);

            // Each side of the rim: a straight face up to the shoulder, then the chamfer band into the top face.
            var rim = new[]
            {
                (A: new Vector3(0.5f, 0f, -0.5f), B: new Vector3(0.5f, 0f, 0.5f), Out: Vector3.right),
                (A: new Vector3(0.5f, 0f, 0.5f), B: new Vector3(-0.5f, 0f, 0.5f), Out: Vector3.forward),
                (A: new Vector3(-0.5f, 0f, 0.5f), B: new Vector3(-0.5f, 0f, -0.5f), Out: Vector3.left),
                (A: new Vector3(-0.5f, 0f, -0.5f), B: new Vector3(0.5f, 0f, -0.5f), Out: Vector3.back),
            };

            foreach (var edge in rim)
            {
                var a = edge.A + Vector3.up * shoulder;
                var b = edge.B + Vector3.up * shoulder;
                var ai = new Vector3(edge.A.x * inner * 2f, top, edge.A.z * inner * 2f);
                var bi = new Vector3(edge.B.x * inner * 2f, top, edge.B.z * inner * 2f);
                builder.AddQuad(a, b, bi, ai, (edge.Out + Vector3.up).normalized);
                builder.AddQuad(edge.A - Vector3.up * 0.5f, edge.B - Vector3.up * 0.5f, b, a, edge.Out);
            }

            // The underside is never seen from a pitched camera, but a closed mesh keeps the bounds honest.
            builder.AddQuad(
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, -0.5f), Vector3.down);

            _tile = builder.ToMesh("Platform Slab");
            _tileChamferXZ = chamferXZ;
            _tileChamferY = chamferY;
            return _tile;
        }

        /// <summary>
        /// Rewrites <paramref name="mesh"/> as a flat annulus sector lying in the XZ plane, swept clockwise from
        /// north. This is the erase ring filling round as the press is held (work order 9), so it is rebuilt every
        /// frame into the same mesh rather than allocating one per step.
        /// </summary>
        public static void FillRing(Mesh mesh, float innerRadius, float outerRadius, float fraction)
        {
            if (mesh == null) return;
            mesh.Clear();

            var sweep = Mathf.Clamp01(fraction);
            var steps = Mathf.Max(1, Mathf.CeilToInt(RingSegments * sweep));
            var vertices = new Vector3[(steps + 1) * 2];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[steps * 6];

            for (var i = 0; i <= steps; i++)
            {
                // Clockwise from north, so the ring closes the way a progress dial does.
                var angle = 2f * Mathf.PI * sweep * i / steps;
                var dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                vertices[i * 2] = dir * innerRadius;
                vertices[i * 2 + 1] = dir * outerRadius;
                normals[i * 2] = Vector3.up;
                normals[i * 2 + 1] = Vector3.up;
            }

            for (var i = 0; i < steps; i++)
            {
                var v = i * 2;
                var t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 3;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// A tunnel portal: a unit block with a swept arch cut through it along Z, the opening facing the board.
        /// It replaces the placeholder box in <c>BoardView.BuildTunnel</c> and spans the same unit cube, so the
        /// tunnel ring keeps exactly the extents the camera fit was solved for.
        /// </summary>
        /// <param name="halfWidth">Half the opening, as a fraction of the block's width.</param>
        /// <param name="springLine">Where the arch starts to curve, in the block's own -0.5 to 0.5.</param>
        /// <param name="crown">The top of the arch, in the same range.</param>
        public static Mesh ArchPortal(float halfWidth, float springLine, float crown)
        {
            if (_portal != null) return _portal;

            var w = Mathf.Clamp(halfWidth, 0.05f, 0.49f);
            var spring = Mathf.Clamp(springLine, -0.5f, 0.45f);
            var top = Mathf.Clamp(crown, spring + 0.01f, 0.5f);
            var builder = new QuadMeshBuilder();

            // The arch profile, west to east: a half ellipse standing on the jambs.
            var profile = new Vector2[ArchSegments + 1];
            for (var i = 0; i <= ArchSegments; i++)
            {
                var x = Mathf.Lerp(-w, w, (float)i / ArchSegments);
                var rise = Mathf.Sqrt(Mathf.Max(0f, 1f - (x / w) * (x / w)));
                profile[i] = new Vector2(x, spring + rise * (top - spring));
            }

            foreach (var face in new[] { 0.5f, -0.5f })
            {
                var outward = face > 0f ? Vector3.forward : Vector3.back;

                // The wall either side of the opening, full height.
                builder.AddQuad(
                    new Vector3(-0.5f, -0.5f, face), new Vector3(-w, -0.5f, face),
                    new Vector3(-w, 0.5f, face), new Vector3(-0.5f, 0.5f, face), outward);
                builder.AddQuad(
                    new Vector3(w, -0.5f, face), new Vector3(0.5f, -0.5f, face),
                    new Vector3(0.5f, 0.5f, face), new Vector3(w, 0.5f, face), outward);

                // The spandrel: the wall left over between the arch and the top of the block.
                for (var i = 0; i < ArchSegments; i++)
                {
                    var a = profile[i];
                    var b = profile[i + 1];
                    builder.AddQuad(
                        new Vector3(a.x, a.y, face), new Vector3(b.x, b.y, face),
                        new Vector3(b.x, 0.5f, face), new Vector3(a.x, 0.5f, face), outward);
                }
            }

            // The soffit: the inside of the arch, seen through the opening.
            for (var i = 0; i < ArchSegments; i++)
            {
                var a = profile[i];
                var b = profile[i + 1];
                var mid = (a + b) * 0.5f;
                var inward = new Vector3(-mid.x, spring - mid.y, 0f).normalized;
                builder.AddQuad(
                    new Vector3(a.x, a.y, -0.5f), new Vector3(b.x, b.y, -0.5f),
                    new Vector3(b.x, b.y, 0.5f), new Vector3(a.x, a.y, 0.5f), inward);
            }

            // The jambs below the springing.
            builder.AddQuad(
                new Vector3(-w, -0.5f, -0.5f), new Vector3(-w, spring, -0.5f),
                new Vector3(-w, spring, 0.5f), new Vector3(-w, -0.5f, 0.5f), Vector3.right);
            builder.AddQuad(
                new Vector3(w, -0.5f, -0.5f), new Vector3(w, spring, -0.5f),
                new Vector3(w, spring, 0.5f), new Vector3(w, -0.5f, 0.5f), Vector3.left);

            // The block's own outside.
            builder.AddQuad(
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(-0.5f, -0.5f, 0.5f), Vector3.left);
            builder.AddQuad(
                new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f), Vector3.right);
            builder.AddQuad(
                new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f), Vector3.up);
            builder.AddQuad(
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-w, -0.5f, -0.5f),
                new Vector3(-w, -0.5f, 0.5f), new Vector3(-0.5f, -0.5f, 0.5f), Vector3.down);
            builder.AddQuad(
                new Vector3(w, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, 0.5f), new Vector3(w, -0.5f, 0.5f), Vector3.down);

            _portal = builder.ToMesh("Tunnel Portal");
            return _portal;
        }

        /// <summary>Accumulates flat-shaded quads. Each quad carries its own vertices, so the faces stay crisp.</summary>
        private sealed class QuadMeshBuilder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int> _triangles = new List<int>();

            /// <summary>
            /// Adds a quad. <paramref name="outward"/> is which way it should face; the winding is flipped to match
            /// rather than trusted, so a corner listed the wrong way round cannot silently vanish into back-face
            /// culling.
            /// </summary>
            public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                var normal = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(normal, outward) < 0f)
                {
                    (b, d) = (d, b);
                    normal = -normal;
                }

                var start = _vertices.Count;
                _vertices.Add(a);
                _vertices.Add(b);
                _vertices.Add(c);
                _vertices.Add(d);
                for (var i = 0; i < 4; i++) _normals.Add(normal);
                _uvs.Add(new Vector2(0f, 0f));
                _uvs.Add(new Vector2(1f, 0f));
                _uvs.Add(new Vector2(1f, 1f));
                _uvs.Add(new Vector2(0f, 1f));

                // Unity's front face is the one whose winding gives cross(p1 - p0, p2 - p0) towards the viewer,
                // which is what the built-in primitives use. Emit the corners in order and the face survives culling.
                _triangles.Add(start);
                _triangles.Add(start + 1);
                _triangles.Add(start + 2);
                _triangles.Add(start);
                _triangles.Add(start + 2);
                _triangles.Add(start + 3);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetUVs(0, _uvs);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
