using System;
using System.Text;

namespace TrainSudoku.Core
{
    /// <summary>
    /// The result card a player pastes into a chat, in the shape Wordle made legible: a header naming the run and a
    /// grid of coloured squares under it. Plain text, built here in Core so it can be tested without Unity.
    /// </summary>
    /// <remarks>
    /// <b>The grid shows the puzzle, never the solution.</b> A coloured square is a cell the level handed the
    /// player pre-laid (<see cref="LevelData.FixedPieces"/>) — green in the body of the board, blue at the two
    /// tunnel mouths, so a reader can see which way the train ran; every other cell is black whether or not the
    /// finished route passes through it. So the card says how hard the board was and how the run went, and a
    /// reader who opens the same station still has the whole deduction in front of them. Anything that drew the
    /// player's own track would give the answer away to everyone in the thread, which is the one thing this must
    /// not do — and the test suite holds it to that by solving a board and checking the card does not move.
    ///
    /// <b>It carries no translated copy, in any locale.</b> Station and line names are untranslated proper nouns
    /// (D14), the masthead is the product's name, and the rest is digits and symbols — so one build's card is every
    /// build's card, there is nothing here for the `UI` String Table to hold, and the text never reaches a
    /// <see cref="UnityEngine.UIElements.VisualElement"/>, which is what keeps its emoji clear of D13 and of the
    /// baked `ja` atlas. <b>Do not render this string in the UI</b> — not as a preview, not as a confirmation — or
    /// every square in it becomes a missing-glyph box in the Japanese build.
    ///
    /// The squares are written as escapes rather than as literal emoji so the file's encoding cannot quietly
    /// mangle them: several are outside the BMP and survive a careless re-save as a pair of replacement characters.
    /// </remarks>
    public static class ShareCard
    {
        /// <summary>The product's name, and a proper noun like the station names beside it — never a table lookup.</summary>
        public const string Masthead = "TSUGI";

        /// <summary>A cell the level gave the player: pre-laid, fixed, unerasable.</summary>
        public const string FixedSquare = "\U0001F7E9";

        /// <summary>
        /// The same, at a tunnel mouth — the cell the train enters or leaves by. A second colour rather than a
        /// second row of squares: the two ends are already in the grid (the baker fills every tunnel cell with a
        /// fixed piece), so marking them costs the card no width at all.
        /// </summary>
        public const string TunnelSquare = "\U0001F7E6";

        /// <summary>Every other cell, solved or not.</summary>
        public const string EmptySquare = "⬛";

        /// <summary>
        /// The dead corner where the two clue runs meet. A station sign rather than another square, because a square
        /// there — white or black — reads as a seventh column and throws the whole grid out by one.
        /// </summary>
        public const string Corner = "\U0001F689";

        public const string StarEarned = "★";
        public const string StarMissed = "☆";

        /// <summary>The interpunct between header facts, the way a departure board separates them.</summary>
        private const string Separator = " · ";

        /// <summary>How many stars a run can earn, and so how wide the star row always is.</summary>
        private const int MaxStars = 3;

        /// <summary>
        /// The whole card: header, blank line, grid. Every argument is optional in the sense that a missing one
        /// drops its segment rather than printing "null" — a half-built level asset is a bad card, not a crash.
        /// </summary>
        /// <param name="level">The board. Null yields the header alone.</param>
        /// <param name="stationName">The station, an untranslated proper noun.</param>
        /// <param name="lineName">The line the station sits on, likewise.</param>
        /// <param name="lineCode">The line's two-letter roundel code, e.g. <c>TS</c>.</param>
        /// <param name="seconds">The finished run, formatted by <see cref="ProgressTracker.FormatTime"/>.</param>
        /// <param name="stars">Stars earned, clamped to 0..3.</param>
        public static string Build(LevelData level, string stationName, string lineName, string lineCode,
            double seconds, int stars)
        {
            var card = new StringBuilder();
            card.Append(Header(stationName, lineName, lineCode));
            card.Append('\n');
            card.Append(ProgressTracker.FormatTime(seconds));
            card.Append(' ');
            card.Append(Stars(stars));

            if (level != null)
            {
                card.Append("\n\n");
                card.Append(Grid(level));
            }

            return card.ToString();
        }

