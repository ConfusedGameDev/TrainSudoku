using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    /// <summary>
    /// The shareable result card. The rule worth a test is the one a bug would quietly break: the grid must show
    /// the level's pre-laid track and nothing else, so a card pasted into a chat cannot give the puzzle away.
    /// </summary>
    public class ShareCardTests
    {
        /// <summary>
        /// Ashgate (`simple.asset`), the tutorial station, transcribed from the asset: a 6x6 with column clues
        /// 3,4,3,3,1,2 and row clues 1,1,2,3,4,5 — both totalling 16 — and four fixed pieces, two of which are the
        /// tunnel cells the baker fills.
        /// </summary>
        private static LevelData Ashgate()
        {
            var level = new LevelData(6, 6) { Name = "Ashgate" };
            int[] columns = { 3, 4, 3, 3, 1, 2 };
            int[] rows = { 1, 1, 2, 3, 4, 5 };
            for (var i = 0; i < 6; i++)
            {
                level.ColumnClues[i] = columns[i];
                level.RowClues[i] = rows[i];
            }

            level.FixedPieces.Add(new FixedPiece(0, 0, PieceKey.SW));
            level.FixedPieces.Add(new FixedPiece(1, 3, PieceKey.NE));
            level.FixedPieces.Add(new FixedPiece(3, 4, PieceKey.NW));
            level.FixedPieces.Add(new FixedPiece(5, 4, PieceKey.SE));
            level.Entrance = new Tunnel(Direction.West, 0);
            level.Exit = new Tunnel(Direction.East, 4);
            return level;
        }

        [Test]
        public void GridMarksTheFixedPiecesAndNothingElse()
        {
            var grid = ShareCard.Grid(Ashgate()).Split('\n');

            Assert.AreEqual(7, grid.Length, "One clue row plus six board rows.");
            Assert.AreEqual(
                ShareCard.Corner + ShareCard.Keycap(3) + ShareCard.Keycap(4) + ShareCard.Keycap(3) +
                ShareCard.Keycap(3) + ShareCard.Keycap(1) + ShareCard.Keycap(2),
                grid[0], "The top line is the corner then the column clues, left to right.");

            // Row 0 is the far row and is written first, so the grid reads the way the player saw the board.
            // (0,0) is the entrance and (5,4) the exit, so those two givens are blue rather than green.
            Assert.AreEqual(ShareCard.Keycap(1) + ShareCard.TunnelSquare + Empty(5), grid[1]);
            Assert.AreEqual(ShareCard.Keycap(1) + Empty(6), grid[2]);
            Assert.AreEqual(ShareCard.Keycap(2) + Empty(6), grid[3]);
            Assert.AreEqual(ShareCard.Keycap(3) + Empty(1) + ShareCard.FixedSquare + Empty(4), grid[4]);
            Assert.AreEqual(
                ShareCard.Keycap(4) + Empty(3) + ShareCard.FixedSquare + Empty(1) + ShareCard.TunnelSquare,
                grid[5]);
            Assert.AreEqual(ShareCard.Keycap(5) + Empty(6), grid[6]);
        }

        /// <summary>
        /// The whole point of the card: it may not encode the solution. A solved board and an untouched one must
        /// produce the same grid, because only the level's own fixed pieces are ever drawn.
        /// </summary>
        [Test]
        public void SolvingTheBoardDoesNotChangeTheCard()
        {
            var level = Ashgate();
            var before = ShareCard.Grid(level);

            // Fill every empty cell. A real solution is not needed — the claim is that play cannot reach the card.
            var board = new Board(level);
            for (var y = 0; y < level.Height; y++)
                for (var x = 0; x < level.Width; x++)
                    if (board.IsEmpty(x, y))
                        board.SetUnchecked(x, y, new Piece(PieceKey.EW, false));

            Assert.AreEqual(before, ShareCard.Grid(level));
            Assert.AreEqual(2, CountOf(before, ShareCard.FixedSquare), "Two givens in the body of the board.");
            Assert.AreEqual(2, CountOf(before, ShareCard.TunnelSquare), "The two tunnel mouths, and only those.");
        }

        /// <summary>
        /// Blue means a tunnel mouth the level pre-laid. A tunnel cell left empty stays black, because a coloured
        /// square has one meaning on this card — the level gave you this — and colouring an empty one would claim
        /// a given that is not there. Every shipped level is baked, so in practice both ends are always blue.
        /// </summary>
        [Test]
        public void AnUnbakedTunnelCellIsNotMarked()
        {
            var level = Ashgate();
            level.FixedPieces.RemoveAll(p => p.X == 0 && p.Y == 0);

            var card = ShareCard.Grid(level);
            Assert.AreEqual(ShareCard.Keycap(1) + Empty(6), card.Split('\n')[1], "The empty entrance cell is black.");
            Assert.AreEqual(1, CountOf(card, ShareCard.TunnelSquare), "Only the exit is still marked.");
        }

        [Test]
        public void GridIsRectangularOnANonSquareBoard()
        {
            var level = new LevelData(8, 5);
            var grid = ShareCard.Grid(level).Split('\n');

            Assert.AreEqual(6, grid.Length, "One clue row plus five board rows.");
            foreach (var row in grid)
                Assert.AreEqual(9, CountCells(row), "Every line is one clue cell plus eight board cells.");
        }

        [Test]
        public void HeaderNamesTheStationAndItsLine()
        {
            Assert.AreEqual("TSUGI · Ashgate · Thornemoor (TS)",
                ShareCard.Header("Ashgate", "Thornemoor", "TS"));
        }

        /// <summary>A half-built asset should shorten the header, not print a null or an empty bracket.</summary>
        [Test]
        public void HeaderDropsMissingNames()
        {
            Assert.AreEqual("TSUGI · Ashgate", ShareCard.Header("Ashgate", null, null));
            Assert.AreEqual("TSUGI · Ashgate · (TS)", ShareCard.Header("Ashgate", "  ", "TS"));
            Assert.AreEqual("TSUGI", ShareCard.Header(null, null, null));
        }

        [Test]
        public void StarsAreAlwaysThreeWide()
        {
            Assert.AreEqual(ShareCard.StarMissed + ShareCard.StarMissed + ShareCard.StarMissed, ShareCard.Stars(0));
            Assert.AreEqual(ShareCard.StarEarned + ShareCard.StarMissed + ShareCard.StarMissed, ShareCard.Stars(1));
            Assert.AreEqual(ShareCard.StarEarned + ShareCard.StarEarned + ShareCard.StarEarned, ShareCard.Stars(3));
            Assert.AreEqual(ShareCard.Stars(3), ShareCard.Stars(9), "More than three stars cannot widen the row.");
        }

        /// <summary>
        /// Keycaps only exist for 0-9. A clue cannot reach ten on a board capped at eight wide, but the fallback is
        /// what keeps a card that is a column out of true from becoming a card full of missing-glyph boxes.
        /// </summary>
        [Test]
        public void KeycapFallsBackToPlainDigitsOutsideItsRange()
        {
            Assert.AreEqual("0️⃣", ShareCard.Keycap(0));
            Assert.AreEqual("8️⃣", ShareCard.Keycap(8));
            Assert.AreEqual("12", ShareCard.Keycap(12));
        }

        [Test]
        public void BuildPutsTheHeaderTimeAndStarsAboveTheGrid()
        {
            var card = ShareCard.Build(Ashgate(), "Ashgate", "Thornemoor", "TS", 222, 2);
            var lines = card.Split('\n');

            Assert.AreEqual("TSUGI · Ashgate · Thornemoor (TS)", lines[0]);
            Assert.AreEqual("03:42 " + ShareCard.Stars(2), lines[1]);
            Assert.AreEqual("", lines[2], "A blank line separates the header from the grid, as Wordle's does.");
            Assert.AreEqual(ShareCard.Grid(Ashgate()), string.Join("\n", lines, 3, lines.Length - 3));
        }

        /// <summary>A level that failed to load should still produce a readable card rather than throwing.</summary>
        [Test]
        public void BuildWithoutALevelIsTheHeaderAlone()
        {
            var card = ShareCard.Build(null, "Ashgate", "Thornemoor", "TS", 222, 2);
            Assert.AreEqual("TSUGI · Ashgate · Thornemoor (TS)\n03:42 " + ShareCard.Stars(2), card);
        }

        private static string Empty(int count) => Repeat(ShareCard.EmptySquare, count);

        private static string Repeat(string unit, int count)
        {
            var text = "";
            for (var i = 0; i < count; i++) text += unit;
            return text;
        }

        private static int CountOf(string text, string unit)
        {
            var count = 0;
            for (var i = text.IndexOf(unit, System.StringComparison.Ordinal); i >= 0;
                 i = text.IndexOf(unit, i + unit.Length, System.StringComparison.Ordinal))
                count++;
            return count;
        }

        /// <summary>
        /// Counts rendered cells in a grid line. Every unit in the card is one or more UTF-16 code units, so a
        /// plain <c>Length</c> would count a square as two and a keycap as three; this counts what a reader sees.
        /// </summary>
        private static int CountCells(string line)
        {
            var cells = 0;
            var i = 0;
            while (i < line.Length)
            {
                i += char.IsSurrogatePair(line, i) ? 2 : 1;
                // A keycap is a digit followed by the variation selector and the combining enclosure.
                while (i < line.Length && (line[i] == '️' || line[i] == '⃣')) i++;
                cells++;
            }

            return cells;
        }
    }
}
