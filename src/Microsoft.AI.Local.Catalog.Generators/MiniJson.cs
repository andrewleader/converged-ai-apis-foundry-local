using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Microsoft.AI.Local.Catalog.Generators;

/// <summary>
/// A small, strict JSON reader. Source generators run on netstandard2.0 inside the compiler, where taking a
/// System.Text.Json dependency is fragile; the manifest is simple enough not to need it.
/// Objects become <see cref="Dictionary{TKey, TValue}"/>, arrays <see cref="List{T}"/>, numbers <see cref="double"/>.
/// </summary>
internal sealed class MiniJson
{
    private readonly string _text;
    private int _pos;

    private MiniJson(string text) => _text = text;

    public static object? Parse(string text)
    {
        var reader = new MiniJson(text);
        reader.SkipWhitespace();
        var value = reader.ReadValue();
        reader.SkipWhitespace();
        if (reader._pos != text.Length)
        {
            throw reader.Error("Unexpected trailing content");
        }

        return value;
    }

    private object? ReadValue()
    {
        SkipWhitespace();
        if (_pos >= _text.Length)
        {
            throw Error("Unexpected end of input");
        }

        switch (_text[_pos])
        {
            case '{': return ReadObject();
            case '[': return ReadArray();
            case '"': return ReadString();
            case 't': Expect("true"); return true;
            case 'f': Expect("false"); return false;
            case 'n': Expect("null"); return null;
            default: return ReadNumber();
        }
    }

    private Dictionary<string, object?> ReadObject()
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        _pos++;
        SkipWhitespace();
        if (Peek() == '}')
        {
            _pos++;
            return result;
        }

        while (true)
        {
            SkipWhitespace();
            if (Peek() != '"')
            {
                throw Error("Expected a property name");
            }

            var name = ReadString();
            SkipWhitespace();
            if (Peek() != ':')
            {
                throw Error("Expected ':'");
            }

            _pos++;
            if (result.ContainsKey(name))
            {
                throw Error($"Duplicate property '{name}'");
            }

            result[name] = ReadValue();
            SkipWhitespace();
            var c = Peek();
            _pos++;
            if (c == '}')
            {
                return result;
            }

            if (c != ',')
            {
                throw Error("Expected ',' or '}'");
            }
        }
    }

    private List<object?> ReadArray()
    {
        var result = new List<object?>();
        _pos++;
        SkipWhitespace();
        if (Peek() == ']')
        {
            _pos++;
            return result;
        }

        while (true)
        {
            result.Add(ReadValue());
            SkipWhitespace();
            var c = Peek();
            _pos++;
            if (c == ']')
            {
                return result;
            }

            if (c != ',')
            {
                throw Error("Expected ',' or ']'");
            }
        }
    }

    private string ReadString()
    {
        _pos++;
        var sb = new StringBuilder();
        while (true)
        {
            if (_pos >= _text.Length)
            {
                throw Error("Unterminated string");
            }

            var c = _text[_pos++];
            if (c == '"')
            {
                return sb.ToString();
            }

            if (c != '\\')
            {
                if (c < 0x20)
                {
                    throw Error("Control character in string");
                }

                sb.Append(c);
                continue;
            }

            if (_pos >= _text.Length)
            {
                throw Error("Unterminated escape");
            }

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
                    if (_pos + 4 > _text.Length)
                    {
                        throw Error("Invalid \\u escape");
                    }

                    sb.Append((char)int.Parse(_text.Substring(_pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    _pos += 4;
                    break;
                default:
                    throw Error($"Invalid escape '\\{e}'");
            }
        }
    }

    private double ReadNumber()
    {
        var start = _pos;
        while (_pos < _text.Length && "+-0123456789.eE".IndexOf(_text[_pos]) >= 0)
        {
            _pos++;
        }

        if (start == _pos || !double.TryParse(_text.Substring(start, _pos - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            _pos = start;
            throw Error("Invalid value");
        }

        return value;
    }

    private void Expect(string literal)
    {
        if (string.CompareOrdinal(_text, _pos, literal, 0, literal.Length) != 0)
        {
            throw Error("Invalid literal");
        }

        _pos += literal.Length;
    }

    private char Peek() => _pos < _text.Length ? _text[_pos] : '\0';

    private void SkipWhitespace()
    {
        while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
        {
            _pos++;
        }
    }

    private FormatException Error(string message)
    {
        var line = 1;
        var column = 1;
        for (var i = 0; i < _pos && i < _text.Length; i++)
        {
            if (_text[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return new FormatException($"{message} at line {line}, column {column}.");
    }
}
