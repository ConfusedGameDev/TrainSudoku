using System.Collections;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>A track piece on the board with its small animations (PRD section 4): scale-in on placement, shake when a fixed piece is long-pressed.</summary>
    public sealed class PieceView : MonoBehaviour
    {
        private const float ScaleInDuration = 0.15f;
        private const float ShakeDuration = 0.25f;
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
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / ScaleInDuration);
                // Ease out with a slight overshoot.
                var s = 1f + 0.15f * Mathf.Sin(t * Mathf.PI);
                transform.localScale = Vector3.one * Mathf.Lerp(0f, 1f, 1f - (1f - t) * (1f - t)) * s;
                yield return null;
            }

            transform.localScale = Vector3.one;
            _animation = null;
        }

        private IEnumerator Shake()
        {
            var elapsed = 0f;
            while (elapsed < ShakeDuration)
            {
                elapsed += Time.deltaTime;
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
