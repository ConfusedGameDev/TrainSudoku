using System.Collections.Generic;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Placeholder straight track: two rails and sleepers, one cell long along +Z, centred on the origin. The rails
    /// are split into short boxes so the bender can curve them. Replaced by the kit mesh through <see cref="TrackAssets"/>.
    /// </summary>
    public static class ProceduralTrackMesh
    {
        private const float RailOffset = 0.28f;
        private const float RailWidth = 0.06f;
        private const float RailHeight = 0.05f;
        private const int RailSegments = 8;
        private const int SleeperCount = 5;
        private const float SleeperWidth = 0.76f;
        private const float SleeperHeight = 0.03f;
        private const float SleeperDepth = 0.10f;

        private static Mesh _straight;

        public static Mesh Straight()
        {
            if (_straight != null) return _straight;

            var builder = new BoxMeshBuilder();
            var segment = 1f / RailSegments;
            for (var i = 0; i < RailSegments; i++)
            {
                var z = -0.5f + segment * (i + 0.5f);
                builder.AddBox(new Vector3(-RailOffset, SleeperHeight + RailHeight / 2f, z), new Vector3(RailWidth, RailHeight, segment));
                builder.AddBox(new Vector3(RailOffset, SleeperHeight + RailHeight / 2f, z), new Vector3(RailWidth, RailHeight, segment));
            }

            for (var i = 0; i < SleeperCount; i++)
            {
                var z = -0.5f + (i + 0.5f) / SleeperCount;
                builder.AddBox(new Vector3(0f, SleeperHeight / 2f, z), new Vector3(SleeperWidth, SleeperHeight, SleeperDepth));
            }

            _straight = builder.ToMesh("Placeholder Track");
            return _straight;
        }

        /// <summary>Accumulates axis-aligned boxes with flat normals into one mesh.</summary>
        private sealed class BoxMeshBuilder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int> _triangles = new List<int>();

            private static readonly Vector3[] FaceNormals =
            {
                Vector3.up, Vector3.down, Vector3.forward, Vector3.back, Vector3.right, Vector3.left,
            };

            public void AddBox(Vector3 center, Vector3 size)
            {
                var half = size / 2f;
                foreach (var normal in FaceNormals)
                {
                    // Two axes spanning the face, chosen so the winding faces outward.
                    var u = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, normal);
                    var v = Vector3.Cross(normal, u);
                    var faceCenter = center + Vector3.Scale(normal, half);
                    var uExtent = Vector3.Scale(u, half);
                    var vExtent = Vector3.Scale(v, half);

                    var start = _vertices.Count;
                    _vertices.Add(faceCenter - uExtent - vExtent);
                    _vertices.Add(faceCenter + uExtent - vExtent);
                    _vertices.Add(faceCenter + uExtent + vExtent);
                    _vertices.Add(faceCenter - uExtent + vExtent);
                    for (var i = 0; i < 4; i++) _normals.Add(normal);
                    _uvs.Add(new Vector2(0f, 0f));
                    _uvs.Add(new Vector2(1f, 0f));
                    _uvs.Add(new Vector2(1f, 1f));
                    _uvs.Add(new Vector2(0f, 1f));

                    // Corners in order: Unity's front face is the winding whose cross(p1 - p0, p2 - p0) faces the
                    // viewer. This was reversed until M19, which left the placeholder track inside out - invisible
                    // in practice only because TrackAssets ships with the kit mesh assigned.
                    _triangles.Add(start);
                    _triangles.Add(start + 1);
                    _triangles.Add(start + 2);
                    _triangles.Add(start);
                    _triangles.Add(start + 2);
                    _triangles.Add(start + 3);
                }
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
