using System;
using System.Globalization;
using System.IO;
using TrainSudoku.Core;
using TrainSudoku.Game;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The star-timing sample of XR-PRD 9: how long each station took by hand against the thresholds its asset carries
    /// for the phone, to the log and to <c>xr-star-sample.csv</c> in the app's data folder, where <c>adb pull</c> finds
    /// it. It keeps growing until the sample is big enough to decide on an XR scaling factor.
    /// </summary>
    public static class XRStarSample
    {
        private const string SampleFile = "xr-star-sample.csv";

        private const string Header = "utc,level,name,size,seconds,stars,threeStars,twoStars,grabs,landings,cellCm";

        /// <param name="cellMetres">The size of a cell the station was played at: the handle can scale the board (X6, revised 2026-09-14).</param>
        public static void Record(LevelDefinition station, double seconds, int grabs, int landings, float cellMetres)
        {
            if (station == null) return;
            var times = station.StarTimes;
            var stars = ProgressTracker.StarsFor(seconds, times);
            var size = $"{station.Width}x{station.Height}";
            var cellCm = cellMetres * 100f;
            Debug.Log($"[XR play] {station.DisplayName} ({station.Id}, {size}) solved in {seconds:F1} s by hand: " +
                      $"{stars} star(s) against 3 at {times[0]:F0} s and 2 at {times[1]:F0} s; {grabs} grabs, {landings} landings, {cellCm:F1} cm cells.");

            try
            {
                var path = Path.Combine(Application.persistentDataPath, SampleFile);
                AddCellColumn(path);
                var header = !File.Exists(path);
                using (var writer = File.AppendText(path))
                {
                    if (header) writer.WriteLine(Header);
                    writer.WriteLine(string.Join(",",
                        DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), station.Id, $"\"{station.DisplayName}\"", size,
                        seconds.ToString("F2", CultureInfo.InvariantCulture), stars.ToString(CultureInfo.InvariantCulture),
                        times[0].ToString("F0", CultureInfo.InvariantCulture), times[1].ToString("F0", CultureInfo.InvariantCulture),
                        grabs.ToString(CultureInfo.InvariantCulture), landings.ToString(CultureInfo.InvariantCulture),
                        cellCm.ToString("F1", CultureInfo.InvariantCulture)));
                }
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[XR play] Could not append to {SampleFile}: {e.Message}");
            }
        }

        /// <summary>A sample from before the board could be scaled was all played at 6 cm cells: its rows get that column.</summary>
        private static void AddCellColumn(string path)
        {
            if (!File.Exists(path)) return;
            var lines = File.ReadAllLines(path);
            if (lines.Length == 0 || lines[0].EndsWith(",cellCm", StringComparison.Ordinal)) return;
            lines[0] = Header;
            for (var i = 1; i < lines.Length; i++)
                if (lines[i].Length > 0)
                    lines[i] += ",6.0";
            File.WriteAllLines(path, lines);
        }
    }
}
