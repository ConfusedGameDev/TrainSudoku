using TrainSudoku.Core;

namespace TrainSudoku.XR.Rules
{
    /// <summary>A background loop of the XR edition's music.</summary>
    public enum XRMusicLoop
    {
        None,

        /// <summary>The placement and the maps.</summary>
        Concourse,

        /// <summary>A station in play, and its pause.</summary>
        Platform,
    }

    /// <summary>
    /// What the XR edition's music plays in each flow state (XR-PRD 9). The map follows the phone's <c>MusicPlan</c>
    /// (D22): one loop for the maps, one for play, the pause keeping play's loop, and no loop over the train run or
    /// the arrival. Where it departs from the phone is the arrival: each line has its own jingle, as each station on
    /// the Yamanote line has its own melody. It starts the moment the board is solved, plays while the train runs the
    /// rails, and fades out as the results come up.
    /// </summary>
    public static class XRMusicPlan
    {
        /// <summary>One jingle per line in <c>Network.asset</c>. More lines than jingles reuse them in order.</summary>
        public const int JingleCount = 24;

        public static XRMusicLoop Loop(GameState state)
        {
            switch (state)
            {
                case GameState.Play:
                case GameState.Pause:
                    return XRMusicLoop.Platform;
                case GameState.TrainRun:
                case GameState.Win:
                    return XRMusicLoop.None;
                default:
                    return XRMusicLoop.Concourse;
            }
        }

        /// <summary>The pause turns its loop down rather than stopping it: the signboard's pause has no music of its own.</summary>
        public static bool Ducked(GameState state) => state == GameState.Pause;

        /// <summary>The line's jingle starts as the train sets off.</summary>
        public static bool StartsJingle(GameState state) => state == GameState.TrainRun;

        /// <summary>The jingle plays on only over the train run; anywhere else it fades out.</summary>
        public static bool FadesJingle(GameState state) => state != GameState.TrainRun;

        /// <summary>Which jingle <paramref name="lineIndex"/> arrives to, or -1 with no line.</summary>
        public static int Jingle(int lineIndex) => lineIndex < 0 ? -1 : lineIndex % JingleCount;
    }
}
