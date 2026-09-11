using System;
using System.Collections.Generic;
using TrainSudoku.Core;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// A node shared by two lines. Kept in the schema from the start even though v1 ships one line (D19): going from
    /// a branching network to islands later is an empty array, but going the other way is a save migration.
    /// </summary>
    [Serializable]
    public struct Interchange
    {
        public int lineA;
        public int nodeA;
        public int lineB;
        public int nodeB;
    }

    /// <summary>
    /// Every line the game ships. v1 holds one; the schema scales to five without a screen change (D2, D10).
    /// </summary>
    /// <remarks>
    /// <b>The flat level order is this asset's line order, concatenated.</b> That is what keeps
    /// <see cref="GameFlow.CurrentLevelIndex"/> the save-file identity: appending a second line cannot renumber the
    /// first. Reordering lines, or reordering stations inside one, <i>would</i> renumber — so don't, after release.
    /// </remarks>
    [CreateAssetMenu(menuName = "TrainSudoku/Network Definition", fileName = "Network")]
    public sealed class NetworkDefinition : ScriptableObject
    {
        [SerializeField] private LineDefinition[] lines = Array.Empty<LineDefinition>();

        [Tooltip("Nodes shared by two lines. Empty until a second line exists (D19).")]
        [SerializeField] private Interchange[] interchanges = Array.Empty<Interchange>();

        [Tooltip("The box the network map is drawn inside, in the same map space as each line's nodes.")]
        [SerializeField] private Rect mapBounds = new Rect(0f, 0f, 1000f, 1000f);

        public IReadOnlyList<LineDefinition> Lines => lines ?? Array.Empty<LineDefinition>();
        public IReadOnlyList<Interchange> Interchanges => interchanges ?? Array.Empty<Interchange>();
        public Rect MapBounds => mapBounds;

        public int LineCount => lines != null ? lines.Length : 0;

        public LineDefinition Line(int index) =>
            lines != null && index >= 0 && index < lines.Length ? lines[index] : null;

        /// <summary>
        /// Every level across every line, in line order. This is the flat list the flow and the save file use, so its
        /// order is the save-file identity.
        /// </summary>
        public List<LevelDefinition> FlatLevels()
        {
            var result = new List<LevelDefinition>();
            foreach (var line in Lines)
            {
                if (line == null || line.Levels == null) continue;
                foreach (var level in line.Levels.Levels) result.Add(level);
            }

            return result;
        }

        /// <summary>
        /// How the flat list divides into lines, for <see cref="GameFlow"/>. Lines with no stations are skipped, so
        /// a half-built asset cannot produce an empty line, which <see cref="NetworkLayout"/> rejects.
        /// </summary>
        public NetworkLayout ToLayout()
        {
            var counts = new List<int>();
            foreach (var line in Lines)
            {
                var count = line != null ? line.StationCount : 0;
                if (count > 0) counts.Add(count);
            }

            return counts.Count > 0 ? new NetworkLayout(counts) : NetworkLayout.Single(1);
        }

#if UNITY_EDITOR
        public void SetLines(LineDefinition[] newLines) => lines = newLines ?? Array.Empty<LineDefinition>();

        /// <summary>
        /// Editor-only. A line placed by a tool brings its own interchange with it — the node it hangs off is decided
        /// by the same pass that decides where the line goes, so the two have to be written together.
        /// </summary>
        public void SetInterchanges(Interchange[] newInterchanges) =>
            interchanges = newInterchanges ?? Array.Empty<Interchange>();
#endif
    }
}
