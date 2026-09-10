using System;
using System.Collections.Generic;
using TrainSudoku.Core;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// A level as stored on disk (PRD section 9.1). Gameplay code converts it to a <see cref="LevelData"/> with
    /// <see cref="ToLevelData"/>; the level editor writes it with <see cref="SetFrom"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "TrainSudoku/Level Definition", fileName = "Level")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [Serializable]
        public struct FixedPieceEntry
        {
            public int x;
            public int y;
            public PieceKey key;
        }

        [Serializable]
        public struct TunnelEntry
        {
            public Direction side;
            public int index;

            public Tunnel ToTunnel() => new Tunnel(side, index);

            public static TunnelEntry From(Tunnel tunnel) => new TunnelEntry { side = tunnel.Side, index = tunnel.Index };
        }

        [Tooltip("Stable identity used by the save file. Renaming the asset must not change it.")]
        [SerializeField] private string id = "";
        [SerializeField] private string displayName = "";
        [SerializeField] private int width = 6;
        [SerializeField] private int height = 6;
        [SerializeField] private int[] columnClues = new int[6];
        [SerializeField] private int[] rowClues = new int[6];
        [SerializeField] private FixedPieceEntry[] fixedPieces = Array.Empty<FixedPieceEntry>();
        [SerializeField] private TunnelEntry entrance = new TunnelEntry { side = Direction.West, index = 0 };
        [SerializeField] private TunnelEntry exit = new TunnelEntry { side = Direction.East, index = 0 };

        [Tooltip("Seconds: [0] earns three stars, [1] earns two. Anything slower earns one. Hand-authored from real " +
                 "play, never derived from solver node count — that measures search-tree size, not how long a person takes.")]
        [SerializeField] private float[] starTimes = new float[2];

        public string Id => id;

        /// <summary>The station's name. An invented proper noun, untranslated, like any station on a real map (D14).</summary>
        public string DisplayName => displayName;
        public int Width => width;
        public int Height => height;

        /// <summary>
        /// Star thresholds in seconds, fastest first. Always two entries, so a half-authored asset still answers.
        /// A zero threshold means "unauthored" and is treated as unreachable, which costs the player nothing.
        /// </summary>
        public IReadOnlyList<double> StarTimes => new[]
        {
            starTimes != null && starTimes.Length > 0 ? starTimes[0] : 0d,
            starTimes != null && starTimes.Length > 1 ? starTimes[1] : 0d,
        };

        /// <summary>Sets both thresholds. Used by the level editor; the game only ever reads them.</summary>
        public void SetStarTimes(float threeStars, float twoStars) => starTimes = new[] { threeStars, twoStars };

        /// <summary>Builds the Core representation. Tolerates a half-edited asset: sizes are clamped to at least 1 and short arrays are padded.</summary>
        public LevelData ToLevelData()
        {
            var level = new LevelData(Mathf.Max(1, width), Mathf.Max(1, height)) { Name = displayName ?? "" };
            CopyClues(columnClues, level.ColumnClues);
            CopyClues(rowClues, level.RowClues);
            if (fixedPieces != null)
                foreach (var entry in fixedPieces)
                    level.FixedPieces.Add(new FixedPiece(entry.x, entry.y, entry.key));
            level.Entrance = entrance.ToTunnel();
            level.Exit = exit.ToTunnel();
            return level;
        }

        /// <summary>
        /// Overwrites every field from the Core representation. The id is kept unless a new one is supplied, and
        /// <b>the star thresholds are deliberately left alone</b>: they are hand-authored from real play and are not
        /// part of <see cref="LevelData"/>, so re-saving a level from the editor must never wipe them.
        /// </summary>
        public void SetFrom(LevelData level, string newId = null)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));

            if (newId != null) id = newId;
            displayName = level.Name ?? "";
            width = level.Width;
            height = level.Height;
            columnClues = (int[])level.ColumnClues.Clone();
            rowClues = (int[])level.RowClues.Clone();
            fixedPieces = new FixedPieceEntry[level.FixedPieces.Count];
            for (var i = 0; i < fixedPieces.Length; i++)
            {
                var piece = level.FixedPieces[i];
                fixedPieces[i] = new FixedPieceEntry { x = piece.X, y = piece.Y, key = piece.Key };
            }

            entrance = TunnelEntry.From(level.Entrance);
            exit = TunnelEntry.From(level.Exit);
        }

        private static void CopyClues(int[] source, int[] target)
        {
            if (source == null) return;
            Array.Copy(source, target, Mathf.Min(source.Length, target.Length));
        }
    }
}
