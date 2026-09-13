using System;
using TrainSudoku.Core;
using TrainSudoku.XR.Rules;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>Something a hand can take hold of (XR-PRD 10.4): a tray slot, by its key, or a board cell.</summary>
    public readonly struct GrabTarget : IEquatable<GrabTarget>
    {
        public bool IsTray { get; }

        /// <summary>A tray slot's key.</summary>
        public PieceKey Key { get; }

        /// <summary>A board cell's column and row.</summary>
        public int X { get; }
        public int Y { get; }

        private GrabTarget(bool isTray, PieceKey key, int x, int y)
        {
            IsTray = isTray;
            Key = key;
            X = x;
            Y = y;
        }

        public static GrabTarget Tray(PieceKey key) => new GrabTarget(true, key, -1, -1);
        public static GrabTarget Cell(int x, int y) => new GrabTarget(false, default, x, y);

        public bool Equals(GrabTarget other) => IsTray == other.IsTray && (IsTray ? Key == other.Key : X == other.X && Y == other.Y);
        public override bool Equals(object obj) => obj is GrabTarget other && Equals(other);
        public override int GetHashCode() => IsTray ? (int)Key : 1000 + X * 31 + Y;
        public override string ToString() => IsTray ? $"tray {Key}" : $"cell ({X},{Y})";
    }

    /// <summary>Where a hand holds its piece this frame.</summary>
    public readonly struct GrabHold
    {
        /// <summary>The hand itself — the pinch point, or the controller — whose speed tells a throw from a release.</summary>
        public Vector3 Hand { get; }

        /// <summary>Where the input layer itself carries what it holds: the pinch point, or a point along the ray.</summary>
        public Vector3 Point { get; }

        /// <summary>A distant hold's ray. The piece rides the platform under it (X16).</summary>
        public Ray Ray { get; }

        /// <summary>Held along a ray rather than in the hand.</summary>
        public bool Distant { get; }

        public GrabHold(Vector3 hand, Vector3 point, Ray ray, bool distant)
        {
            Hand = hand;
            Point = point;
            Ray = ray;
            Distant = distant;
        }
    }

    /// <summary>
    /// The grab interface (XR-PRD 10.4, X12): how hands reach the game, whatever the hardware. An implementation turns
    /// pinches, grips and rays into grabs and releases of <see cref="GrabTarget"/>s — <see cref="XRIGrabInput"/> on the
    /// XR Interaction Toolkit now; Meta's hand grab or the visionOS spatial pointer later — and
    /// <see cref="XRPieceHands"/> turns those into <see cref="PieceDrop"/> calls. Neither the rules nor the board know
    /// which implementation is in use.
    /// </summary>
    public interface IGrabInput
    {
        /// <summary>A hand took hold of a target.</summary>
        event Action<Hand, GrabTarget> Grabbed;

        /// <summary>A hand that held a target let go of it.</summary>
        event Action<Hand> Released;

        /// <summary>A target started or stopped being aimed at by any hand; the flag is whether any hand still is.</summary>
        event Action<GrabTarget, bool> HoverChanged;

        /// <summary>Whether new grabs may start: off between levels, while paused and while the board is being moved. A hold already begun carries on.</summary>
        bool AcceptsGrabs { get; set; }

        /// <summary>
        /// Makes a target grabbable through <paramref name="volume"/>, the collider hands aim at, whenever
        /// <paramref name="grabbable"/> says it has something to give. With <paramref name="directOnly"/> only a hand's
        /// own pinch or grip can take it, never a ray (4.2). The target goes when the collider does.
        /// </summary>
        void AddTarget(Collider volume, GrabTarget target, Func<bool> grabbable, bool directOnly);

        /// <summary>Where <paramref name="hand"/> holds its target this frame; false when it holds none.</summary>
        bool TryGetHold(Hand hand, out GrabHold hold);
    }
}
