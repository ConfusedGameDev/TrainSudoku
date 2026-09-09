using System.Collections.Generic;
using TrainSudoku.Core;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Bends a straight track segment along a <see cref="TrackCurve"/> (PRD section 7). The source mesh is read along
    /// its local Z extent, mapped to t in [0, 1] on the curve; X becomes the offset to the right of travel and Y stays
    /// vertical. One bent mesh per key serves every cell with that key.
    /// </summary>
    public static class TrackMeshBender
    {
        private static readonly Dictionary<(Mesh, PieceKey), Mesh> Cache = new Dictionary<(Mesh, PieceKey), Mesh>();

        /// <summary>Bent copy of <paramref name="segment"/> for a key, cached per source mesh.</summary>
        public static Mesh ForKey(Mesh segment, PieceKey key)
        {
            var cacheKey = (segment, key);
            if (Cache.TryGetValue(cacheKey, out var cached) && cached != null) return cached;
            var bent = Bend(segment, TrackCurve.For(key));
            bent.name = $"{segment.name} {key}";
            Cache[cacheKey] = bent;
            return bent;
        }

        public static Mesh Bend(Mesh segment, TrackCurve curve)
        {
            var bounds = segment.bounds;
            var minZ = bounds.min.z;
            var length = Mathf.Max(bounds.size.z, 1e-5f);
            var vertices = segment.vertices;
            var normals = segment.normals;
            var hasNormals = normals != null && normals.Length == vertices.Length;

            var bentVertices = new Vector3[vertices.Length];
            var bentNormals = hasNormals ? new Vector3[vertices.Length] : null;
            for (var i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i];
                var t = Mathf.Clamp01((v.z - minZ) / length);
                var (px, pz) = curve.Position(t);
                var (tx, tz) = curve.Tangent(t);
                var forward = new Vector3((float)tx, 0f, (float)tz);
                var right = new Vector3(forward.z, 0f, -forward.x);
                bentVertices[i] = new Vector3((float)px, 0f, (float)pz) + right * v.x + Vector3.up * v.y;
                if (hasNormals)
                {
                    var n = normals[i];
                    bentNormals[i] = (right * n.x + Vector3.up * n.y + forward * n.z).normalized;
                }
            }

            var mesh = new Mesh { indexFormat = segment.indexFormat };
            mesh.SetVertices(bentVertices);
            if (hasNormals) mesh.SetNormals(bentNormals);
            var uv = new List<Vector2>();
            segment.GetUVs(0, uv);
            if (uv.Count == vertices.Length) mesh.SetUVs(0, uv);
            mesh.subMeshCount = segment.subMeshCount;
            for (var s = 0; s < segment.subMeshCount; s++) mesh.SetTriangles(segment.GetTriangles(s), s);
            if (!hasNormals) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
