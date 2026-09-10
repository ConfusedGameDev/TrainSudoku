using System;
using System.Collections.Generic;

namespace TrainSudoku.Core
{
    public readonly struct CompletionResult
    {
        /// <summary>Time of the run that just finished.</summary>
        public double Time { get; }

        /// <summary>Best time after recording this run.</summary>
        public double BestTime { get; }

        public bool IsNewBest { get; }

        /// <summary>Stars for the run that just finished, 1 to 3.</summary>
        public int Stars { get; }

        /// <summary>True when this run beat the stars the level was already carrying.</summary>
        public bool IsNewBestStars { get; }

        public CompletionResult(double time, double bestTime, bool isNewBest, int stars = 0, bool isNewBestStars = false)
        {
            Time = time;
            BestTime = bestTime;
            IsNewBest = isNewBest;
            Stars = stars;
            IsNewBestStars = isNewBestStars;
        }
    }

    /// <summary>Best times, unlocking and in-progress snapshots (PRD section 6) on top of an <see cref="ISaveStore"/>.</summary>
    public sealed class ProgressTracker
    {
        private readonly ISaveStore _store;

        public ProgressTracker(ISaveStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public bool TryGetBestTime(string levelId, out double seconds) => _store.TryGetBestTime(levelId, out seconds);

        /// <summary>The first level is always unlocked; level N unlocks once level N-1 has a best time.</summary>
        public bool IsUnlocked(IReadOnlyList<string> levelIds, int index)
        {
            if (levelIds == null) throw new ArgumentNullException(nameof(levelIds));
            if (index < 0 || index >= levelIds.Count) return false;
            return index == 0 || _store.TryGetBestTime(levelIds[index - 1], out _);
        }

        /// <summary>
        /// Saves the time when it beats the stored best (or none exists), awards the run's stars, and reports the
        /// outcome. Time and stars are kept independently: a slower run never lowers either.
        /// </summary>
        /// <param name="starTimes">
        /// Thresholds in seconds, fastest first: <c>[0]</c> earns 3 stars, <c>[1]</c> earns 2. Null or short means
        /// the level has no thresholds authored yet, and finishing it earns the floor of 1 star.
        /// </param>
        public CompletionResult RecordCompletion(string levelId, double seconds, IReadOnlyList<double> starTimes = null)
        {
            if (levelId == null) throw new ArgumentNullException(nameof(levelId));
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "A run cannot take negative time.");

            var isNewBest = !_store.TryGetBestTime(levelId, out var best) || seconds < best;
            if (isNewBest)
            {
                best = seconds;
                _store.SetBestTime(levelId, seconds);
            }

            var stars = StarsFor(seconds, starTimes);
            var isNewBestStars = !_store.TryGetStars(levelId, out var storedStars) || stars > storedStars;
            if (isNewBestStars) _store.SetStars(levelId, stars);

            return new CompletionResult(seconds, best, isNewBest, stars, isNewBestStars);
        }

        /// <summary>Stars earned on a level, or 0 when it has never been completed.</summary>
        public int GetStars(string levelId)
        {
            if (levelId == null) return 0;
            return _store.TryGetStars(levelId, out var stars) ? stars : 0;
        }

        /// <summary>Total stars across a line's levels.</summary>
        public int StarsOnLine(IEnumerable<string> levelIds)
        {
            if (levelIds == null) return 0;
            var total = 0;
            foreach (var id in levelIds) total += GetStars(id);
            return total;
        }

        /// <summary>
        /// Whether the line after <paramref name="previousLineIds"/> is open: every station on the previous line must
        /// carry at least <paramref name="minStars"/>. Derived, never stored, so changing the rule ships without a
        /// save migration. An empty previous line means this is the first line, which is always open.
        /// </summary>
        public bool IsLineUnlocked(IReadOnlyList<string> previousLineIds, int minStars)
        {
            if (previousLineIds == null || previousLineIds.Count == 0) return true;
            foreach (var id in previousLineIds)
                if (GetStars(id) < minStars) return false;
            return true;
        }

        /// <summary>
        /// Fills in a star rating for a level that has a best time but no stored stars. This is the version 1 to
        /// version 2 save migration: v1 files record times only, so the rating is recovered from the time the first
        /// time the level's thresholds are known. Returns true when something was awarded.
        /// </summary>
        public bool AwardMissingStars(string levelId, IReadOnlyList<double> starTimes)
        {
            if (levelId == null) return false;
            if (_store.TryGetStars(levelId, out _)) return false;
            if (!_store.TryGetBestTime(levelId, out var best)) return false;

            _store.SetStars(levelId, StarsFor(best, starTimes));
            return true;
        }

        /// <summary>
        /// Stars for a finished run. Completing a level is always worth at least one star, so an unauthored or
        /// malformed threshold list costs the player nothing.
        /// </summary>
        public static int StarsFor(double seconds, IReadOnlyList<double> starTimes)
        {
            if (starTimes == null || starTimes.Count < 2) return 1;
            var three = starTimes[0];
            var two = starTimes[1];
            if (three > 0 && seconds <= three) return 3;
            if (two > 0 && seconds <= two) return 2;
            return 1;
        }

        public bool TryGetInProgress(string levelId, out LevelProgress progress)
        {
            progress = null;
            return levelId != null && _store.TryGetProgress(levelId, out progress);
        }

        /// <summary>Remembers a level mid-play so it can be continued later.</summary>
        public void SaveInProgress(string levelId, LevelProgress progress)
        {
            if (levelId == null) throw new ArgumentNullException(nameof(levelId));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            _store.SetProgress(levelId, progress);
        }

        /// <summary>Forgets the snapshot: the level was won or retried.</summary>
        public void ClearInProgress(string levelId)
        {
            if (levelId == null) throw new ArgumentNullException(nameof(levelId));
            _store.ClearProgress(levelId);
        }

        /// <summary>
        /// Formats seconds as a station clock, mm:ss — for example 01:05. Hours roll into the minutes rather than
        /// adding a third field, and the whole seconds are <b>floored</b>: a clock that showed a second the timer has
        /// not reached would be lying, and the star thresholds are compared against the raw seconds anyway, never
        /// against this string, so what is shown and what is scored cannot disagree.
        /// </summary>
        public static string FormatTime(double seconds)
        {
            if (seconds < 0) seconds = 0;
            // The nudge only catches accumulated float error: a value within 1e-9 under a whole second is that second.
            var whole = (long)Math.Floor(seconds + 1e-9);
            return $"{whole / 60:00}:{whole % 60:00}";
        }
    }
}
