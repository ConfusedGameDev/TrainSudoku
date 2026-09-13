using NUnit.Framework;
using TrainSudoku.Core;
using TrainSudoku.XR.Rules;

namespace TrainSudoku.XR.Tests
{
    /// <summary>XR-PRD 4.3: the cell a held piece is over, and the hover band.</summary>
    public class BoardPickTests
    {
        private static readonly (int Width, int Height)[] Sizes = { (6, 6), (7, 7), (8, 8), (6, 8), (8, 7) };

        [Test]
        public void EveryCellCentreAndCornerPicksItsCell()
        {
            foreach (var (width, height) in Sizes)
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var (cx, cz) = BoardLayout.CellCenter(x, y, width, height);
                    foreach (var dx in new[] { -0.49, 0, 0.49 })
                    foreach (var dz in new[] { -0.49, 0, 0.49 })
                        Assert.AreEqual((x, y), BoardPick.CellAt(cx + dx, cz + dz, width, height), $"{width}x{height} ({x},{y}) {dx},{dz}");
                }
        }

        [Test]
        public void OffTheGridPicksNothing()
        {
            Assert.IsNull(BoardPick.CellAt(3.01, 0, 6, 6));
            Assert.IsNull(BoardPick.CellAt(-3.01, 0, 6, 6));
            Assert.IsNull(BoardPick.CellAt(0, 3.01, 6, 6));
            Assert.IsNull(BoardPick.CellAt(0, -3.01, 6, 6));
            // The tunnel ring and the tray are off the grid.
            Assert.IsNull(BoardPick.CellAt(0, -BoardLayout.HalfDepth(6) + 0.2, 6, 6));
        }

        [Test]
        public void RowZeroIsTheFarRow()
        {
            Assert.AreEqual((0, 0), BoardPick.CellAt(-2.9, 2.9, 6, 6));
            Assert.AreEqual((5, 5), BoardPick.CellAt(2.9, -2.9, 6, 6));
        }

        [Test]
        public void OnlyInsideTheHoverBandIsAPieceOverACell()
        {
            const double band = 1.6;
            Assert.AreEqual((2, 3), BoardPick.HoverCell(-0.5, 0.0, -0.5, 6, 6, band));
            Assert.AreEqual((2, 3), BoardPick.HoverCell(-0.5, band, -0.5, 6, 6, band));
            Assert.IsNull(BoardPick.HoverCell(-0.5, band + 0.01, -0.5, 6, 6, band));
            Assert.AreEqual((2, 3), BoardPick.HoverCell(-0.5, -BoardPick.BelowTolerance, -0.5, 6, 6, band), "pushed into the board");
            Assert.IsNull(BoardPick.HoverCell(-0.5, -BoardPick.BelowTolerance - 0.01, -0.5, 6, 6, band));
            Assert.IsNull(BoardPick.HoverCell(4, 0.2, 0, 6, 6, band), "over the tunnel ring");
        }
    }
}
