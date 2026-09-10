using System.Collections.Generic;

namespace TrainSudoku.Core
{
    /// <summary>
    /// Persistent progress (PRD section 6): best times keyed by level id, from which station unlocking derives, the
    /// star rating of each level, and the in-progress snapshot of any level the player left unfinished.
    /// </summary>
    /// <remarks>
    /// Stars are stored rather than derived from the best time so a later change to a level's thresholds cannot take
    /// away a rating the player already earned. Line unlocking, by contrast, is always recomputed from the stars, so
    /// changing that rule ships without a save migration.
    /// </remarks>
    public interface ISaveStore
    {
        bool TryGetBestTime(string levelId, out double seconds);
        void SetBestTime(string levelId, double seconds);

        /// <summary>Stars earned on a level, 1 to 3. False when the level has never been completed.</summary>
        bool TryGetStars(string levelId, out int stars);
        void SetStars(string levelId, int stars);

        bool TryGetProgress(string levelId, out LevelProgress progress);
        void SetProgress(string levelId, LevelProgress progress);
        void ClearProgress(string levelId);
    }

    public sealed class InMemorySaveStore : ISaveStore
    {
        private readonly Dictionary<string, double> _bestTimes = new Dictionary<string, double>();
        private readonly Dictionary<string, int> _stars = new Dictionary<string, int>();
        private readonly Dictionary<string, LevelProgress> _progress = new Dictionary<string, LevelProgress>();

        public bool TryGetBestTime(string levelId, out double seconds) => _bestTimes.TryGetValue(levelId, out seconds);

        public void SetBestTime(string levelId, double seconds) => _bestTimes[levelId] = seconds;

        public bool TryGetStars(string levelId, out int stars)
        {
            stars = 0;
            return levelId != null && _stars.TryGetValue(levelId, out stars);
        }

        public void SetStars(string levelId, int stars) => _stars[levelId] = stars;

        public bool TryGetProgress(string levelId, out LevelProgress progress) => _progress.TryGetValue(levelId, out progress);

        public void SetProgress(string levelId, LevelProgress progress) => _progress[levelId] = progress;

        public void ClearProgress(string levelId) => _progress.Remove(levelId);
    }
}
