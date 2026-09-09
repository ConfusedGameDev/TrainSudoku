using System.Collections.Generic;

namespace TrainSudoku.Core
{
    /// <summary>
    /// Persistent progress (PRD section 6): best times keyed by level id, from which unlocking derives, plus the
    /// in-progress snapshot of any level the player left unfinished.
    /// </summary>
    public interface ISaveStore
    {
        bool TryGetBestTime(string levelId, out double seconds);
        void SetBestTime(string levelId, double seconds);

        bool TryGetProgress(string levelId, out LevelProgress progress);
        void SetProgress(string levelId, LevelProgress progress);
        void ClearProgress(string levelId);
    }

    public sealed class InMemorySaveStore : ISaveStore
    {
        private readonly Dictionary<string, double> _bestTimes = new Dictionary<string, double>();
        private readonly Dictionary<string, LevelProgress> _progress = new Dictionary<string, LevelProgress>();

        public bool TryGetBestTime(string levelId, out double seconds) => _bestTimes.TryGetValue(levelId, out seconds);

        public void SetBestTime(string levelId, double seconds) => _bestTimes[levelId] = seconds;

        public bool TryGetProgress(string levelId, out LevelProgress progress) => _progress.TryGetValue(levelId, out progress);

        public void SetProgress(string levelId, LevelProgress progress) => _progress[levelId] = progress;

        public void ClearProgress(string levelId) => _progress.Remove(levelId);
    }
}
