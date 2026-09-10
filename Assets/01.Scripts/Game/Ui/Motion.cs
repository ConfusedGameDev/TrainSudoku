using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// The small tween runner every screen animation goes through (work order 9). One call gives a normalised 0-to-1
    /// value for a fixed duration, plus the easing curves the table asks for.
    /// </summary>
    /// <remarks>
    /// <b>Unscaled, always.</b> Every row of the motion table is specified on unscaled time so it survives the pause,
    /// and this is the single place that is decided: the step reads <see cref="Time.realtimeSinceStartup"/> rather
    /// than accumulating deltas, so a dropped frame shortens the animation instead of stretching it past its stated
    /// duration.
    ///
    /// UI Toolkit's own <c>experimental.animation</c> is not used: it animates resolved style values through the
    /// panel's animation system, which gives no way to drive a <see cref="Painter2D"/> repaint — and the line
    /// opening on the network map is exactly that.
    /// </remarks>
    public static class Motion
    {
        /// <summary>One screen replacing another (work order 9). Also the outgoing screen's fade.</summary>
        public const float ScreenChange = 0.26f;

        private const long TickMilliseconds = 16;

        /// <summary>
        /// Runs <paramref name="step"/> with 0 to 1 over <paramref name="duration"/> seconds, then
        /// <paramref name="done"/>. Pause the returned item to cancel; it stops on its own at the end and dies with
        /// the element it was started on.
        /// </summary>
        public static IVisualElementScheduledItem Play(VisualElement host, float duration, Action<float> step, Action done = null)
        {
            if (host == null || step == null) return null;

            var start = Time.realtimeSinceStartup;
            step(0f);

            IVisualElementScheduledItem item = null;
            item = host.schedule.Execute(() =>
            {
                var t = duration <= 0f ? 1f : Mathf.Clamp01((Time.realtimeSinceStartup - start) / duration);
                step(t);
                if (t < 1f) return;
                item.Pause();
                done?.Invoke();
            }).Every(TickMilliseconds);
            return item;
        }

        /// <summary>Fast then settling. What an element arriving on screen should do.</summary>
        public static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>Slow then leaving. What an element being dismissed should do.</summary>
        public static float EaseIn(float t) => t * t;

        /// <summary>A single pop out to <paramref name="peak"/> and back, full size at both ends.</summary>
        public static float Pop(float t, float peak) => Mathf.LerpUnclamped(1f, peak, Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI));

        /// <summary>The share of a longer sequence that has elapsed for an item starting at <paramref name="offset"/>.</summary>
        public static float Stage(float elapsed, float offset, float duration) =>
            duration <= 0f ? 1f : Mathf.Clamp01((elapsed - offset) / duration);
    }
}
