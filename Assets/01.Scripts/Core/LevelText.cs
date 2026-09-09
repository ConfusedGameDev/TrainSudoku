using System;
using System.Collections.Generic;
using System.Text;

namespace TrainSudoku.Core
{
    public sealed class LevelTextException : Exception
    {
        /// <summary>1-based line in the source text, or 0 when the problem is not tied to a line.</summary>
        public int Line { get; }

        public LevelTextException(int line, string message) : base(line > 0 ? $"Line {line}: {message}" : message)
        {
            Line = line;
        }
    }

    /// <summary>The authoring text format, PRD section 9.2. The runtime never parses text; the editor does.</summary>
    public static class LevelText
    {
        private const string NamePrefix = "name:";

        public static LevelData Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            var name = "";
            var body = new List<(int Line, string[] Tokens)>();
            var rawLines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (var i = 0; i < rawLines.Length; i++)
            {
                var line = rawLines[i].Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith(NamePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    name = line.Substring(NamePrefix.Length).Trim();
                    continue;
                }

                body.Add((i + 1, line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)));
            }

            if (body.Count == 0) throw new LevelTextException(0, "The level text is empty.");

            var cursor = 0;
            (int Line, string[] Tokens)? topEdge = null;
            if (!ContainsInteger(body[0].Tokens))
            {
                topEdge = body[0];
                cursor++;
            }

            if (cursor >= body.Count) throw new LevelTextException(body[body.Count - 1].Line, "Missing the column clue line.");

            var clueLine = body[cursor++];
            var columnClues = new int[clueLine.Tokens.Length];
            for (var x = 0; x < columnClues.Length; x++)
            {
                if (!int.TryParse(clueLine.Tokens[x], out columnClues[x]))
                    throw new LevelTextException(clueLine.Line, $"Column clue line must contain only integers, found '{clueLine.Tokens[x]}'.");
            }

            var width = columnClues.Length;

            var rows = new List<(int Line, string[] Tokens)>();
            while (cursor < body.Count && ContainsInteger(body[cursor].Tokens)) rows.Add(body[cursor++]);
            if (rows.Count == 0) throw new LevelTextException(clueLine.Line, "No row lines follow the column clues.");

            (int Line, string[] Tokens)? bottomEdge = null;
            if (cursor < body.Count) bottomEdge = body[cursor++];
            if (cursor < body.Count) throw new LevelTextException(body[cursor].Line, "Unexpected line after the bottom edge line.");

            var level = new LevelData(width, rows.Count) { Name = name };
            Array.Copy(columnClues, level.ColumnClues, width);

            Tunnel? entrance = null;
            Tunnel? exit = null;

            void Assign(string token, Tunnel tunnel, int line)
            {
                if (token == "S")
                {
                    if (entrance.HasValue) throw new LevelTextException(line, "More than one entrance (S).");
                    entrance = tunnel;
                }
                else
                {
                    if (exit.HasValue) throw new LevelTextException(line, "More than one exit (E).");
                    exit = tunnel;
                }
            }

            void ParseEdge((int Line, string[] Tokens) edge, Direction side)
            {
                if (edge.Tokens.Length != width)
                    throw new LevelTextException(edge.Line, $"Edge line needs {width} tokens, found {edge.Tokens.Length}.");
                for (var x = 0; x < width; x++)
                {
                    var token = edge.Tokens[x];
                    if (token == ".") continue;
                    if (IsTunnelToken(token)) Assign(token, new Tunnel(side, x), edge.Line);
                    else throw new LevelTextException(edge.Line, $"Edge line accepts only '.', 'S' or 'E', found '{token}'.");
                }
            }

            if (topEdge.HasValue) ParseEdge(topEdge.Value, Direction.North);

