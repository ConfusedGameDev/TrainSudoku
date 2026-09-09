using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Subdivides a mesh along its local Z into evenly spaced bands so <see cref="TrackMeshBender"/> has enough rings
    /// to follow a curve smoothly. The kit segment carries only three Z rings, so bending it straight off the import
    /// turns a quarter circle into a two-facet polyline; slicing it first is what makes curves read as arcs.
    /// Attributes are interpolated and never recomputed, so the hard edges and UV seams of the source survive intact.
    /// </summary>
    public static class TrackMeshResampler
    {
        private struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector2 Uv;
            public Vector4 Tangent;

            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex
            {
                Position = Vector3.Lerp(a.Position, b.Position, t),
                Normal = Vector3.Lerp(a.Normal, b.Normal, t),
                Uv = Vector2.Lerp(a.Uv, b.Uv, t),
                Tangent = Vector4.Lerp(a.Tangent, b.Tangent, t),
            };
        }

        /// <summary>
        /// A copy of <paramref name="source"/> cut into <paramref name="slices"/> bands along local Z. Returns the
        /// source untouched when no extra rings are wanted or the mesh has no depth to cut.
        /// </summary>
        public static Mesh Subdivide(Mesh source, int slices)
        {
            if (source == null || slices <= 1) return source;

            var bounds = source.bounds;
            var minZ = bounds.min.z;
            var length = bounds.size.z;
            if (length <= 1e-5f) return source;

            var positions = source.vertices;
            var normals = source.normals;
            var tangents = source.tangents;
            var uv = new List<Vector2>();
            source.GetUVs(0, uv);

            var hasNormals = normals != null && normals.Length == positions.Length;
            var hasTangents = tangents != null && tangents.Length == positions.Length;
            var hasUv = uv.Count == positions.Length;

            var outVertices = new List<Vertex>(positions.Length * slices);
            var outTriangles = new List<int>[source.subMeshCount];

            // Scratch buffers reused across every triangle so slicing does not allocate per face.
            var poly = new List<Vertex>(8);
            var scratch = new List<Vertex>(8);

            for (var s = 0; s < source.subMeshCount; s++)
            {
                var indices = source.GetTriangles(s);
                var triangles = new List<int>(indices.Length * slices);
                outTriangles[s] = triangles;

                for (var i = 0; i < indices.Length; i += 3)
                {
                    var a = Read(indices[i], positions, normals, tangents, uv, hasNormals, hasTangents, hasUv);
                    var b = Read(indices[i + 1], positions, normals, tangents, uv, hasNormals, hasTangents, hasUv);
                    var c = Read(indices[i + 2], positions, normals, tangents, uv, hasNormals, hasTangents, hasUv);

                    var lo = Band(Mathf.Min(a.Position.z, Mathf.Min(b.Position.z, c.Position.z)), minZ, length, slices);
                    var hi = Band(Mathf.Max(a.Position.z, Mathf.Max(b.Position.z, c.Position.z)), minZ, length, slices);

                    for (var band = lo; band <= hi; band++)
                    {
                        poly.Clear();
                        poly.Add(a);
                        poly.Add(b);
                        poly.Add(c);

                        // A triangle clipped by planes stays convex, so Sutherland-Hodgman is safe here.
                        if (band > 0) Clip(poly, scratch, minZ + length * band / slices, true);
                        if (band < slices - 1) Clip(poly, scratch, minZ + length * (band + 1) / slices, false);
                        if (poly.Count < 3) continue;

                        var first = outVertices.Count;
                        for (var v = 0; v < poly.Count; v++) outVertices.Add(poly[v]);
                        for (var v = 1; v < poly.Count - 1; v++)
                        {
                            triangles.Add(first);
                            triangles.Add(first + v);
                            triangles.Add(first + v + 1);
                        }
                    }
                }
            }

            return Build(source, outVertices, outTriangles, hasNormals, hasTangents, hasUv);
        }

        private static Vertex Read(int index, Vector3[] positions, Vector3[] normals, Vector4[] tangents,
            List<Vector2> uv, bool hasNormals, bool hasTangents, bool hasUv) => new Vertex
        {
            Position = positions[index],
            Normal = hasNormals ? normals[index] : Vector3.up,
            Tangent = hasTangents ? tangents[index] : new Vector4(1f, 0f, 0f, 1f),
            Uv = hasUv ? uv[index] : Vector2.zero,
        };

        /// <summary>A stable order on positions, so a shared edge is always interpolated from the same end.</summary>
        private static bool Precedes(Vector3 a, Vector3 b)
        {
            if (a.z != b.z) return a.z < b.z;
            if (a.x != b.x) return a.x < b.x;
            return a.y <= b.y;
        }

        private static int Band(float z, float minZ, float length, int slices)
        {
            var band = Mathf.FloorToInt((z - minZ) / length * slices);
            return Mathf.Clamp(band, 0, slices - 1);
        }

        /// <summary>Clips <paramref name="poly"/> in place to one side of a constant-Z plane.</summary>
        private static void Clip(List<Vertex> poly, List<Vertex> scratch, float z, bool keepAbove)
        {
            scratch.Clear();
            var sign = keepAbove ? 1f : -1f;

            for (var i = 0; i < poly.Count; i++)
            {
                var current = poly[i];
                var next = poly[(i + 1) % poly.Count];
                var dCurrent = (current.Position.z - z) * sign;
                var dNext = (next.Position.z - z) * sign;
                var currentInside = dCurrent >= 0f;

                if (currentInside) scratch.Add(current);
                if (currentInside != dNext >= 0f)
                {
                    var denominator = dCurrent - dNext;
                    if (Mathf.Abs(denominator) > 1e-9f)
                    {
                        // The neighbouring triangle walks this edge the other way round. Always interpolating from the
                        // lexicographically lower endpoint makes both sides produce bit-identical cut vertices, so no
                        // crack opens along the seam.
                        scratch.Add(Precedes(current.Position, next.Position)
                            ? Vertex.Lerp(current, next, dCurrent / denominator)
                            : Vertex.Lerp(next, current, -dNext / denominator));
                    }
                }
            }

            poly.Clear();
            poly.AddRange(scratch);
        }

        private static Mesh Build(Mesh source, List<Vertex> vertices, List<int>[] triangles,
            bool hasNormals, bool hasTangents, bool hasUv)
        {
            var mesh = new Mesh
            {
                name = source.name + " resampled",
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : source.indexFormat,
            };

            var positions = new Vector3[vertices.Count];
            var normals = hasNormals ? new Vector3[vertices.Count] : null;
            var tangents = hasTangents ? new Vector4[vertices.Count] : null;
            var uv = hasUv ? new Vector2[vertices.Count] : null;

            for (var i = 0; i < vertices.Count; i++)
            {
                var vertex = vertices[i];
                positions[i] = vertex.Position;
                if (hasNormals) normals[i] = vertex.Normal.normalized;
                if (hasTangents) tangents[i] = vertex.Tangent;
                if (hasUv) uv[i] = vertex.Uv;
            }

            mesh.SetVertices(positions);
            if (hasNormals) mesh.SetNormals(normals);
            if (hasTangents) mesh.SetTangents(tangents);
            if (hasUv) mesh.SetUVs(0, uv);

            mesh.subMeshCount = triangles.Length;
            for (var s = 0; s < triangles.Length; s++) mesh.SetTriangles(triangles[s], s);
            if (!hasNormals) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