        /// <summary>
        /// The naming line: masthead, station, then the line and its code. Empty names are skipped, so a level with
        /// no line assigned reads <c>TSUGI · Ashgate</c> rather than trailing an empty bracket.
        /// </summary>
        public static string Header(string stationName, string lineName, string lineCode)
        {
            var header = new StringBuilder(Masthead);
            if (!string.IsNullOrWhiteSpace(stationName)) header.Append(Separator).Append(stationName.Trim());

            var hasName = !string.IsNullOrWhiteSpace(lineName);
            var hasCode = !string.IsNullOrWhiteSpace(lineCode);
            if (hasName || hasCode)
            {
                header.Append(Separator);
                if (hasName) header.Append(lineName.Trim());
                if (hasName && hasCode) header.Append(' ');
                if (hasCode) header.Append('(').Append(lineCode.Trim()).Append(')');
            }

            return header.ToString();
        }

        /// <summary>Three stars, filled up to <paramref name="earned"/>, so the row is the same width every time.</summary>
        public static string Stars(int earned)
        {
            var row = new StringBuilder(MaxStars);
            for (var i = 0; i < MaxStars; i++) row.Append(i < earned ? StarEarned : StarMissed);
            return row.ToString();
        }

        /// <summary>
        /// The board: the column clues along the top, each row's clue down the left, and one square per cell. Row 0
        /// is written first, which is the far row and the one drawn at the top of the screen, so the grid in the
        /// message is oriented the way the player saw it.
        /// </summary>
        public static string Grid(LevelData level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));

            var grid = new StringBuilder();
            grid.Append(Corner);
            for (var x = 0; x < level.Width; x++) grid.Append(Keycap(level.ColumnClues[x]));

            var entranceX = level.Entrance.CellX(level.Width);
            var entranceY = level.Entrance.CellY(level.Height);
            var exitX = level.Exit.CellX(level.Width);
            var exitY = level.Exit.CellY(level.Height);

            for (var y = 0; y < level.Height; y++)
            {
                grid.Append('\n');
                grid.Append(Keycap(level.RowClues[y]));
                for (var x = 0; x < level.Width; x++)
                {
                    // Blue only where a tunnel cell actually carries a piece. A tunnel mouth left empty by a
                    // hand-authored level is drawn black like any other empty cell, because a coloured square has
                    // one meaning here — the level gave you this — and a blue one on an empty cell would claim a
                    // given that is not there. Every shipped level is baked, so in practice both ends are blue.
                    if (!level.TryGetFixedPiece(x, y, out _)) grid.Append(EmptySquare);
                    else if ((x == entranceX && y == entranceY) || (x == exitX && y == exitY)) grid.Append(TunnelSquare);
                    else grid.Append(FixedSquare);
                }
            }

            return grid.ToString();
        }

        /// <summary>
        /// A clue as a keycap emoji, which is the one numeral form that is emoji-width — so the digits line up with
        /// the squares under them in Messages, WhatsApp, Discord and Slack, where this text actually gets pasted.
        /// </summary>
        /// <remarks>
        /// Keycaps only exist for 0-9. The board is capped at eight cells wide (D18) so a clue cannot reach ten, but
        /// a two-digit one falls back to plain digits rather than to a missing glyph: a card that is a column out of
        /// true still reads, and one full of boxes does not.
        /// </remarks>
        public static string Keycap(int clue)
        {
            if (clue < 0 || clue > 9) return clue.ToString();
            return $"{(char)('0' + clue)}️⃣";
        }
    }
}
