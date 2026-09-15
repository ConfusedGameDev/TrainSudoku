using System.Collections.Generic;
using TrainSudoku.Core;

namespace TrainSudoku.XR.Rules
{
    /// <summary>An entry on the wrist menu (XR-PRD 6.4).</summary>
    public enum WristItem
    {
        Resume,
        Retry,
        BackToMap,
        BackToNetwork,
        Settings,
        Close,
    }

    /// <summary>What pressing the wrist button, or the controller's menu button, asks of the shell.</summary>
    public enum WristToggle
    {
        Nothing,
        Open,

        /// <summary>In play the menu opens as the pause (6.2).</summary>
        OpenAndPause,

        /// <summary>The menu closes; a pause stays a pause. Only RESUME restarts the clock.</summary>
        Close,
    }

    /// <summary>
    /// The wrist menu's entries in each flow state (XR-PRD 6.2, 6.4) and what its button does. Play and Pause offer
    /// the same entries because the menu never shows over play: opening it pauses. The train run and the arrival
    /// offer nothing, and the button hides.
    /// </summary>
    public static class WristMenuPlan
    {
        private static readonly WristItem[] None = new WristItem[0];
        private static readonly WristItem[] OnNetwork = { WristItem.Settings, WristItem.Close };
        private static readonly WristItem[] OnLine = { WristItem.BackToNetwork, WristItem.Settings, WristItem.Close };
        private static readonly WristItem[] Paused = { WristItem.Resume, WristItem.Retry, WristItem.BackToMap, WristItem.Settings };

        public static bool Offered(GameState state) => Items(state).Count > 0;

        public static IReadOnlyList<WristItem> Items(GameState state)
        {
            switch (state)
            {
                case GameState.Network:
                    return OnNetwork;
                case GameState.LevelSelect:
                    return OnLine;
                case GameState.Play:
                case GameState.Pause:
                    return Paused;
                default:
                    return None;
            }
        }

        /// <summary>
        /// The wrist button was pressed in <paramref name="state"/>, with the menu <paramref name="open"/> or not.
        /// Closing never resumes: a stray touch on the button must not restart the clock, so only RESUME does (revised
        /// after the first XR8 headset check, where the game resumed on its own).
        /// </summary>
        public static WristToggle Toggle(GameState state, bool open)
        {
            if (open) return WristToggle.Close;
            if (!Offered(state)) return WristToggle.Nothing;
            return state == GameState.Play ? WristToggle.OpenAndPause : WristToggle.Open;
        }

        /// <summary>Losing focus or being suspended pauses play (6.3). No other state has a clock to stop.</summary>
        public static bool PausesOnFocusLoss(GameState state) => state == GameState.Play;
    }
}
