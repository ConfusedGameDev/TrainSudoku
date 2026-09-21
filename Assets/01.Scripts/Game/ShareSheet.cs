using System.Runtime.InteropServices;
using TrainSudoku.Core;
using UnityEngine;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Sends a <see cref="ShareCard"/> out of the game: the system share sheet on an iOS device, the clipboard
    /// everywhere else. The caller is told which happened, because the two need different things said to the
    /// player — a sheet says nothing (it is on screen), a copy has to.
    /// </summary>
    /// <remarks>
    /// The native half is <c>Assets/00.Plugins/iOS/TrainSudokuShare.mm</c>, which is iOS-only in its importer, so
    /// everything here compiles to the clipboard branch off device. That branch is not a stub: it is the real
    /// behaviour in the Editor and on Android and desktop, which is where most of this will be tested.
    /// </remarks>
    public static class ShareSheet
    {
        /// <summary>How the text actually left, which is what decides whether the player needs telling.</summary>
        public enum Delivery
        {
            /// <summary>The system sheet is up; the player can see it and needs no confirmation from us.</summary>
            Sheet,

            /// <summary>The text is on the clipboard. Nothing is on screen, so the caller must say so.</summary>
            Clipboard,

            /// <summary>Nothing was sent — there was nothing to send.</summary>
            None,
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void _TSShareText(string text);
#endif

        public static Delivery Share(string text)
        {
            if (string.IsNullOrEmpty(text)) return Delivery.None;

#if UNITY_IOS && !UNITY_EDITOR
            _TSShareText(text);
            return Delivery.Sheet;
#else
            GUIUtility.systemCopyBuffer = text;
            return Delivery.Clipboard;
#endif
        }

        /// <summary>
        /// Builds the card for a finished run and sends it. The pieces come from three different places — the board
        /// from the level asset, the names from the level and its line, the time and stars from the run — so
        /// assembling them is this layer's job and <see cref="ShareCard"/> stays a pure formatter.
        /// </summary>
        /// <remarks>
        /// <b>The run's own stars, not the stored best.</b> <see cref="ProgressTracker"/> keeps time and stars
        /// independently and a slower run never lowers either, so the stored pair can come from two different runs;
        /// the card is a report of one run, so it takes both from <see cref="CompletionResult"/>. This is the same
        /// reasoning OM1 applies to a leaderboard submission, for the same reason.
        /// </remarks>
        public static Delivery ShareResult(LevelDefinition level, LineDefinition line, CompletionResult result)
        {
            if (level == null) return Delivery.None;

            return Share(ShareCard.Build(
                level.ToLevelData(),
                level.DisplayName,
                line != null ? line.DisplayName : null,
                line != null ? line.Code : null,
                result.Time,
                result.Stars));
        }
    }
}
