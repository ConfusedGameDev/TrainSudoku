using System.Collections.Generic;

namespace TrainSudoku.Core
{
    /// <summary>
    /// The live star tier as the clock runs: 3 while three stars are still reachable, then 2, then 1. It is what the
    /// music ladder rides on, so it must agree with <see cref="ProgressTracker.StarsFor"/> — the track the player
    /// hears and the number the arrival screen prints are the same fact.
    /// </summary>
    /// <remarks>
    /// Two things are deliberate. The thresholds are <b>copied once</b> in <see cref="Begin"/> and the list is never
    /// touched again, because the Unity layer reads them off a <c>LevelDefinition</c> whose getter allocates a fresh
    /// array on every call and this is ticked every frame. And the tier is <b>one-way by construction</b>: only
    /// <see cref="Begin"/> can raise it, so a clock that jumps backwards cannot make the music climb.
    /// </remarks>
    public sealed class StarLadder
    {
        private double _three;
        private double _two;

        /// <summary>The live tier, 1 to 3. Starts at 3 and falls; a level that has not begun reads 3.</summary>
        public int Tier { get; private set; } = 3;

        /// <summary>
        /// Starts a level's ladder. The tier is <b>seeded from</b> <paramref name="elapsedSeconds"/> rather than always
        /// set to 3, because a continued attempt already past its two-star time must open on the one-star track rather
        /// than open on three and drop a frame later.
        /// </summary>
        public void Begin(IReadOnlyList<double> starTimes, double elapsedSeconds = 0)
        {
            if (starTimes == null || starTimes.Count < 2)
            {
                _three = 0;
                _two = 0;
            }
            else
            {
                _three = starTimes[0];
                _two = starTimes[1];
            }

            Tier = TierFor(elapsedSeconds, _three, _two);
        }

        /// <summary>Advances the ladder. Returns true on the frame the tier descends, which is the cue to crossfade.</summary>
        public bool Tick(double elapsedSeconds)
        {
            var next = TierFor(elapsedSeconds, _three, _two);
            if (next >= Tier) return false;
            Tier = next;
            return true;
        }

        /// <summary>
        /// Mirrors <see cref="ProgressTracker.StarsFor"/>, with one deliberate difference: <b>an unauthored ladder
        /// holds tier 3</b> instead of returning the floor of 1.
        /// </summary>
        /// <remarks>
        /// Returning 1 is right for scoring — finishing a level is always worth a star — and wrong for music. A live
        /// ladder that opened on the one-star track would tell the player they had already failed before they touched
        /// the board, which is a thing the game does not mean and has no way to take back. Holding the top track says
        /// nothing, which is the correct thing to say when there is nothing to say.
        /// </remarks>
        public static int TierFor(double seconds, double three, double two)
        {
            if (three <= 0 && two <= 0) return 3;
            if (three > 0 && seconds <= three) return 3;
            if (two > 0 && seconds <= two) return 2;
            return 1;
        }
    }
}