            for (var y = 0; y < rows.Count; y++)
            {
                var (line, tokens) = rows[y];
                var i = 0;
                if (IsTunnelToken(tokens[0]))
                {
                    Assign(tokens[0], new Tunnel(Direction.West, y), line);
                    i++;
                }

                if (tokens.Length < i + width + 1)
                    throw new LevelTextException(line, $"Row needs {width} cells followed by a clue.");

                for (var x = 0; x < width; x++)
                {
                    var token = tokens[i + x];
                    if (token == ".") continue;
                    if (PieceKeys.TryParse(token, out var key)) level.FixedPieces.Add(new FixedPiece(x, y, key));
                    else throw new LevelTextException(line, $"Unknown cell token '{token}'.");
                }

                i += width;

                if (!int.TryParse(tokens[i], out var clue))
                    throw new LevelTextException(line, $"Expected the row clue, found '{tokens[i]}'.");
                level.RowClues[y] = clue;
                i++;

                if (i < tokens.Length && IsTunnelToken(tokens[i]))
                {
                    Assign(tokens[i], new Tunnel(Direction.East, y), line);
                    i++;
                }

                if (i != tokens.Length)
                    throw new LevelTextException(line, $"Unexpected token '{tokens[i]}' after the row clue.");
            }

            if (bottomEdge.HasValue) ParseEdge(bottomEdge.Value, Direction.South);

            if (!entrance.HasValue) throw new LevelTextException(0, "No entrance (S) found.");
            if (!exit.HasValue) throw new LevelTextException(0, "No exit (E) found.");
            level.Entrance = entrance.Value;
            level.Exit = exit.Value;
            return level;
        }

        public static string Serialize(LevelData level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(level.Name)) sb.Append(NamePrefix).Append(' ').Append(level.Name).Append('\n');

            // Rows with a west tunnel start with "S "/"E "; indenting the other lines keeps columns aligned.
            var indent = HasTunnelOn(level, Direction.West) ? "  " : "";

            if (HasTunnelOn(level, Direction.North)) sb.Append(indent).Append(EdgeLine(level, Direction.North)).Append('\n');

            sb.Append(indent);
            for (var x = 0; x < level.Width; x++)
            {
                if (x > 0) sb.Append(' ');
                sb.Append(level.ColumnClues[x]);
            }

            sb.Append('\n');

            for (var y = 0; y < level.Height; y++)
            {
                var west = TunnelToken(level, Direction.West, y);
                if (west != null) sb.Append(west).Append(' ');

                for (var x = 0; x < level.Width; x++)
                {
                    if (x > 0) sb.Append(' ');
                    sb.Append(level.TryGetFixedPiece(x, y, out var piece) ? piece.Key.ToString() : ".");
                }

                sb.Append(' ').Append(level.RowClues[y]);

                var east = TunnelToken(level, Direction.East, y);
                if (east != null) sb.Append(' ').Append(east);
                sb.Append('\n');
            }

            if (HasTunnelOn(level, Direction.South)) sb.Append(indent).Append(EdgeLine(level, Direction.South)).Append('\n');

            return sb.ToString();
        }

        private static string EdgeLine(LevelData level, Direction side)
        {
            var sb = new StringBuilder();
            for (var x = 0; x < level.Width; x++)
            {
                if (x > 0) sb.Append(' ');
                sb.Append(TunnelToken(level, side, x) ?? ".");
            }

            return sb.ToString();
        }

        private static string TunnelToken(LevelData level, Direction side, int index)
        {
            var tunnel = new Tunnel(side, index);
            if (level.Entrance == tunnel) return "S";
            if (level.Exit == tunnel) return "E";
            return null;
        }

        private static bool HasTunnelOn(LevelData level, Direction side) => level.Entrance.Side == side || level.Exit.Side == side;

        private static bool IsTunnelToken(string token) => token == "S" || token == "E";

        private static bool ContainsInteger(string[] tokens)
        {
            foreach (var token in tokens)
                if (int.TryParse(token, out _)) return true;
            return false;
        }
    }
}
