using System.Collections;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// A one-shot scale pop on a world-space object: out to a peak and back, on unscaled time. The clue labels use
    /// it when a row or column becomes satisfied (work order 9: 160 ms, 1.2x).
    /// </summary>
    /// <remarks>
    /// It attaches itself on demand and stays attached, so a clue that pops twice reuses the one component and the
    /// second pop restarts the first rather than fighting it.
    /// </remarks>
    public sealed class PopScale : MonoBehaviour
    {
        private Coroutine _running;
        private Vector3 _rest = Vector3.one;

        /// <summary>Pops <paramref name="target"/>, attaching the component the first time it is asked.</summary>
        public static void Play(GameObject target, float peak, float duration)
        {
            if (target == null) return;
            if (!target.TryGetComponent<PopScale>(out var pop)) pop = target.AddComponent<PopScale>();
            pop.Play(peak, duration);
        }

        public void Play(float peak, float duration)
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                transform.localScale = _rest;
            }
            else
            {
                _rest = transform.localScale;
            }

            if (isActiveAndEnabled) _running = StartCoroutine(Pop(peak, duration));
        }

        private IEnumerator Pop(float peak, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                // One half-sine: full size at both ends, the peak in the middle.
                transform.localScale = _rest * Mathf.LerpUnclamped(1f, peak, Mathf.Sin(t * Mathf.PI));
                yield return null;
            }

            transform.localScale = _rest;
            _running = null;
        }
    }
}
