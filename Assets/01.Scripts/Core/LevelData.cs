using System;
using System.Collections.Generic;

namespace TrainSudoku.Core
{
    public readonly struct FixedPiece : IEquatable<FixedPiece>
    {
        public int X { get; }
        public int Y { get; }
        public PieceKey Key { get; }

        public FixedPiece(int x, int y, PieceKey key)
        {
            X = x;
            Y = y;
            Key = key;
        }

        public bool Equals(FixedPiece other) => X == other.X && Y == other.Y && Key == other.Key;
        public override bool Equals(object obj) => obj is FixedPiece other && Equals(other);
        public override int GetHashCode() => ((X * 397) ^ Y) * 397 ^ (int)Key;
        public override string ToString() => $"{Key}@({X},{Y})";
    }

    /// <summary>
    /// Plain description of a level: size, clues, fixed pieces and tunnels. The runtime ScriptableObject converts
    /// to and from this type; everything in Core works on it directly.
    /// </summary>
    public sealed class LevelData
    {
        public string Name { get; set; } = "";
        public int Width { get; }
        public int Height { get; }
        public int[] ColumnClues { get; }
        public int[] RowClues { get; }
        public List<FixedPiece> FixedPieces { get; } = new List<FixedPiece>();
        public Tunnel Entrance { get; set; }
        public Tunnel Exit { get; set; }

        public LevelData(int width, int height)
        {
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be at least 1.");
            if (height < 1) throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be at least 1.");
            Width = width;
            Height = height;
            ColumnClues = new int[width];
            RowClues = new int[height];
        }

        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        public bool TryGetFixedPiece(int x, int y, out FixedPiece piece)
        {
            foreach (var candidate in FixedPieces)
            {
                if (candidate.X == x && candidate.Y == y)
                {
                    piece = candidate;
                    return true;
                }
            }

            piece = default;
            return false;
        }

        public LevelData Clone()
        {
            var copy = new LevelData(Width, Height) { Name = Name, Entrance = Entrance, Exit = Exit };
            Array.Copy(ColumnClues, copy.ColumnClues, Width);
            Array.Copy(RowClues, copy.RowClues, Height);
            copy.FixedPieces.AddRange(FixedPieces);
            return copy;
        }

        public bool HasTunnel(int x, int y, Direction side) => IsEntrance(x, y, side) || IsExit(x, y, side);
        public bool IsEntrance(int x, int y, Direction side) => Matches(Entrance, x, y, side);
        public bool IsExit(int x, int y, Direction side) => Matches(Exit, x, y, side);

        private bool Matches(Tunnel tunnel, int x, int y, Direction side) =>
            tunnel.Side == side && tunnel.CellX(Width) == x && tunnel.CellY(Height) == y;

        /// <summary>Static consistency problems. An empty list means the level is well formed, not that it is solvable.</summary>
        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            var columnTotal = 0;
            for (var x = 0; x < Width; x++)
            {
                if (ColumnClues[x] < 0 || ColumnClues[x] > Height)
                    problems.Add($"Column {x} clue {ColumnClues[x]} is outside 0..{Height}.");
                columnTotal += ColumnClues[x];
            }

            var rowTotal = 0;
            for (var y = 0; y < Height; y++)
            {
                if (RowClues[y] < 0 || RowClues[y] > Width)
                    problems.Add($"Row {y} clue {RowClues[y]} is outside 0..{Width}.");
                rowTotal += RowClues[y];
            }

            if (columnTotal != rowTotal)
                problems.Add($"Column clues total {columnTotal} but row clues total {rowTotal}.");

            if (!Entrance.IsOnPerimeter(Width, Height)) problems.Add($"Entrance {Entrance} is not on the perimeter.");
            if (!Exit.IsOnPerimeter(Width, Height)) problems.Add($"Exit {Exit} is not on the perimeter.");
            if (Entrance == Exit) problems.Add("Entrance and exit occupy the same tunnel position.");

            var columnFixed = new int[Width];
            var rowFixed = new int[Height];
            var seen = new HashSet<(int, int)>();
            foreach (var piece in FixedPieces)
            {
                if (!InBounds(piece.X, piece.Y))
                {
                    problems.Add($"Fixed piece {piece} is outside the board.");
                    continue;
                }

                if (!seen.Add((piece.X, piece.Y)))
                {
                    problems.Add($"Cell ({piece.X},{piece.Y}) has more than one fixed piece.");
                    continue;
                }

                columnFixed[piece.X]++;
                rowFixed[piece.Y]++;

                var (a, b) = PieceKeys.Connections(piece.Key);
                CheckFixedConnection(piece, a, problems);
                CheckFixedConnection(piece, b, problems);
            }

            for (var x = 0; x < Width; x++)
                if (columnFixed[x] > ColumnClues[x])
                    problems.Add($"Column {x} has {columnFixed[x]} fixed pieces but its clue is {ColumnClues[x]}.");
            for (var y = 0; y < Height; y++)
                if (rowFixed[y] > RowClues[y])
                    problems.Add($"Row {y} has {rowFixed[y]} fixed pieces but its clue is {RowClues[y]}.");

            return problems;
        }

        private void CheckFixedConnection(FixedPiece piece, Direction direction, List<string> problems)
        {
            var nx = piece.X + direction.Dx();
            var ny = piece.Y + direction.Dy();
            if (!InBounds(nx, ny))
            {
                if (!HasTunnel(piece.X, piece.Y, direction))
                    problems.Add($"Fixed piece {piece} points {direction} off the board with no tunnel there.");
                return;
            }

            if (TryGetFixedPiece(nx, ny, out var neighbour) && !PieceKeys.Has(neighbour.Key, direction.Opposite()))
                problems.Add($"Fixed piece {piece} points at fixed piece {neighbour}, which does not point back.");
        }
    }
}
