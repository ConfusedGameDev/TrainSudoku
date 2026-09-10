using TrainSudoku.Core;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// A tappable arrow at the midpoint of a selected cell's side, hopping on the spot so it reads as an invitation
    /// rather than a decal (work order 9: 900 ms loop, forced side 6 px higher).
    /// </summary>
    /// <remarks>
    /// A forced side — the one legal choice left — hops higher and wears the line colour, which is the same signal
    /// twice over: the game is telling the player where the track has to go next.
    ///
    /// The hop runs on <b>unscaled</b> time. Nothing pauses the board mid-selection today, but every motion in the
    /// work order is specified that way and one that silently depends on <c>Time.timeScale</c> would be the one that
    /// breaks the day something does.
    /// </remarks>
    public sealed class BoardMarker : MonoBehaviour
    {
        /// <summary>One loop of the hop.</summary>
        private const float Period = 0.9f;

        /// <summary>The share of the loop spent in the air; the rest is the pause between hops.</summary>
        private const float AirTime = 0.45f;

        /// <summary>
        /// A hop, in cells. The work order says 6 px: at the 1080-wide reference a six-cell board runs about 167 px
        /// to the cell, so 6 px is 0.036 of one.
        /// </summary>
        public const float HopHeight = 0.036f;

        private Vector3 _restPosition;
        private float _height;
        private float _phase;

        public Direction Side { get; private set; }

        /// <summary>Sets the side this marker stands for and how high it hops; a forced side hops twice as high.</summary>
        public void Set(Direction side, bool forced)
        {
            Side = side;
            _restPosition = transform.localPosition;
            _height = forced ? HopHeight * 2f : HopHeight;
            // Every marker of one selection hops together, so they read as one prompt rather than a queue.
            _phase = 0f;
        }

        private void Update()
        {
            _phase += Time.unscaledDeltaTime / Period;
            if (_phase >= 1f) _phase -= Mathf.Floor(_phase);

            var rise = _phase < AirTime ? Mathf.Sin(Mathf.PI * (_phase / AirTime)) : 0f;
            transform.localPosition = _restPosition + Vector3.up * (rise * _height);
        }
    }
}
