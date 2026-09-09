using System.Collections.Generic;
using TrainSudoku.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Bends a straight track segment along a <see cref="TrackCurve"/> (PRD section 7). The source is read along its
    /// local Z extent, mapped to t in [0, 1] on the curve; X becomes the offset to the right of travel and Y stays
    /// vertical. Low-poly sources are resampled first (see <see cref="TrackMeshResampler"/>) so curves are smooth
    /// rather than faceted. Rails and sleepers are welded into one mesh per key, so a piece stays a single draw call.
    /// </summary>
    public static class TrackMeshBender
    {
        private static readonly Dictionary<(TrackMeshProfile, PieceKey), Mesh> Cache =
            new Dictionary<(TrackMeshProfile, PieceKey), Mesh>();

        private static readonly Dictionary<(Mesh, int), Mesh> Resampled = new Dictionary<(Mesh, int), Mesh>();

        /// <summary>Bent mesh for a key, cached per profile.</summary>
        public static Mesh ForKey(TrackMeshProfile profile, PieceKey key)
        {
            var cacheKey = (profile, key);
            if (Cache.TryGetValue(cacheKey, out var cached) && cached != null) return cached;

            var bent = Build(profile, TrackCurve.For(key));
            bent.name = $"{profile.Name} {key}";
            Cache[cacheKey] = bent;
            return bent;
        }

        /// <summary>Drops every generated mesh. Called on level teardown so bent meshes do not pile up.</summary>
        public static void Clear()
        {
            foreach (var mesh in Cache.Values) Destroy(mesh);
            foreach (var mesh in Resampled.Values) Destroy(mesh);
            Cache.Clear();
            Resampled.Clear();
        }

        private static void Destroy(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) Object.Destroy(mesh);
            else Object.DestroyImmediate(mesh);
        }

        /// <summary>Rails across the whole curve plus evenly spaced sleepers, combined into one mesh.</summary>
        private static Mesh Build(TrackMeshProfile profile, TrackCurve curve)
        {
            var slices = curve.IsStraight ? 1 : Mathf.Max(1, profile.CurveSlices);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();

            AppendBent(Resample(profile.Rails, slices), curve, 0.0, 1.0, profile.WidthScale,
                vertices, normals, tangents, uv, triangles);

            if (profile.Sleeper != null && profile.SleepersPerCell > 0)
            {
                var count = profile.SleeperCount(curve.Length);
                var sleeperWidth = profile.WidthScale * profile.SleeperWidthScale;

                // Bending keeps the tiles joined into a continuous deck, but the Jacobian collapses at
                // |x| = Radius, so art that reaches the arc centre has to be dropped in rigidly instead.
                var halfWidth = profile.Sleeper.bounds.extents.x * sleeperWidth;
                var canBend = curve.IsStraight || halfWidth < (float)TrackCurve.Radius * 0.9f;
                var span = canBend ? Mathf.Min(1f, profile.Sleeper.bounds.size.z / (float)curve.Length) : 0f;
                var sleeper = canBend
                    ? Resample(profile.Sleeper, Mathf.Max(1, Mathf.RoundToInt(slices * span)))
                    : profile.Sleeper;

                for (var i = 0; i < count; i++)
                {
                    // Half a pitch in at both ends, so neighbouring pieces never double up on the shared side midpoint.
                    var centre = (i + 0.5) / count;
                    if (canBend)
                        AppendBent(sleeper, curve, centre - span / 2.0, centre + span / 2.0, sleeperWidth,
                            vertices, normals, tangents, uv, triangles);
                    else
                        AppendRigid(sleeper, curve, centre, sleeperWidth,
                            vertices, normals, tangents, uv, triangles);
                }
            }

            var mesh = new Mesh
            {
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                hideFlags = HideFlags.HideAndDontSave,
            };
            mesh.SetVertices(vertices);
            if (normals.Count == vertices.Count) mesh.SetNormals(normals);
            if (tangents.Count == vertices.Count) mesh.SetTangents(tangents);
            if (uv.Count == vertices.Count) mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            if (normals.Count != vertices.Count) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh Resample(Mesh source, int slices)
        {
            if (source == null || slices <= 1) return source;
            var key = (source, slices);
            if (Resampled.TryGetValue(key, out var cached) && cached != null) return cached;
            var mesh = TrackMeshResampler.Subdivide(source, slices);
            // Subdivide hands back the source itself when there is nothing to cut; that mesh is an imported asset and
            // must never enter the cache, which destroys everything it holds.
            if (!ReferenceEquals(mesh, source)) Resampled[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// Warps <paramref name="source"/> along the whole curve: local Z runs the arc, X offsets to the right of
        /// travel, Y stays vertical.
        /// </summary>
        /// <remarks>
        /// Along-track lengths scale by the Jacobian <c>k = (Length / depth) * (1 + curvature * x)</c>, so normals and
        /// tangents cannot use the frame directly. Note <c>k</c> reaches zero at <c>|x| = Radius</c>: art as wide as
        /// the arc diameter folds through the arc centre, which is why sleepers are placed rigidly instead.
        /// </remarks>
        private static void AppendBent(Mesh source, TrackCurve curve, double t0, double t1, float widthScale,
            List<Vector3> vertices, List<Vector3> normals, List<Vector4> tangents, List<Vector2> uv,
            List<int> triangles)
        {
            if (source == null) return;

            var minZ = source.bounds.min.z;
            var depth = Mathf.Max(source.bounds.size.z, 1e-5f);
            var stretch = (float)(curve.Length * (t1 - t0)) / depth;
            var curvature = CurvatureSign(curve) / (float)TrackCurve.Radius;

            Read(source, out var sourceVertices, out var sourceNormals, out var sourceTangents, out var sourceUv,
                out var hasNormals, out var hasTangents, out var hasUv);

            var first = vertices.Count;
            for (var i = 0; i < sourceVertices.Length; i++)
            {
                var v = sourceVertices[i];
                var t = t0 + (t1 - t0) * Mathf.Clamp01((v.z - minZ) / depth);
                var (px, pz) = curve.Position(t);
                var (tx, tz) = curve.Tangent(t);
                var forward = new Vector3((float)tx, 0f, (float)tz);
                var right = new Vector3(forward.z, 0f, -forward.x);
                var x = v.x * widthScale;
                var k = Mathf.Max(1e-4f, stretch * (1f + curvature * x));

                vertices.Add(new Vector3((float)px, 0f, (float)pz) + right * x + Vector3.up * v.y);

                if (hasNormals)
                {
                    var n = sourceNormals[i];
                    normals.Add((right * n.x + Vector3.up * n.y + forward * (n.z / k)).normalized);
                }

                if (hasTangents)
                {
                    var tan = sourceTangents[i];
                    var rotated = (right * tan.x + Vector3.up * tan.y + forward * (tan.z * k)).normalized;
                    tangents.Add(new Vector4(rotated.x, rotated.y, rotated.z, tan.w));
                }

                if (hasUv) uv.Add(sourceUv[i]);
            }

            AppendTriangles(source, first, triangles);
        }

        /// <summary>
        /// Drops an undeformed copy of <paramref name="source"/> at <paramref name="t"/> along the curve, turned to
        /// face the direction of travel. Sleepers are rigid planks; bending one as wide as the arc diameter would
        /// pinch its inner edge to a point.
        /// </summary>
        private static void AppendRigid(Mesh source, TrackCurve curve, double t, float widthScale,
            List<Vector3> vertices, List<Vector3> normals, List<Vector4> tangents, List<Vector2> uv,
            List<int> triangles)
        {
            if (source == null) return;

            var (px, pz) = curve.Position(t);
            var (tx, tz) = curve.Tangent(t);
            var forward = new Vector3((float)tx, 0f, (float)tz);
            var right = new Vector3(forward.z, 0f, -forward.x);
            var origin = new Vector3((float)px, 0f, (float)pz);

            Read(source, out var sourceVertices, out var sourceNormals, out var sourceTangents, out var sourceUv,
                out var hasNormals, out var hasTangents, out var hasUv);

            var first = vertices.Count;
            for (var i = 0; i < sourceVertices.Length; i++)
            {
                var v = sourceVertices[i];
                vertices.Add(origin + right * (v.x * widthScale) + Vector3.up * v.y + forward * v.z);

                // A pure rotation, so normals and tangents need no Jacobian correction.
                if (hasNormals)
                {
                    var n = sourceNormals[i];
                    normals.Add(right * n.x + Vector3.up * n.y + forward * n.z);
                }

                if (hasTangents)
                {
                    var tan = sourceTangents[i];
                    var rotated = right * tan.x + Vector3.up * tan.y + forward * tan.z;
                    tangents.Add(new Vector4(rotated.x, rotated.y, rotated.z, tan.w));
                }

                if (hasUv) uv.Add(sourceUv[i]);
            }

            AppendTriangles(source, first, triangles);
        }

        /// <summary>+1 where the curve turns left, -1 where it turns right, 0 on a straight.</summary>
        private static float CurvatureSign(TrackCurve curve)
        {
            if (curve.IsStraight) return 0f;
            var (x0, z0) = curve.Tangent(0.0);
            var (x1, z1) = curve.Tangent(1.0);
            return x0 * z1 - z0 * x1 >= 0.0 ? 1f : -1f;
        }

        private static void Read(Mesh source, out Vector3[] positions, out Vector3[] normals, out Vector4[] tangents,
            out List<Vector2> uv, out bool hasNormals, out bool hasTangents, out bool hasUv)
        {
            positions = source.vertices;
            normals = source.normals;
            tangents = source.tangents;
            uv = new List<Vector2>();
            source.GetUVs(0, uv);
            hasNormals = normals != null && normals.Length == positions.Length;
            hasTangents = tangents != null && tangents.Length == positions.Length;
            hasUv = uv.Count == positions.Length;
        }

        private static void AppendTriangles(Mesh source, int first, List<int> triangles)
        {
            for (var s = 0; s < source.subMeshCount; s++)
            {
                var indices = source.GetTriangles(s);
                for (var i = 0; i < indices.Length; i++) triangles.Add(first + indices[i]);
            }
        }
    }
}
