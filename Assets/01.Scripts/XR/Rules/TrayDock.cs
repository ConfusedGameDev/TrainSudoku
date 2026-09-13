using System;
using TrainSudoku.Core;

namespace TrainSudoku.XR.Rules
{
    /// <summary>
    /// Where the tray sits (XR-PRD 4.1): a 2x3 block of slots beside the board edge nearest the player, on their
    /// dominant-hand side, moving to another edge once the player has stood nearer to it for
    /// <see cref="DwellSeconds"/>, and never while a piece is held. Everything is in <see cref="BoardLayout"/>'s frame
    /// (one unit per cell, board centred on the origin, X east, Z north), so the display only scales it.
    /// </summary>
    /// <remarks>
    /// The block stands just outside the platform, off the dominant-hand side of the docked edge, its near row level
    /// with that edge. That keeps the whole near edge free for the handle bar and puts the pieces where the hand rests.
    /// It stands <see cref="Gap"/> clear of the tunnel ring, which also clears the clue signs beyond it (north and east).
    /// </remarks>
    public sealed class TrayDock
    {
        public const int Columns = 2;
        public const int Rows = 3;
        public const int SlotCount = Columns * Rows;

        /// <summary>Centre to centre between slots, in cells: generous, because a 6 cm piece is a small thing to pinch.</summary>
        public const double Pitch = 1.3;

        /// <summary>From the platform's side (grid plus tunnel ring) to the block's inner edge, in cells.</summary>
        public const double Gap = 1.0;

        public const double DefaultDwellSeconds = 1.5;

        /// <summary>
        /// The tray stands clear of a board this many cells across at the least — the largest shipped — so on the edge
        /// the board was placed from it keeps its place from level to level whatever the board's size (X17).
        /// </summary>
        public const int SteadyCells = 8;

        /// <summary>
        /// The slots as the player at the docked edge sees them: rows from near to far, columns from left to right. From
        /// the south edge every piece sits where it points — the south curves nearest, the west ones on the left.
        /// </summary>
        private static readonly PieceKey[,] Layout =
        {
            { PieceKey.SW, PieceKey.SE },
            { PieceKey.NS, PieceKey.EW },
            { PieceKey.NW, PieceKey.NE },
        };

        private Direction _candidate;
        private double _candidateFor;

        /// <summary>The board edge the tray is docked at. South is the edge the board was placed from.</summary>
        public Direction Edge { get; private set; }

        public Hand DominantHand { get; set; }
        public double DwellSeconds { get; }

        public TrayDock(Hand dominantHand = Hand.Right, Direction edge = Direction.South, double dwellSeconds = DefaultDwellSeconds)
        {
            DominantHand = dominantHand;
            Edge = edge;
            DwellSeconds = dwellSeconds;
            _candidate = edge;
        }

        /// <summary>
        /// Follows the player round the board. <paramref name="viewerX"/> and <paramref name="viewerZ"/> are where they
        /// stand, in the board's frame. Returns true on the update that moves the tray to another edge.
        /// </summary>
        public bool Update(double viewerX, double viewerZ, int width, int height, double deltaSeconds, bool holding)
        {
            var nearest = NearestEdge(viewerX, viewerZ, width, height);
            if (holding || nearest == Edge)
            {
                _candidateFor = 0;
                _candidate = Edge;
                return false;
            }

            if (nearest != _candidate)
            {
                _candidate = nearest;
                _candidateFor = 0;
            }

            _candidateFor += Math.Max(0, deltaSeconds);
            if (_candidateFor < DwellSeconds) return false;
            Edge = nearest;
            _candidateFor = 0;
            return true;
        }

        /// <summary>Puts the tray at an edge straight away, as when a new board is laid out.</summary>
        public void DockAt(Direction edge)
        {
            Edge = edge;
            _candidate = edge;
            _candidateFor = 0;
        }

        /// <summary>
        /// The platform edge (grid plus tunnel ring) a point in the board's frame stands by: the one it is furthest out
        /// past. Inside the platform that is the nearest edge; beyond a corner, where the corner is equally near to both
        /// edges, it is the edge the player faces most squarely.
        /// </summary>
        public static Direction NearestEdge(double x, double z, int width, int height)
        {
            var hw = BoardLayout.HalfWidth(width);
            var hd = BoardLayout.HalfDepth(height);
            var best = Direction.South;
            var bestOutward = double.MinValue;
            foreach (var edge in DirectionExtensions.All)
            {
                var (nx, nz) = BoardLayout.Step(edge);
                var outward = x * nx + z * nz - (nx != 0 ? hw : hd);
                if (outward > bestOutward)
                {
                    bestOutward = outward;
                    best = edge;
                }
            }

            return best;
        }

        public (double X, double Z) SlotCentre(int slot, int width, int height) => SlotCentre(slot, Edge, DominantHand, width, height);

        /// <summary>A slot's centre in the board's frame, for the tray docked at <paramref name="edge"/>.</summary>
        public static (double X, double Z) SlotCentre(int slot, Direction edge, Hand dominant, int width, int height)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot), slot, "A tray has six slots.");
            var row = slot / Columns;
            var column = slot % Columns;

            // Out of the board towards the player, and the player's right as they face the board from that edge.
            var (nx, nz) = BoardLayout.Step(edge);
            var (rx, rz) = (-nz, nx);
            var (sx, sz) = dominant == Hand.Right ? (rx, rz) : (-rx, -rz);

            var hw = BoardLayout.HalfWidth(width);
            var hd = BoardLayout.HalfDepth(height);
            var halfSide = Math.Max(sx != 0 ? hw : hd, BoardLayout.HalfWidth(SteadyCells));
            var halfOut = nx != 0 ? hw : hd;

            // Columns count left to right as the player sees them, so on the left-hand side the outer column comes first.
            var fromInner = dominant == Hand.Right ? column : Columns - 1 - column;
            var across = halfSide + Gap + Pitch * (fromInner + 0.5);
            var along = halfOut - Pitch * (row + 0.5);
            return (sx * across + nx * along, sz * across + nz * along);
        }

        public static PieceKey KeyAt(int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot), slot, "A tray has six slots.");
            return Layout[slot / Columns, slot % Columns];
        }

        public static int SlotOf(PieceKey key)
        {
            for (var slot = 0; slot < SlotCount; slot++)
                if (KeyAt(slot) == key) return slot;
            throw new ArgumentOutOfRangeException(nameof(key), key, "Not a track key.");
        }
    }
}
