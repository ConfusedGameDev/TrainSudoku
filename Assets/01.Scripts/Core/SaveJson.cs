using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TrainSudoku.Core
{
    /// <summary>
    /// Everything the game persists (PRD section 6): best times keyed by level id, from which unlocking derives, and the
    /// in-progress snapshot of every level the player left unfinished.
    /// </summary>
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;
        public Dictionary<string, double> BestTimes { get; } = new Dictionary<string, double>(StringComparer.Ordinal);
        public Dictionary<string, LevelProgress> InProgress { get; } = new Dictionary<string, LevelProgress>(StringComparer.Ordinal);
    }

    /// <summary>
    /// The save file format:
    /// <c>{"version":1,"bestTimes":{"level-id":12.5},"inProgress":{"level-id":{"elapsed":40.25,"pieces":[{"x":1,"y":0,"key":"NE"}]}}}</c>.
    /// A small hand-written reader and writer so Core stays free of UnityEngine; the reader accepts any well-formed JSON
    /// and ignores unknown members. A malformed in-progress entry is skipped rather than failing the file, so a bad
    /// snapshot never costs the best times.
    /// </summary>
    public static class SaveJson
    {
        public static string Write(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var sb = new StringBuilder();
            sb.Append("{\n  \"version\": ").Append(data.Version.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\n  \"bestTimes\": {");
            var first = true;
            var ids = new List<string>(data.BestTimes.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                sb.Append(first ? "\n" : ",\n").Append("    ");
                WriteString(sb, id);
                sb.Append(": ").Append(data.BestTimes[id].ToString("R", CultureInfo.InvariantCulture));
                first = false;
            }

            sb.Append(first ? "}" : "\n  }");

            sb.Append(",\n  \"inProgress\": {");
            first = true;
            ids = new List<string>(data.InProgress.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                var progress = data.InProgress[id];
                if (progress == null) continue;
                sb.Append(first ? "\n" : ",\n").Append("    ");
                WriteString(sb, id);
                sb.Append(": {\n      \"elapsed\": ").Append(progress.Elapsed.ToString("R", CultureInfo.InvariantCulture));
                sb.Append(",\n      \"pieces\": [");
                var firstPiece = true;
                foreach (var piece in progress.Pieces)
                {
                    sb.Append(firstPiece ? "\n" : ",\n").Append("        ");
                    sb.Append("{ \"x\": ").Append(piece.X.ToString(CultureInfo.InvariantCulture));
                    sb.Append(", \"y\": ").Append(piece.Y.ToString(CultureInfo.InvariantCulture));
                    sb.Append(", \"key\": ");
                    WriteString(sb, piece.Key.ToString());
                    sb.Append(" }");
                    firstPiece = false;
                }

                sb.Append(firstPiece ? "]" : "\n      ]");
                sb.Append("\n    }");
                first = false;
            }

            sb.Append(first ? "}" : "\n  }");
            sb.Append("\n}\n");
            return sb.ToString();
        }

        /// <summary>Parses a save file. False when the text is not valid JSON or lacks the expected shape.</summary>
        public static bool TryRead(string json, out SaveData data)
        {
            data = null;
            if (json == null) return false;
            try
            {
                var root = new Parser(json).ParseDocument();
                if (!(root is Dictionary<string, object> obj)) return false;

                var result = new SaveData();
                if (obj.TryGetValue("version", out var version) && version is double v) result.Version = (int)v;

                if (obj.TryGetValue("bestTimes", out var times))
                {
                    if (!(times is Dictionary<string, object> map)) return false;
                    foreach (var pair in map)
                    {
                        if (!(pair.Value is double seconds) || seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return false;
                        result.BestTimes[pair.Key] = seconds;
                    }
                }

                if (obj.TryGetValue("inProgress", out var snapshots))
                {
                    if (!(snapshots is Dictionary<string, object> map)) return false;
                    foreach (var pair in map)
                        if (TryReadProgress(pair.Value, out var progress)) result.InProgress[pair.Key] = progress;
                }

                data = result;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>One in-progress entry. False for anything misshapen, which the caller skips.</summary>
        private static bool TryReadProgress(object value, out LevelProgress progress)
        {
            progress = null;
            if (!(value is Dictionary<string, object> obj)) return false;
            if (!obj.TryGetValue("elapsed", out var elapsedValue) || !(elapsedValue is double elapsed)) return false;
            if (elapsed < 0 || double.IsNaN(elapsed) || double.IsInfinity(elapsed)) return false;

            var pieces = new List<PlacedPiece>();
            if (obj.TryGetValue("pieces", out var piecesValue))
            {
                if (!(piecesValue is List<object> list)) return false;
                foreach (var item in list)
                {
                    if (!(item is Dictionary<string, object> pieceObj)) return false;
                    if (!TryReadInt(pieceObj, "x", out var x) || !TryReadInt(pieceObj, "y", out var y)) return false;
                    if (!pieceObj.TryGetValue("key", out var keyValue) || !(keyValue is string keyText)) return false;
                    if (!PieceKeys.TryParse(keyText, out var key)) return false;
                    pieces.Add(new PlacedPiece(x, y, key));
                }
            }

            progress = new LevelProgress(elapsed, pieces);
            return true;
        }

        private static bool TryReadInt(Dictionary<string, object> obj, string name, out int value)
        {
            value = 0;
            if (!obj.TryGetValue(name, out var raw) || !(raw is double number)) return false;
            if (number != Math.Floor(number) || number < int.MinValue || number > int.MaxValue) return false;
            value = (int)number;
            return true;
        }

        private static void WriteString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }

            sb.Append('"');
        }

        /// <summary>Recursive-descent JSON reader producing dictionaries, lists, strings, doubles, bools and null.</summary>
        private sealed class Parser
        {
            private readonly string _text;
            private int _pos;

            public Parser(string text)
            {
                _text = text;
            }

            public object ParseDocument()
            {
                var value = ParseValue();
                SkipWhitespace();
                if (_pos != _text.Length) throw Error("trailing characters");
                return value;
            }

            private object ParseValue()
            {
                SkipWhitespace();
                if (_pos >= _text.Length) throw Error("unexpected end");
                var c = _text[_pos];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                        throw Error($"unexpected '{c}'");
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                _pos++; // {
                SkipWhitespace();
                if (Peek() == '}')
                {
                    _pos++;
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (Peek() != '"') throw Error("expected a member name");
                    var name = ParseString();
                    SkipWhitespace();
                    if (Peek() != ':') throw Error("expected ':'");
                    _pos++;
                    result[name] = ParseValue();
                    SkipWhitespace();
                    var next = Peek();
                    _pos++;
                    if (next == '}') return result;
                    if (next != ',') throw Error("expected ',' or '}'");
                }
            }

            private List<object> ParseArray()
            {
                var result = new List<object>();
                _pos++; // [
                SkipWhitespace();
                if (Peek() == ']')
                {
                    _pos++;
                    return result;
                }

                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();
                    var next = Peek();
                    _pos++;
                    if (next == ']') return result;
                    if (next != ',') throw Error("expected ',' or ']'");
                }
            }

            private string ParseString()
            {
                _pos++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (_pos >= _text.Length) throw Error("unterminated string");
                    var c = _text[_pos++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }

                    if (_pos >= _text.Length) throw Error("unterminated escape");
                    var e = _text[_pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_pos + 4 > _text.Length) throw Error("short unicode escape");
                            if (!int.TryParse(_text.Substring(_pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                                throw Error("bad unicode escape");
                            sb.Append((char)code);
                            _pos += 4;
                            break;
                        default: throw Error($"bad escape '\\{e}'");
                    }
                }
            }

            private double ParseNumber()
            {
                var start = _pos;
                if (Peek() == '-') _pos++;
                while (_pos < _text.Length && (char.IsDigit(_text[_pos]) || _text[_pos] == '.' || _text[_pos] == 'e' || _text[_pos] == 'E' || _text[_pos] == '+' || _text[_pos] == '-'))
                    _pos++;
                var token = _text.Substring(start, _pos - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) throw Error($"bad number '{token}'");
                return value;
            }

            private void Expect(string literal)
            {
                if (string.CompareOrdinal(_text, _pos, literal, 0, literal.Length) != 0) throw Error($"expected '{literal}'");
                _pos += literal.Length;
            }

            private char Peek() => _pos < _text.Length ? _text[_pos] : throw Error("unexpected end");

            private void SkipWhitespace()
            {
                while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos])) _pos++;
            }

            private FormatException Error(string message) => new FormatException($"Invalid JSON at {_pos}: {message}.");
        }
    }
}
