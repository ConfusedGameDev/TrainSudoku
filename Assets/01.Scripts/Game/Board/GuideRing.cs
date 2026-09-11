using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The ring the tutorial draws round the cell it is asking the player to tap. It breathes so the eye finds it
    /// against a static board, and it can be popped once to answer a tap that landed somewhere else.
    /// </summary>
    /// <remarks>
    /// <b>Unscaled time</b>, like every other board animation (<see cref="BoardMarker"/>, <see cref="PopScale"/>):
    /// the tutorial is up in Play only, but nothing on the board is allowed to stop with the timescale.
    ///
    /// It owns its own scale rather than borrowing <see cref="PopScale"/> for the pop. Two things writing
    /// <c>localScale</c> on the same transform each frame fight, and the loser is whichever ran first — so the pulse
    /// and the pop are one calculation here instead of two components arguing.
    /// </remarks>
    public sealed class GuideRing : MonoBehaviour
    {
        /// <summary>One breath. Slow enough to read as waiting rather than as an alarm.</summary>
        private const float Period = 1.6f;

        /// <summary>How far the breath swells. Small: the ring marks a cell, it must not look like it is growing out of one.</summary>
        private const float Swell = 0.06f;

        /// <summary>The extra kick a refused tap gives, and how long it takes to fall back into the breath.</summary>
        private const float PopScaleAmount = 0.22f;
        private const float PopSeconds = 0.32f;

        private float _phase;
        private float _pop = -1f;

        /// <summary>Answers a tap that landed off the guided cell: one kick, then back to breathing.</summary>
        public void Pop() => _pop = 0f;

        private void Update()
        {
            _phase += Time.unscaledDeltaTime / Period;
            if (_phase >= 1f) _phase -= Mathf.Floor(_phase);

            var breath = 1f + Swell * 0.5f * (1f - Mathf.Cos(_phase * 2f * Mathf.PI));

            var kick = 0f;
            if (_pop >= 0f)
            {
                _pop += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(_pop / PopSeconds);
                kick = PopScaleAmount * Mathf.Sin(t * Mathf.PI);
                if (t >= 1f) _pop = -1f;
            }

            transform.localScale = Vector3.one * (breath + kick);
        }
    }
}
