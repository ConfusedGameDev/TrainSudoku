using System;
using System.IO;
using System.Text;

namespace TrainSudoku.Core
{
    /// <summary>
    /// <see cref="ISaveStore"/> backed by one JSON file (PRD section 6). Loads on construction and writes through on
    /// every change (a new best time, an in-progress snapshot), via a temporary file so a crash mid-write cannot lose
    /// the previous save. An unreadable file is set aside with a <c>.corrupt</c> suffix and progress starts fresh.
    /// </summary>
    public sealed class FileSaveStore : ISaveStore
    {
        private readonly Action<string> _log;
        private SaveData _data;

        public string Path { get; }

        /// <summary>True when the file existed but could not be read on load.</summary>
        public bool LoadedFromCorruptFile { get; private set; }

        public FileSaveStore(string path, Action<string> log = null)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("A file path is required.", nameof(path));
            Path = path;
            _log = log;
            Reload();
        }

        public int Count => _data.BestTimes.Count;

        public bool TryGetBestTime(string levelId, out double seconds)
        {
            if (levelId == null)
            {
                seconds = 0;
                return false;
            }

            return _data.BestTimes.TryGetValue(levelId, out seconds);
        }

        public void SetBestTime(string levelId, double seconds)
        {
            if (levelId == null) throw new ArgumentNullException(nameof(levelId));
            _data.BestTimes[levelId] = seconds;
            Save();
        }

        public bool TryGetProgress(string levelId, out LevelProgress progress)
        {
            progress = null;
            return levelId != null && _data.InProgress.TryGetValue(levelId, out progress);
        }

        public void SetProgress(string levelId, LevelProgress progress)
        {
            if (levelId == null) throw new ArgumentNullException(nameof(levelId));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            _data.InProgress[levelId] = progress;
            Save();
        }

        public void ClearProgress(string levelId)
        {
            if (levelId == null) throw new ArgumentNullException(nameof(levelId));
            if (_data.InProgress.Remove(levelId)) Save();
        }

        /// <summary>Forgets every best time and snapshot and deletes the file.</summary>
        public void Clear()
        {
            _data = new SaveData();
            try
            {
                if (File.Exists(Path)) File.Delete(Path);
            }
            catch (IOException e)
            {
                _log?.Invoke($"Could not delete the save file: {e.Message}");
            }
        }

        public void Reload()
        {
            LoadedFromCorruptFile = false;
            _data = new SaveData();
            if (!File.Exists(Path)) return;

            string text;
            try
            {
                text = File.ReadAllText(Path, Encoding.UTF8);
            }
            catch (IOException e)
            {
                _log?.Invoke($"Could not read the save file: {e.Message}");
                return;
            }

            if (SaveJson.TryRead(text, out var data))
            {
                _data = data;
                return;
            }

            LoadedFromCorruptFile = true;
            _log?.Invoke("The save file is not valid; starting with no progress and keeping a copy with the .corrupt suffix.");
            try
            {
                var corrupt = Path + ".corrupt";
                if (File.Exists(corrupt)) File.Delete(corrupt);
                File.Move(Path, corrupt);
            }
            catch (IOException e)
            {
                _log?.Invoke($"Could not set the corrupt save file aside: {e.Message}");
            }
        }

        private void Save()
        {
            try
            {
                var directory = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                var temp = Path + ".tmp";
                File.WriteAllText(temp, SaveJson.Write(_data), new UTF8Encoding(false));
                if (File.Exists(Path)) File.Replace(temp, Path, null);
                else File.Move(temp, Path);
            }
            catch (IOException e)
            {
                _log?.Invoke($"Could not write the save file: {e.Message}");
            }
            catch (UnauthorizedAccessException e)
            {
                _log?.Invoke($"Could not write the save file: {e.Message}");
            }
        }
    }
}
