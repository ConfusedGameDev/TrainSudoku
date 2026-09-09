using System;

namespace TrainSudoku.Core
{
    /// <summary>
    /// World-space layout of a board (PRD section 7). One cell is one unit. The board is centred on the origin on the
    /// ground plane: X grows east, Z grows north, so row 0 is the row furthest from the camera. Y is up.
    /// </summary>
    public static class BoardLayout
    {
        public const double CellSize = 1.0;

        /// <summary>Distance from a perimeter cell centre to the tunnel centre outside it.</summary>
        public const double TunnelOffset = 1.0;

        /// <summary>Distance from a perimeter cell centre to the clue label beyond the tunnel ring.</summary>
        public const double ClueOffset = 1.85;

        /// <summary>Extra half-extent past the board edge that the camera must keep in view: tunnels and clues.</summary>
        public const double Padding = 1.9;

        public static (double X, double Z) CellCenter(int x, int y, int width, int height) =>
            (x - (width - 1) / 2.0, (height - 1) / 2.0 - y);

        /// <summary>Unit step on the ground plane towards a side.</summary>
        public static (double X, double Z) Step(Direction side)
        {
            switch (side)
            {
                case Direction.North: return (0, 1);
                case Direction.East: return (1, 0);
                case Direction.South: return (0, -1);
                case Direction.West: return (-1, 0);
                default: throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown direction.");
            }
        }

        /// <summary>Rotation about Y, in degrees, that turns +Z to face the given side.</summary>
        public static double Yaw(Direction side) => 90.0 * (int)side;

        public static (double X, double Z) TunnelCenter(Tunnel tunnel, int width, int height) =>
            Offset(CellCenter(tunnel.CellX(width), tunnel.CellY(height), width, height), tunnel.Side, TunnelOffset);

        /// <summary>Column clues sit north of the board, beyond the tunnel ring.</summary>
        public static (double X, double Z) ColumnClueAnchor(int x, int width, int height) =>
            Offset(CellCenter(x, 0, width, height), Direction.North, ClueOffset);

        /// <summary>Row clues sit east of the board, beyond the tunnel ring.</summary>
        public static (double X, double Z) RowClueAnchor(int y, int width, int height) =>
            Offset(CellCenter(width - 1, y, width, height), Direction.East, ClueOffset);

        /// <summary>Half of the width the camera must show, centred on the origin.</summary>
        public static double HalfWidth(int width) => width * CellSize / 2.0 + Padding;

        /// <summary>Half of the depth the camera must show, centred on the origin.</summary>
        public static double HalfDepth(int height) => height * CellSize / 2.0 + Padding;

        private static (double X, double Z) Offset((double X, double Z) origin, Direction side, double distance)
        {
            var (dx, dz) = Step(side);
            return (origin.X + dx * distance, origin.Z + dz * distance);
        }
    }
}
