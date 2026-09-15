using System;

namespace TrainSudoku.XR.Rules
{
    /// <summary>
    /// The watch check (XR-PRD 6.4): the wrist button shows while the back of the wrist is turned to the eyes, the way
    /// one looks at a watch, and hides once it turns away. It shows inside <see cref="ShowDegrees"/> and hides only
    /// outside <see cref="HideDegrees"/>, so a wrist held near the limit does not flicker the button on and off.
    /// </summary>
    public sealed class WatchCheck
    {
        public const double ShowDegrees = 45;
        public const double HideDegrees = 65;

        public bool Showing { get; private set; }

        /// <param name="tracked">The wrist is tracked this frame.</param>
        /// <param name="back">The direction out of the back of the wrist.</param>
        /// <param name="toEyes">From the button to the eyes.</param>
        /// <returns>Whether the button shows.</returns>
        public bool Update(bool tracked, (double X, double Y, double Z) back, (double X, double Y, double Z) toEyes)
        {
            if (!tracked) return Showing = false;
            var lengths = Length(back) * Length(toEyes);
            if (lengths < 1e-12) return Showing = false;
            var cosine = (back.X * toEyes.X + back.Y * toEyes.Y + back.Z * toEyes.Z) / lengths;
            var limit = Math.Cos((Showing ? HideDegrees : ShowDegrees) * Math.PI / 180);
            return Showing = cosine >= limit;
        }

        public void Reset() => Showing = false;

        private static double Length((double X, double Y, double Z) v) => Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
    }
}
