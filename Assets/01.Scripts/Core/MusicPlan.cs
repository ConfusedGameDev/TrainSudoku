namespace TrainSudoku.Core
{
    /// <summary>
    /// One music track. <see cref="None"/> is explicitly 0 so a half-authored library row means silence rather than
    /// the main menu theme.
    /// </summary>
    public enum MusicTrack
    {
        None = 0,
        MainMenu,
        Map,

        /// <summary>In-game, while three stars are still reachable. The ladder only ever descends from here.</summary>
        PlayThreeStar,
        PlayTwoStar,
        PlayOneStar,
    }

    /// <summary>
    /// What should be playing, given where the game is and how the clock is doing. The whole rule is one switch, and
    /// it is here rather than in the Unity layer so it can be tested without a scene.
    /// </summary>
    public static class MusicPlan
    {
        /// <param name="starTier">The live star tier from <see cref="StarLadder"/>, 1 to 3. Values outside clamp.</param>
        public static MusicTrack For(GameState state, int starTier)
        {
            switch (state)
            {
                case GameState.MainMenu:
                    return MusicTrack.MainMenu;

                // Both are maps — the network is every line, Level Select is one of them — and crossing between them
                // must not interrupt the track, which the player hears as one continuous place.
                case GameState.Network:
                case GameState.LevelSelect:
                    return MusicTrack.Map;

                // Pause deliberately answers the same track as Play. That is what makes pausing a Pause/UnPause on the
                // source rather than a crossfade out to silence and back in.
                case GameState.Play:
                case GameState.Pause:
                    if (starTier <= 1) return MusicTrack.PlayOneStar;
                    if (starTier == 2) return MusicTrack.PlayTwoStar;
                    return MusicTrack.PlayThreeStar;

                // The win fades the music out: the train's own loop and the arrival fanfare carry that whole beat, and
                // the map track comes back when the player reaches the map.
                case GameState.TrainRun:
                case GameState.Win:
                    return MusicTrack.None;

                default:
                    return MusicTrack.None;
            }
        }
    }
}
