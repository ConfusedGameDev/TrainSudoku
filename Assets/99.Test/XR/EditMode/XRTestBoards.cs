using TrainSudoku.Core;

namespace TrainSudoku.XR.Tests
{
    /// <summary>
    /// Boards for the XR rules tests. XR keeps its own fixtures rather than referencing the phone's test assembly,
    /// which would pull the phone's Game and Editor assemblies into XR's tests.
    /// </summary>
    internal static class XRTestBoards
    {
        /// <summary>The Plan.md map with its two fixed pieces: SW at (1,2) and NW at (4,4).</summary>
        private const string PlanExampleText =
            "name: Plan Example\n" +
            "  2 3 1 1 3 1\n" +
            ". . . . . . 0\n" +
            ". . . . . . 0\n" +
            ". SW . . . . 4 E\n" +
            "S . . . . . . 3\n" +
            ". . . . NW . 4\n" +
            ". . . . . . 0\n";

        /// <summary>
        /// 3x3 board, entrance west of (0,1), exit east of (2,1), all clues zero (legality ignores clues). On the empty
        /// board every key is legal at (1,1); (0,1) must connect west to the entrance; the corner (0,0) takes only SE.
        /// </summary>
        public static Board Corridor() =>
            new Board(new LevelData(3, 3) { Entrance = new Tunnel(Direction.West, 1), Exit = new Tunnel(Direction.East, 1) });

        public static Board PlanExample() => new Board(LevelText.Parse(PlanExampleText));
    }
}
