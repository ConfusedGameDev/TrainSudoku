using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TrainSudoku.Core
{
    /// <summary>Everything the game persists (PRD section 6): best times keyed by level id. Unlocking derives from them.</summary>
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;
        public Dictionary<string, double> BestTimes { get; } = new Dictionary<string, double>(StringComparer.Ordinal);
    }

    /// <summary>
    /// The save file format: <c>{"version":1,"bestTimes":{"level-id":12.5}}</c>. A small hand-written reader and writer
    /// so Core stays free of UnityEngine; the reader accepts any well-formed JSON and ignores unknown members.
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

                data = result;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
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
