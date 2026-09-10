using System.Collections;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// A track piece on the board with its small animations (PRD section 4, work order 9): a 220 ms snap-in that
    /// overshoots to 1.15 and settles, and a 180 ms shake when a fixed piece is long-pressed. Both run on unscaled
    /// time, so neither depends on <c>Time.timeScale</c>.
    /// </summary>
    public sealed class PieceView : MonoBehaviour
    {
        private const float ScaleInDuration = 0.22f;
        private const float Overshoot = 1.15f;
        private const float ShakeDuration = 0.18f;
        private const float ShakeAmplitude = 0.05f;

        private Vector3 _restPosition;
        private Coroutine _animation;

        public int X { get; private set; }
        public int Y { get; private set; }
        public bool IsFixed { get; private set; }

        public void Set(int x, int y, bool isFixed)
        {
            X = x;
            Y = y;
            IsFixed = isFixed;
            _restPosition = transform.localPosition;
        }

        public void PlayScaleIn()
        {
            Restart(ScaleIn());
        }

        public void PlayShake()
        {
            Restart(Shake());
        }

        private void Restart(IEnumerator routine)
        {
            if (_animation != null) StopCoroutine(_animation);
            transform.localScale = Vector3.one;
            transform.localPosition = _restPosition;
            if (isActiveAndEnabled) _animation = StartCoroutine(routine);
        }

        private IEnumerator ScaleIn()
        {
            var elapsed = 0f;
            transform.localScale = Vector3.zero;
            while (elapsed < ScaleInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / ScaleInDuration);
                transform.localScale = Vector3.one * Snap(t);
                yield return null;
            }

            transform.localScale = Vector3.one;
            _animation = null;
        }

        /// <summary>
        /// Rises past full size and comes back: 0 to <see cref="Overshoot"/> over the first 60 percent of the
        /// duration, then down to 1. Both halves ease out, so the piece arrives with weight rather than snapping.
        /// </summary>
        private static float Snap(float t)
        {
            const float peak = 0.6f;
            if (t < peak)
            {
                var u = t / peak;
                return Overshoot * (1f - (1f - u) * (1f - u));
            }

            var v = (t - peak) / (1f - peak);
            return Mathf.Lerp(Overshoot, 1f, 1f - (1f - v) * (1f - v));
        }

        private IEnumerator Shake()
        {
            var elapsed = 0f;
            while (elapsed < ShakeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var falloff = 1f - Mathf.Clamp01(elapsed / ShakeDuration);
                var offset = Mathf.Sin(elapsed * 60f) * ShakeAmplitude * falloff;
                transform.localPosition = _restPosition + new Vector3(offset, 0f, 0f);
                yield return null;
            }

            transform.localPosition = _restPosition;
            _animation = null;
        }
    }
}
