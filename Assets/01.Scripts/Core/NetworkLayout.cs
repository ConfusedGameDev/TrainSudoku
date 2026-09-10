using System;
using System.Collections.Generic;

namespace TrainSudoku.Core
{
    /// <summary>
    /// How the flat list of level ids is cut into lines. The network is a UI idea, but the flow needs to know where
    /// one line ends and the next begins, so the shape lives here as plain data.
    /// </summary>
    /// <remarks>
    /// <b>The flat index stays the save-file identity.</b> Lines are a view over the same ordered list the game has
    /// always had, not a replacement for it, so adding a second line cannot renumber the first and no save migration
    /// is needed. A line is a contiguous run of stations, which is what a transit line is.
    /// </remarks>
    public sealed class NetworkLayout
    {
        private readonly int[] _starts;
        private readonly int[] _counts;

        public int LineCount => _starts.Length;

        /// <summary>Total stations across every line, which is the length of the flat id list.</summary>
        public int StationTotal { get; }

        /// <param name="stationsPerLine">How many stations each line holds, in order. Empty lines are not allowed.</param>
        public NetworkLayout(IReadOnlyList<int> stationsPerLine)
        {
            if (stationsPerLine == null) throw new ArgumentNullException(nameof(stationsPerLine));

            _starts = new int[stationsPerLine.Count];
            _counts = new int[stationsPerLine.Count];
            var running = 0;
            for (var i = 0; i < stationsPerLine.Count; i++)
            {
                var count = stationsPerLine[i];
                if (count <= 0)
                    throw new ArgumentException($"Line {i} has {count} stations; a line must have at least one.", nameof(stationsPerLine));
                _starts[i] = running;
                _counts[i] = count;
                running += count;
            }

            StationTotal = running;
        }

        /// <summary>The whole level list as a single line — the shape the game had before the network existed.</summary>
        public static NetworkLayout Single(int stationCount) => new NetworkLayout(new[] { Math.Max(1, stationCount) });

        public int StationCount(int line) => IsLine(line) ? _counts[line] : 0;

        /// <summary>The flat level index of a station, or -1 if either coordinate is out of range.</summary>
        public int FlatIndex(int line, int station)
        {
            if (!IsLine(line) || station < 0 || station >= _counts[line]) return -1;
            return _starts[line] + station;
        }

        /// <summary>Which line a flat level index sits on, or -1.</summary>
        public int LineOf(int flatIndex)
        {
            if (flatIndex < 0 || flatIndex >= StationTotal) return -1;
            for (var line = 0; line < _starts.Length; line++)
                if (flatIndex < _starts[line] + _counts[line]) return line;
            return -1;
        }

        /// <summary>The station's position along its own line, or -1.</summary>
        public int StationOf(int flatIndex)
        {
            var line = LineOf(flatIndex);
            return line < 0 ? -1 : flatIndex - _starts[line];
        }

        /// <summary>True when the flat index is the last station on its line.</summary>
        public bool IsTerminus(int flatIndex)
        {
            var line = LineOf(flatIndex);
            return line >= 0 && flatIndex == _starts[line] + _counts[line] - 1;
        }

        /// <summary>The ids of one line's stations, in order. Empty for an unknown line.</summary>
        public IReadOnlyList<string> LineIds(IReadOnlyList<string> allIds, int line)
        {
            if (allIds == null || !IsLine(line)) return Array.Empty<string>();
            var result = new List<string>(_counts[line]);
            for (var i = 0; i < _counts[line]; i++)
            {
                var flat = _starts[line] + i;
                if (flat < allIds.Count) result.Add(allIds[flat]);
            }

            return result;
        }

        private bool IsLine(int line) => line >= 0 && line < _starts.Length;
    }
}
