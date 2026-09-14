using System;
using System.Collections.Generic;

namespace TrainSudoku.XR.Rules
{
    /// <summary>
    /// Which lines the platform's network map shows (XR-PRD 6.2): every line the player has earned, and the first one
    /// they have not. The same progress rule the phone's overworld follows; the phone keeps its own copy in its UI,
    /// which XR does not share (X21), so this one is tested here.
    /// </summary>
    /// <remarks>
    /// Unlocking runs in line order (<c>GameFlow.IsLineUnlocked</c> opens line <i>n</i> off line <i>n-1</i>), so the
    /// open lines are always a prefix and the reveal stops at the first closed one: exactly one locked line is ever on
    /// the map, the one the player is working towards. Lines the map cannot draw (no stations, or fewer than two map
    /// nodes) are skipped without ending the reveal.
    /// </remarks>
    public static class MapReveal
    {
        /// <param name="lineCount">Lines in the network.</param>
        /// <param name="drawable">Whether a line has something to draw. Null counts every line.</param>
        /// <param name="unlocked">Whether a line is open. Null opens everything, which reveals every drawable line.</param>
        public static List<int> Revealed(int lineCount, Func<int, bool> drawable, Func<int, bool> unlocked)
        {
            var revealed = new List<int>();
            for (var line = 0; line < lineCount; line++)
            {
                if (drawable != null && !drawable(line)) continue;
                revealed.Add(line);
                if (unlocked != null && !unlocked(line)) break;
            }

            return revealed;
        }
    }
}
