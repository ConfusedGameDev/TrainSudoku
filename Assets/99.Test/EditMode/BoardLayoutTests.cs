using NUnit.Framework;
using TrainSudoku.Core;

namespace TrainSudoku.Tests
{
    public class BoardLayoutTests
    {
        private const double Eps = 1e-9;

        [Test]
        public void CellsAreCentredOnTheOriginWithRowZeroToTheNorth()
        {
            var (x00, z00) = BoardLayout.CellCenter(0, 0, 6, 6);
            Assert.AreEqual(-2.5, x00, Eps);
            Assert.AreEqual(2.5, z00, Eps);

            var (x55, z55) = BoardLayout.CellCenter(5, 5, 6, 6);
            Assert.AreEqual(2.5, x55, Eps);
            Assert.AreEqual(-2.5, z55, Eps);

            var (x, z) = BoardLayout.CellCenter(1, 2, 3, 5);
            Assert.AreEqual(0, x, Eps, "The middle column of an odd board is on the axis");
            Assert.AreEqual(0, z, Eps, "The middle row of an odd board is on the axis");
        }

        [Test]
        public void NeighbouringCellsAreOneUnitApart()
        {
            var (ax, az) = BoardLayout.CellCenter(2, 3, 6, 6);
            var (bx, bz) = BoardLayout.CellCenter(3, 3, 6, 6);
            var (cx, cz) = BoardLayout.CellCenter(2, 4, 6, 6);
            Assert.AreEqual(BoardLayout.CellSize, bx - ax, Eps);
            Assert.AreEqual(0, bz - az, Eps);
            Assert.AreEqual(0, cx - ax, Eps);
            Assert.AreEqual(-BoardLayout.CellSize, cz - az, Eps, "Row y+1 is one unit south");
        }

        [Test]
        public void StepsMatchTheDirectionEnum()
        {
            Assert.AreEqual((0.0, 1.0), BoardLayout.Step(Direction.North));
            Assert.AreEqual((1.0, 0.0), BoardLayout.Step(Direction.East));
            Assert.AreEqual((0.0, -1.0), BoardLayout.Step(Direction.South));
            Assert.AreEqual((-1.0, 0.0), BoardLayout.Step(Direction.West));
            Assert.AreEqual(0, BoardLayout.Yaw(Direction.North));
            Assert.AreEqual(90, BoardLayout.Yaw(Direction.East));
            Assert.AreEqual(180, BoardLayout.Yaw(Direction.South));
            Assert.AreEqual(270, BoardLayout.Yaw(Direction.West));
        }

        [Test]
        public void TunnelsSitOneCellOutsideTheirPerimeterCell()
        {
            var (wx, wz) = BoardLayout.TunnelCenter(new Tunnel(Direction.West, 3), 6, 6);
            var (cx, cz) = BoardLayout.CellCenter(0, 3, 6, 6);
            Assert.AreEqual(cx - 1, wx, Eps);
            Assert.AreEqual(cz, wz, Eps);

            var (ex, ez) = BoardLayout.TunnelCenter(new Tunnel(Direction.East, 2), 6, 6);
            Assert.AreEqual(3.5, ex, Eps);
            Assert.AreEqual(0.5, ez, Eps);

            var (nx, nz) = BoardLayout.TunnelCenter(new Tunnel(Direction.North, 0), 4, 3);
            Assert.AreEqual(-1.5, nx, Eps);
            Assert.AreEqual(2, nz, Eps);

            var (sx, sz) = BoardLayout.TunnelCenter(new Tunnel(Direction.South, 3), 4, 3);
            Assert.AreEqual(1.5, sx, Eps);
            Assert.AreEqual(-2, sz, Eps);
        }

        [Test]
        public void CluesSitBeyondTheTunnelRing()
        {
            var (cx, cz) = BoardLayout.ColumnClueAnchor(2, 6, 6);
            Assert.AreEqual(-0.5, cx, Eps);
            Assert.Greater(cz, 2.5 + BoardLayout.TunnelOffset + 0.25, "Clear of a tunnel box half a cell deep");

            var (rx, rz) = BoardLayout.RowClueAnchor(4, 6, 6);
            Assert.AreEqual(-1.5, rz, Eps);
            Assert.Greater(rx, 2.5 + BoardLayout.TunnelOffset + 0.25);

            Assert.LessOrEqual(cz, BoardLayout.HalfDepth(6), "Clues stay inside the camera extents");
            Assert.LessOrEqual(rx, BoardLayout.HalfWidth(6));
        }

        [Test]
        public void ExtentsGrowWithTheBoard()
        {
            Assert.AreEqual(BoardLayout.HalfWidth(8) - BoardLayout.HalfWidth(6), 1, Eps);
            Assert.AreEqual(BoardLayout.HalfDepth(3), 1.5 + BoardLayout.Padding, Eps);
        }
    }
}
