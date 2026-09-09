using System.Collections.Generic;

namespace TrainSudoku.Core
{
    /// <summary>Persistent progress: best times keyed by level id. Level N unlocks when level N-1 has a best time.</summary>
    public interface ISaveStore
    {
        bool TryGetBestTime(string levelId, out double seconds);
        void SetBestTime(string levelId, double seconds);
    }

    public sealed class InMemorySaveStore : ISaveStore
    {
        private readonly Dictionary<string, double> _bestTimes = new Dictionary<string, double>();

        public bool TryGetBestTime(string levelId, out double seconds) => _bestTimes.TryGetValue(levelId, out seconds);

        public void SetBestTime(string levelId, double seconds) => _bestTimes[levelId] = seconds;
    }
}
