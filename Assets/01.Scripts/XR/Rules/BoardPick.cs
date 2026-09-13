using System;
using TrainSudoku.Core;

namespace TrainSudoku.XR.Rules
{
    /// <summary>
    /// Which cell a held piece is over (XR-PRD 4.3): the inverse of <see cref="BoardLayout.CellCenter"/>, in the same
    /// frame (one unit per cell, board centred on the origin, X east, Z north, Y up from the top of the slabs).
    /// </summary>
    public static class BoardPick
    {
        /// <summary>How far below the top of the slabs a piece still counts as over its cell: a hand pushed into the board.</summary>
        public const double BelowTolerance = 0.5;

        /// <summary>The cell whose square contains the point, or null off the grid.</summary>
        public static (int X, int Y)? CellAt(double x, double z, int width, int height)
        {
            var cellX = (int)Math.Floor(x / BoardLayout.CellSize + width / 2.0);
            var cellY = (int)Math.Floor(height / 2.0 - z / BoardLayout.CellSize);
            if (cellX < 0 || cellX >= width || cellY < 0 || cellY >= height) return null;
            return (cellX, cellY);
        }

        /// <summary>
        /// The cell a piece centred on the point hovers over: inside the hover band above the board (<paramref name="band"/>
        /// cells high) and over the grid. Null outside the band or off the grid: no ghost, and a release there is off the
        /// platform.
        /// </summary>
        public static (int X, int Y)? HoverCell(double x, double y, double z, int width, int height, double band)
        {
            if (y > band || y < -BelowTolerance) return null;
            return CellAt(x, z, width, height);
        }
    }
}
