using System;

namespace TrainSudoku.XR.Rules
{
    /// <summary>
    /// Tells a throw from a release (XR-PRD 4.4): the hand's speed over the last <see cref="Window"/> seconds, against a
    /// threshold that starts at 1.2 m/s. The grab layer feeds it the hand's position every frame while a piece is held.
    /// Over a short window rather than one frame, because a single tracking frame is noisy and a hand slows as it opens.
    /// </summary>
    public sealed class ThrowGesture
    {
        public const double DefaultThreshold = 1.2;
        public const double DefaultWindow = 0.1;

        private readonly (double T, double X, double Y, double Z)[] _samples = new (double, double, double, double)[32];
        private int _count;
        private int _newest = -1;

        /// <summary>How far back the speed is measured, in seconds.</summary>
        public double Window { get; }

        public ThrowGesture(double window = DefaultWindow)
        {
            if (!(window > 0)) throw new ArgumentOutOfRangeException(nameof(window), window, "The window must be positive.");
            Window = window;
        }

        public void Clear()
        {
            _count = 0;
            _newest = -1;
        }

        /// <summary>Adds where the hand is at <paramref name="time"/>. A sample no later than the last one is ignored.</summary>
        public void Add(double time, double x, double y, double z)
        {
            if (_count > 0 && time <= _samples[_newest].T) return;
            _newest = (_newest + 1) % _samples.Length;
            _samples[_newest] = (time, x, y, z);
            if (_count < _samples.Length) _count++;
        }

        /// <summary>The hand's mean velocity across the window: the newest sample against the oldest still inside it.</summary>
        public (double X, double Y, double Z) Velocity
        {
            get
            {
                if (_count < 2) return (0, 0, 0);
                var newest = _samples[_newest];
                var oldest = newest;
                for (var i = 1; i < _count; i++)
                {
                    var sample = _samples[(_newest - i + _samples.Length) % _samples.Length];
                    if (newest.T - sample.T > Window) break;
                    oldest = sample;
                }

                var dt = newest.T - oldest.T;
                if (dt <= 0) return (0, 0, 0);
                return ((newest.X - oldest.X) / dt, (newest.Y - oldest.Y) / dt, (newest.Z - oldest.Z) / dt);
            }
        }

        public double Speed
        {
            get
            {
                var (x, y, z) = Velocity;
                return Math.Sqrt(x * x + y * y + z * z);
            }
        }

        /// <summary>Whether letting go now is a throw.</summary>
        public bool IsThrow(double threshold = DefaultThreshold) => Speed > threshold;
    }
}
