using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>How a line is drawn on its own map.</summary>
    public enum MapShape
    {
        /// <summary>An open run from one terminus to the other.</summary>
        Route,

        /// <summary>A closed loop; the last node joins back to the first.</summary>
        Loop,

        /// <summary>
        /// A closed loop drawn as a stadium — two semicircular caps joined by straights — fitted to the bounding box
        /// of the nodes, with a radius of half the shorter side. The nodes place the stations on that curve and are
        /// not joined to each other, so this is the one shape whose stations are not on a 45-degree lattice.
        /// </summary>
        Stadium,
    }

    /// <summary>What it takes to open a line. Derived from stars on every launch, never stored (5.5).</summary>
    [Serializable]
    public struct UnlockRule
    {
        [Tooltip("Stars needed on EVERY station of the previous line. v1 is the lenient 1 (D20).")]
        [Range(0, 3)] public int minStarsPerStation;

        public static UnlockRule Default => new UnlockRule { minStarsPerStation = 1 };
    }

    /// <summary>
    /// One line of the network: its stations, its colour, and the polyline its map is drawn from. The stations are
    /// the levels, so a line is a <see cref="LevelCollection"/> with a identity and a shape wrapped around it.
    /// </summary>
    /// <remarks>
    /// <b>The colour lives here and nowhere else.</b> Screens read it through the shell's tint classes, never by
    /// name, which is what lets a second line ship without touching a screen.
    /// </remarks>
    [CreateAssetMenu(menuName = "TrainSudoku/Line Definition", fileName = "Line")]
    public sealed class LineDefinition : ScriptableObject
    {
        [Tooltip("Stable identity. Unlike a level id this is not a save-file key, but keep it stable anyway.")]
        [SerializeField] private string id = "";

        [Tooltip("The roundel's two letters, e.g. TS.")]
        [SerializeField] private string code = "";

        [Tooltip("An invented proper noun, untranslated, like any line on a real map (D14).")]
        [SerializeField] private string displayName = "";

        [SerializeField] private Color color = new Color32(0x9A, 0xCD, 0x32, 0xFF);
        [SerializeField] private LevelCollection levels;
        [SerializeField] private UnlockRule unlockRule = new UnlockRule { minStarsPerStation = 1 };
        [SerializeField] private MapShape mapShape = MapShape.Route;

        [Tooltip("The polyline the map is stroked along, in map space. Authored on a 45-degree snap grid.")]
        [SerializeField] private Vector2[] mapNodes = Array.Empty<Vector2>();

        [Tooltip("Which nodes carry a station, in the same order as the levels. Length should equal the level count.")]
        [SerializeField] private int[] stationNodeIndices = Array.Empty<int>();

        public string Id => id;
        public string Code => code;
        public string DisplayName => displayName;
        public Color Color => color;
        public LevelCollection Levels => levels;
        public UnlockRule UnlockRule => unlockRule;
        public MapShape MapShape => mapShape;
        public IReadOnlyList<Vector2> MapNodes => mapNodes ?? Array.Empty<Vector2>();
        public IReadOnlyList<int> StationNodeIndices => stationNodeIndices ?? Array.Empty<int>();

        /// <summary>Stations on this line. Zero when no collection is assigned, which is a half-built asset, not a crash.</summary>
        public int StationCount => levels != null ? levels.Count : 0;

        public LevelDefinition Station(int index) =>
            levels != null && index >= 0 && index < levels.Count ? levels[index] : null;

        /// <summary>True when the line has content, which is what decides whether it appears on the network map (D10).</summary>
        public bool HasContent => StationCount > 0;

#if UNITY_EDITOR
        /// <summary>Editor-only setup, used by the asset-authoring tools.</summary>
        public void SetUp(string newId, string newCode, string newDisplayName, Color newColor, LevelCollection newLevels)
        {
            id = newId;
            code = newCode;
            displayName = newDisplayName;
            color = newColor;
            levels = newLevels;
        }

        public void SetMap(MapShape shape, Vector2[] nodes, int[] stationNodes)
        {
            mapShape = shape;
            mapNodes = nodes ?? Array.Empty<Vector2>();
            stationNodeIndices = stationNodes ?? Array.Empty<int>();
        }
#endif
    }
}
