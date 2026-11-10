using System.Globalization;
using System.Text;

namespace WoWBoxer.Layouts.Core;

/// <summary>
/// A tiny read-only reader for WoW SavedVariables files (<c>Name = { ... }</c> Lua table literals). It understands only
/// data: strings, numbers, true/false/nil, nested tables with <c>["key"] =</c>, <c>key =</c>, <c>[1] =</c> and list
/// items, and <c>--</c> comments. Nothing is ever run. Tables become <see cref="Dictionary{TKey,TValue}"/> with string
/// keys (list items get "1", "2", …); numbers become double.
/// </summary>
public static class LuaData
{
    /// <summary>The top-level assignments of a SavedVariables file. Unreadable input gives what was read before the error.</summary>
    public static Dictionary<string, object?> ParseFile(string text)
    {
        var p = new Parser(text);
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        try
        {
            while (true)
            {
                p.Skip();
                if (p.End) break;
                var name = p.Name();
                p.Skip();
                p.Expect('=');
                result[name] = p.Value();
            }
        }
        catch (FormatException)
        {
        }
        return result;
    }

    /// <summary>Walks nested tables: <c>Get(db, "bridge", "bars")</c>; null when any step is missing.</summary>
    public static object? Get(object? value, params string[] path)
    {
        foreach (var key in path)
            value = value is Dictionary<string, object?> t && t.TryGetValue(key, out var v) ? v : null;
        return value;
    }

    public static double? Number(object? value, params string[] path) => Get(value, path) is double d ? d : null;

    public static string? Text(object? value, params string[] path) => Get(value, path) as string;

    /// <summary>The list items ("1", "2", …) of a table, in order.</summary>
    public static IEnumerable<object?> Items(object? table)
    {
        if (table is not Dictionary<string, object?> t) yield break;
        for (var i = 1; t.TryGetValue(i.ToString(CultureInfo.InvariantCulture), out var v); i++) yield return v;
    }

    private sealed class Parser(string s)
    {
        private int _i;

        public bool End => _i >= s.Length;

        public void Skip()
        {
            while (_i < s.Length)
            {
                if (char.IsWhiteSpace(s[_i])) _i++;
                else if (s[_i] == '-' && _i + 1 < s.Length && s[_i + 1] == '-')
                {
                    if (_i + 3 < s.Length && s[_i + 2] == '[' && s[_i + 3] == '[')
                    {
                        var close = s.IndexOf("]]", _i + 4, StringComparison.Ordinal);
                        _i = close < 0 ? s.Length : close + 2;
                    }
                    else
                        while (_i < s.Length && s[_i] != '\n') _i++;
                }
                else break;
            }
        }

        public void Expect(char c)
        {
            if (End || s[_i] != c) throw new FormatException($"expected '{c}' at {_i}");
            _i++;
        }

        public string Name()
        {
            var start = _i;
            while (_i < s.Length && (char.IsLetterOrDigit(s[_i]) || s[_i] == '_')) _i++;
            if (_i == start) throw new FormatException($"expected a name at {_i}");
            return s[start.._i];
        }

        public object? Value()
        {
            Skip();
            if (End) throw new FormatException("unexpected end");
            var c = s[_i];
            if (c == '{') return Table();
            if (c is '"' or '\'') return Str();
            if (c == '-' || c == '.' || char.IsDigit(c)) return Num();
            var word = Name();
            return word switch
            {
                "true" => true,
                "false" => false,
                "nil" => null,
                _ => throw new FormatException($"unexpected '{word}' at {_i}"),
            };
        }

        private Dictionary<string, object?> Table()
        {
            Expect('{');
            var t = new Dictionary<string, object?>(StringComparer.Ordinal);
            var n = 0;
            while (true)
            {
                Skip();
                if (End) throw new FormatException("unclosed table");
                if (s[_i] == '}')
                {
                    _i++;
                    return t;
                }
                string? key = null;
                if (s[_i] == '[')
                {
                    _i++;
                    var k = Value();
                    Skip();
                    Expect(']');
                    Skip();
                    Expect('=');
                    key = k switch
                    {
                        double d => d.ToString(CultureInfo.InvariantCulture),
                        bool b => b ? "true" : "false",
                        _ => k?.ToString() ?? "",
                    };
                }
                else if (char.IsLetter(s[_i]) || s[_i] == '_')
                {
                    var save = _i;
                    var name = Name();
                    Skip();
                    if (!End && s[_i] == '=' && (_i + 1 >= s.Length || s[_i + 1] != '='))
                    {
                        _i++;
                        key = name;
                    }
                    else _i = save;   // a value like true/false/nil
                }
                var v = Value();
                key ??= (++n).ToString(CultureInfo.InvariantCulture);
                t[key] = v;
                Skip();
                if (!End && (s[_i] == ',' || s[_i] == ';')) _i++;
            }
        }

        private string Str()
        {
            var q = s[_i++];
            var sb = new StringBuilder();
            while (true)
            {
                if (End) throw new FormatException("unclosed string");
                var c = s[_i++];
                if (c == q) return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (End) throw new FormatException("unclosed string");
                var e = s[_i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case '\n': sb.Append('\n'); break;
                    default:
                        if (char.IsDigit(e))
                        {
                            var start = _i - 1;
                            while (_i < s.Length && _i - start < 3 && char.IsDigit(s[_i])) _i++;
                            sb.Append((char)int.Parse(s[start.._i], CultureInfo.InvariantCulture));
                        }
                        else sb.Append(e);
                        break;
                }
            }
        }

        private double Num()
        {
            var start = _i;
            if (s[_i] == '-') _i++;
            while (_i < s.Length && (char.IsLetterOrDigit(s[_i]) || s[_i] is '.' or '+' or '-') && !(s[_i] == '-' && s[_i - 1] is not ('e' or 'E'))) _i++;
            var text = s[start.._i];
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || text.StartsWith("-0x", StringComparison.OrdinalIgnoreCase))
            {
                var neg = text[0] == '-';
                var hex = long.Parse(text[(neg ? 3 : 2)..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return neg ? -hex : hex;
            }
            if (text is "inf" or "-inf" or "nan") return double.NaN;
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : throw new FormatException($"bad number '{text}'");
        }
    }
}
