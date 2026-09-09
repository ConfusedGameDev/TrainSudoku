using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrainSudoku.Core;
using TrainSudoku.Game;
using UnityEngine;

namespace TrainSudoku.Tests
{
    /// <summary>
    /// The track art is bent onto a quarter circle of radius 0.5. A source with too few rings along Z turns that arc
    /// into a visible polyline, so these tests pin the sag of the bent centre line rather than any vertex count.
    /// </summary>
    public class TrackMeshBenderTests
    {
        private readonly List<Mesh> _created = new List<Mesh>();

        [TearDown]
        public void TearDown()
        {
            TrackMeshBender.Clear();
            foreach (var mesh in _created) Object.DestroyImmediate(mesh);
            _created.Clear();
        }

        /// <summary>
        /// A ribbon standing on the centre line: every vertex has local x = 0, so every bent vertex lands exactly on
        /// the curve and the only error left to measure is the chord sag between rings.
        /// </summary>
        private Mesh CentreLineRibbon()
        {
            var mesh = new Mesh { name = "Ribbon" };
            var z = new[] { -0.5f, 0f, 0.5f };
            var vertices = new List<Vector3>();
            foreach (var value in z)
            {
                vertices.Add(new Vector3(0f, 0f, value));
                vertices.Add(new Vector3(0f, 0.05f, value));
            }

            var triangles = new List<int>();
            for (var i = 0; i < z.Length - 1; i++)
            {
                var a = i * 2;
                triangles.AddRange(new[] { a, a + 1, a + 3, a, a + 3, a + 2 });
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _created.Add(mesh);
            return mesh;
        }

        /// <summary>A single flat quad standing in for one sleeper: 1.0 across (local X), 0.2 deep (local Z).</summary>
        private Mesh SleeperQuad()
        {
            var mesh = new Mesh { name = "Sleeper" };
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-0.5f, 0f, -0.1f), new Vector3(0.5f, 0f, -0.1f),
                new Vector3(0.5f, 0f, 0.1f), new Vector3(-0.5f, 0f, 0.1f),
            });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _created.Add(mesh);
            return mesh;
        }

        private static TrackMeshProfile Profile(Mesh rails, int slices, Mesh sleeper = null,
            int sleepersPerCell = 4, bool evenSpacing = true) =>
            new TrackMeshProfile(rails, sleeper, sleepersPerCell, evenSpacing, slices, 1f);

        /// <summary>Centre of the quarter circle: the cell corner the two side midpoints share.</summary>
        private static Vector2 Corner(TrackCurve curve)
        {
            var from = TrackCurve.SideMidpoint(curve.From);
            var to = TrackCurve.SideMidpoint(curve.To);
            return new Vector2((float)(from.X + to.X), (float)(from.Z + to.Z));
        }

        /// <summary>Largest gap between the true arc and the straight chords the bent mesh actually draws.</summary>
        private static float MaxSag(Mesh bent, TrackCurve curve)
        {
            var corner = Corner(curve);
            var points = bent.vertices
                .Select(v => new Vector2(v.x, v.z))
                .Distinct()
                .OrderBy(p => Mathf.Atan2(p.y - corner.y, p.x - corner.x))
                .ToList();

            // The points sit on a 90 degree arc but Atan2 wraps at +-180, so they are a circular sequence with one
            // large empty gap. Breaking the ring at that gap recovers the true order whichever way the piece turns.
            var breakAt = 0;
            var widest = -1f;
            for (var i = 0; i < points.Count; i++)
            {
                var a = Mathf.Atan2(points[i].y - corner.y, points[i].x - corner.x);
                var next = points[(i + 1) % points.Count];
                var b = Mathf.Atan2(next.y - corner.y, next.x - corner.x) - a;
                while (b < 0f) b += 2f * Mathf.PI;
                if (b > widest) { widest = b; breakAt = i; }
            }

            var sag = 0f;
            for (var step = 0; step < points.Count - 1; step++)
            {
                var a = points[(breakAt + 1 + step) % points.Count];
                var b = points[(breakAt + 2 + step) % points.Count];
                var midpoint = (a + b) * 0.5f;
                sag = Mathf.Max(sag, (float)TrackCurve.Radius - Vector2.Distance(midpoint, corner));
            }

            return sag;
        }

        [Test]
        public void CurvedPieceFollowsTheArcWithinAMillimetre()
        {
            var curve = TrackCurve.For(PieceKey.NE);
            var bent = TrackMeshBender.ForKey(Profile(CentreLineRibbon(), 16), PieceKey.NE);
            Assert.Less(MaxSag(bent, curve), 0.0015f, "Bent track strays from the arc by more than a millimetre.");
        }

        [Test]
        public void WithoutSubdivisionTheSameSourceIsVisiblyFaceted()
        {
            // The bug this fix exists for: three rings bend into two chords, ~38 mm off the arc.
            var curve = TrackCurve.For(PieceKey.NE);
            var bent = TrackMeshBender.ForKey(Profile(CentreLineRibbon(), 1), PieceKey.NE);
            Assert.Greater(MaxSag(bent, curve), 0.03f);
        }

        [Test]
        public void EverySourceRingSurvivesSoTheArcOnlyGetsFiner()
        {
            foreach (var key in PieceKeys.All)
            {
                var curve = TrackCurve.For(key);
                if (curve.IsStraight) continue;
                var bent = TrackMeshBender.ForKey(Profile(CentreLineRibbon(), 16), key);
                Assert.Less(MaxSag(bent, curve), 0.0015f, $"{key} strays from the arc.");
            }
        }

        [Test]
        public void StraightPiecesAreNotSubdivided()
        {
            var source = CentreLineRibbon();
            var bent = TrackMeshBender.ForKey(Profile(source, 16), PieceKey.NS);
            Assert.AreEqual(source.vertexCount, bent.vertexCount);
        }

        [Test]
        public void PieceEndsLandOnTheCellSideMidpointsSoNeighboursStayFlush()
        {
            foreach (var key in PieceKeys.All)
            {
                var curve = TrackCurve.For(key);
                var bent = TrackMeshBender.ForKey(Profile(CentreLineRibbon(), 16), key);
                var points = bent.vertices.Select(v => new Vector2(v.x, v.z)).ToList();

                foreach (var side in new[] { curve.From, curve.To })
                {
                    var midpoint = TrackCurve.SideMidpoint(side);
                    var expected = new Vector2((float)midpoint.X, (float)midpoint.Z);
                    Assert.Less(points.Min(p => Vector2.Distance(p, expected)), 1e-4f,
                        $"{key} does not reach the {side} side midpoint.");
                }
            }
        }

        /// <summary>Sleeper instances laid on a key, found by diffing against the same rails with no sleeper.</summary>
        private static int SleeperCount(Mesh rails, Mesh sleeper, PieceKey key,
            int sleepersPerCell = 4, bool evenSpacing = true)
        {
            var bare = TrackMeshBender.ForKey(Profile(rails, 16), key).vertexCount;
            var dressed = TrackMeshBender.ForKey(Profile(rails, 16, sleeper, sleepersPerCell, evenSpacing), key).vertexCount;
            return (dressed - bare) / sleeper.vertexCount;
        }

        [Test]
        public void SleepersStayRigidInsteadOfPinchingOnTheInsideOfACurve()
        {
            var rails = CentreLineRibbon();
            var sleeper = SleeperQuad();
            var bent = TrackMeshBender.ForKey(Profile(rails, 16, sleeper), PieceKey.NE);

            // A sleeper is as wide as the arc diameter, so bending one would fold its inner edge onto the corner.
            var corner = Corner(TrackCurve.For(PieceKey.NE));
            var collapsed = bent.vertices.Count(v => Vector2.Distance(new Vector2(v.x, v.z), corner) < 1e-3f);
            Assert.Zero(collapsed, "Sleeper geometry collapsed onto the centre of the curve.");

            // Rigid means every instance keeps the source's own 1.0 width across the track.
            var instances = bent.vertices.Skip(bent.vertexCount - SleeperCount(rails, sleeper, PieceKey.NE) * 4).ToList();
            for (var i = 0; i + 3 < instances.Count; i += 4)
                Assert.AreEqual(1f, Vector3.Distance(instances[i], instances[i + 1]), 1e-4f, "A sleeper was stretched.");
        }

        [Test]
        public void SleeperPitchHoldsAcrossStraightsAndCurves()
        {
            var rails = CentreLineRibbon();
            var sleeper = SleeperQuad();

            // 4 per cell: a straight is a full cell, a curve is pi/4 of one, so 4 * 0.7854 rounds to 3.
            Assert.AreEqual(4, SleeperCount(rails, sleeper, PieceKey.NS));
            Assert.AreEqual(3, SleeperCount(rails, sleeper, PieceKey.NE));
        }

        [Test]
        public void SleeperCountFollowsTheParameter()
        {
            var rails = CentreLineRibbon();
            var sleeper = SleeperQuad();

            foreach (var per in new[] { 1, 2, 6, 10 })
                Assert.AreEqual(per, SleeperCount(rails, sleeper, PieceKey.NS, per), $"{per} per cell on a straight.");

            // Turning even spacing off pins the same count on every piece, curves included.
            Assert.AreEqual(6, SleeperCount(rails, sleeper, PieceKey.NE, 6, evenSpacing: false));
            Assert.AreEqual(5, SleeperCount(rails, sleeper, PieceKey.NE, 6));
        }

        [Test]
        public void BentMeshesAreCachedPerKeyAndReleasedOnClear()
        {
            var profile = Profile(CentreLineRibbon(), 16);
            var first = TrackMeshBender.ForKey(profile, PieceKey.NE);
            Assert.AreSame(first, TrackMeshBender.ForKey(profile, PieceKey.NE));

            TrackMeshBender.Clear();
            Assert.IsTrue(first == null, "Clear left the bent mesh alive.");
        }

        [Test]
        public void ResamplingKeepsTheSourceShapeAndItsAttributes()
        {
            var source = CentreLineRibbon();
            var sliced = TrackMeshResampler.Subdivide(source, 8);
            _created.Add(sliced);

            Assert.Greater(sliced.vertexCount, source.vertexCount, "Nothing was subdivided.");
            Assert.AreEqual(source.bounds.center.z, sliced.bounds.center.z, 1e-5f);
            Assert.AreEqual(source.bounds.size.z, sliced.bounds.size.z, 1e-5f);
            Assert.AreEqual(sliced.vertexCount, sliced.normals.Length);
            foreach (var triangle in sliced.triangles) Assert.Less(triangle, sliced.vertexCount);
        }
    }
}
