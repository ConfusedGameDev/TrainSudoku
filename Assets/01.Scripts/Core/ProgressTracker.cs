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

        public CompletionResult(double time, double bestTime, bool isNewBest)
        {
            Time = time;
            BestTime = bestTime;
            IsNewBest = isNewBest;
        }
    }

    /// <summary>Best times and unlocking (PRD section 6) on top of an <see cref="ISaveStore"/>.</summary>
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

        /// <summary>Saves the time when it beats the stored best (or none exists) and reports the outcome.</summary>
        public CompletionResult RecordCompletion(string levelId, double seconds)
        {
            if (levelId == null) throw new ArgumentNullException(nameof(levelId));
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "A run cannot take negative time.");

            var isNewBest = !_store.TryGetBestTime(levelId, out var best) || seconds < best;
            if (isNewBest)
            {
                best = seconds;
                _store.SetBestTime(levelId, seconds);
            }

            return new CompletionResult(seconds, best, isNewBest);
        }

        /// <summary>Formats seconds as m:ss.t, for example 1:05.3. Hours roll into the minutes.</summary>
        public static string FormatTime(double seconds)
        {
            if (seconds < 0) seconds = 0;
            var tenths = (long)Math.Floor(seconds * 10 + 1e-9);
            var minutes = tenths / 600;
            var rest = tenths % 600;
            return $"{minutes}:{rest / 10:00}.{rest % 10}";
        }
    }
}
