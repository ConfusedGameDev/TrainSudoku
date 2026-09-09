using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The art a track piece is built from: the rail mesh bent along the whole curve, and an optional sleeper repeated
    /// along it. Value equality lets <see cref="TrackMeshBender"/> cache bent meshes per profile and key.
    /// </summary>
    public readonly struct TrackMeshProfile : IEquatable<TrackMeshProfile>
    {
        /// <summary>Rails, one cell long along local Z.</summary>
        public Mesh Rails { get; }

        /// <summary>The cross tie repeated along the track, or null for rails on their own.</summary>
        public Mesh Sleeper { get; }

        /// <summary>Sleepers laid on a full one-cell straight.</summary>
        public int SleepersPerCell { get; }

        /// <summary>
        /// When set, a curve gets fewer sleepers than a straight so the spacing stays even (a curve is only pi/4 of a
        /// cell long). When clear, every piece gets <see cref="SleepersPerCell"/> and curves pack them tighter.
        /// </summary>
        public bool EvenSleeperSpacing { get; }

        /// <summary>Rings the resampler cuts a curved piece into. Straights are never subdivided.</summary>
        public int CurveSlices { get; }

        /// <summary>Narrows or widens the track across its direction of travel.</summary>
        public float WidthScale { get; }

        /// <summary>
        /// Extra narrowing applied to sleepers only. A curve has a radius of 0.5, so a sleeper wider than 1.0 reaches
        /// the arc centre and neighbouring planks pile up on the inside of every turn.
        /// </summary>
        public float SleeperWidthScale { get; }

        public TrackMeshProfile(Mesh rails, Mesh sleeper, int sleepersPerCell, bool evenSleeperSpacing,
            int curveSlices, float widthScale, float sleeperWidthScale = 1f)
        {
            SleeperWidthScale = sleeperWidthScale;
            Rails = rails;
            Sleeper = sleeper;
            SleepersPerCell = sleepersPerCell;
            EvenSleeperSpacing = evenSleeperSpacing;
            CurveSlices = curveSlices;
            WidthScale = widthScale;
        }

        /// <summary>Sleepers to lay on one piece, from its arc length.</summary>
        public int SleeperCount(double curveLength) => EvenSleeperSpacing
            ? Mathf.Max(1, Mathf.RoundToInt(SleepersPerCell * (float)curveLength))
            : Mathf.Max(1, SleepersPerCell);

        public string Name => Rails != null ? Rails.name : "Track";

        // Reference identity, not Unity's == overload: the cache wants "the very same mesh object", and a destroyed
        // mesh must not compare equal to a null one.
        private static int Id(Mesh mesh) => RuntimeHelpers.GetHashCode(mesh);

        public bool Equals(TrackMeshProfile other) =>
            ReferenceEquals(Rails, other.Rails) &&
            ReferenceEquals(Sleeper, other.Sleeper) &&
            SleepersPerCell == other.SleepersPerCell &&
            SleeperWidthScale.Equals(other.SleeperWidthScale) &&
            EvenSleeperSpacing == other.EvenSleeperSpacing &&
            CurveSlices == other.CurveSlices &&
            WidthScale.Equals(other.WidthScale);

        public override bool Equals(object obj) => obj is TrackMeshProfile other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Id(Rails), Id(Sleeper), SleepersPerCell, EvenSleeperSpacing, CurveSlices, WidthScale,
                SleeperWidthScale);
    }
}
