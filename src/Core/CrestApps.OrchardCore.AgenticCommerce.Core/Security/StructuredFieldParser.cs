using System.Globalization;
using System.Text;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// Parses structured field dictionaries (RFC 8941 section 4.2.2), the shape of the <c>Signature-Input</c>,
/// <c>Signature</c>, <c>Content-Digest</c> and <c>UCP-Agent</c> headers.
/// </summary>
public static class StructuredFieldParser
{
    /// <summary>
    /// Parses a structured field dictionary. Members keep their order; a repeated key keeps its last value.
    /// </summary>
    /// <param name="value">The header value.</param>
    /// <param name="dictionary">The parsed members when the value is valid.</param>
    /// <returns><see langword="true"/> when the value is a valid structured field dictionary.</returns>
    public static bool TryParseDictionary(string value, out IReadOnlyDictionary<string, StructuredFieldMember> dictionary)
    {
        dictionary = null;

        if (value is null)
        {
            return false;
        }

        var input = value.Trim(' ', '\t');
        var result = new OrderedDictionary<string, StructuredFieldMember>(StringComparer.Ordinal);
        var position = 0;

        while (position < input.Length)
        {
            if (!TryParseKey(input, ref position, out var key))
            {
                return false;
            }

            StructuredFieldMember member;

            if (position < input.Length && input[position] == '=')
            {
                position++;

                if (!TryParseItemOrInnerList(input, ref position, out member))
                {
                    return false;
                }
            }
            else
            {
                if (!TryParseParameters(input, ref position, out var parameters))
                {
                    return false;
                }

                member = new StructuredFieldMember(new StructuredFieldItem(true, false, parameters));
            }

            result[key] = member;
            SkipOptionalWhitespace(input, ref position);

            if (position >= input.Length)
            {
                break;
            }

            if (input[position] != ',')
            {
                return false;
            }

            position++;
            SkipOptionalWhitespace(input, ref position);

            if (position >= input.Length)
            {
                return false;
            }
        }

        dictionary = result;

        return true;
    }

    private static bool TryParseItemOrInnerList(string input, ref int position, out StructuredFieldMember member)
    {
        member = null;

        if (position < input.Length && input[position] == '(')
        {
            if (!TryParseInnerList(input, ref position, out var items, out var listParameters))
            {
                return false;
            }

            member = new StructuredFieldMember(items, listParameters);

            return true;
        }

        if (!TryParseItem(input, ref position, out var item))
        {
            return false;
        }

        member = new StructuredFieldMember(item);

        return true;
    }

    private static bool TryParseInnerList(
        string input,
        ref int position,
        out IReadOnlyList<StructuredFieldItem> items,
        out IReadOnlyDictionary<string, object> parameters)
    {
        items = null;
        parameters = null;

        position++;
        var list = new List<StructuredFieldItem>();

        while (position < input.Length)
        {
            while (position < input.Length && input[position] == ' ')
            {
                position++;
            }

            if (position >= input.Length)
            {
                return false;
            }

            if (input[position] == ')')
            {
                position++;

                if (!TryParseParameters(input, ref position, out parameters))
                {
                    return false;
                }

                items = list;

                return true;
            }

            if (!TryParseItem(input, ref position, out var item))
            {
                return false;
            }

            list.Add(item);

            if (position >= input.Length || (input[position] != ' ' && input[position] != ')'))
            {
                return false;
            }
        }

        return false;
    }

    private static bool TryParseItem(string input, ref int position, out StructuredFieldItem item)
    {
        item = null;

        if (!TryParseBareItem(input, ref position, out var value, out var isToken)
            || !TryParseParameters(input, ref position, out var parameters))
        {
            return false;
        }

        item = new StructuredFieldItem(value, isToken, parameters);

        return true;
    }

    private static bool TryParseParameters(string input, ref int position, out IReadOnlyDictionary<string, object> parameters)
    {
        parameters = null;
        var result = new OrderedDictionary<string, object>(StringComparer.Ordinal);

        while (position < input.Length && input[position] == ';')
        {
            position++;

            while (position < input.Length && input[position] == ' ')
            {
                position++;
            }

            if (!TryParseKey(input, ref position, out var key))
            {
                return false;
            }

            object value = true;

            if (position < input.Length && input[position] == '=')
            {
                position++;

                if (!TryParseBareItem(input, ref position, out value, out var isToken))
                {
                    return false;
                }

                if (isToken)
                {
                    value = new StructuredFieldToken((string)value);
                }
            }

            result[key] = value;
        }

        parameters = result;

        return true;
    }

    private static bool TryParseKey(string input, ref int position, out string key)
    {
        key = null;

        if (position >= input.Length || !(IsLowerAlpha(input[position]) || input[position] == '*'))
        {
            return false;
        }

        var start = position;
        position++;

        while (position < input.Length && IsKeyChar(input[position]))
        {
            position++;
        }

        key = input.Substring(start, position - start);

        return true;
    }

    private static bool TryParseBareItem(string input, ref int position, out object value, out bool isToken)
    {
        value = null;
        isToken = false;

        if (position >= input.Length)
        {
            return false;
        }

        var c = input[position];

        if (c == '-' || char.IsAsciiDigit(c))
        {
            return TryParseNumber(input, ref position, out value);
        }

        if (c == '"')
        {
            return TryParseString(input, ref position, out value);
        }

        if (char.IsAsciiLetter(c) || c == '*')
        {
            isToken = true;

            return TryParseToken(input, ref position, out value);
        }

        if (c == ':')
        {
            return TryParseByteSequence(input, ref position, out value);
        }

        if (c == '?')
        {
            return TryParseBoolean(input, ref position, out value);
        }

        return false;
    }

    private static bool TryParseNumber(string input, ref int position, out object value)
    {
        value = null;
        var start = position;

        if (input[position] == '-')
        {
            position++;
        }

        if (position >= input.Length || !char.IsAsciiDigit(input[position]))
        {
            return false;
        }

        var isDecimal = false;
        var digitStart = position;

        while (position < input.Length)
        {
            var c = input[position];

            if (char.IsAsciiDigit(c))
            {
                position++;
            }
            else if (c == '.' && !isDecimal)
            {
                if (position - digitStart > 12)
                {
                    return false;
                }

                isDecimal = true;
                position++;
            }
            else
            {
                break;
            }

            if ((!isDecimal && position - digitStart > 15) || (isDecimal && position - digitStart > 16))
            {
                return false;
            }
        }

        var text = input.Substring(start, position - start);

        if (!isDecimal)
        {
            value = long.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

            return true;
        }

        if (text.EndsWith('.') || text.Length - text.IndexOf('.') - 1 > 3)
        {
            return false;
        }

        value = decimal.Parse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

        return true;
    }

    private static bool TryParseString(string input, ref int position, out object value)
    {
        value = null;
        position++;
        var builder = new StringBuilder();

        while (position < input.Length)
        {
            var c = input[position++];

            if (c == '\\')
            {
                if (position >= input.Length)
                {
                    return false;
                }

                var next = input[position++];

                if (next != '"' && next != '\\')
                {
                    return false;
                }

                builder.Append(next);
            }
            else if (c == '"')
            {
                value = builder.ToString();

                return true;
            }
            else if (c < 0x20 || c > 0x7E)
            {
                return false;
            }
            else
            {
                builder.Append(c);
            }
        }

        return false;
    }

    private static bool TryParseToken(string input, ref int position, out object value)
    {
        var start = position;
        position++;

        while (position < input.Length && (IsTokenChar(input[position]) || input[position] == ':' || input[position] == '/'))
        {
            position++;
        }

        value = input.Substring(start, position - start);

        return true;
    }

    private static bool TryParseByteSequence(string input, ref int position, out object value)
    {
        value = null;
        var end = input.IndexOf(':', position + 1);

        if (end < 0)
        {
            return false;
        }

        var encoded = input.Substring(position + 1, end - position - 1);
        position = end + 1;

        foreach (var c in encoded)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '+' && c != '/' && c != '=')
            {
                return false;
            }
        }

        try
        {
            value = Convert.FromBase64String(encoded);

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryParseBoolean(string input, ref int position, out object value)
    {
        value = null;

        if (position + 1 >= input.Length)
        {
            return false;
        }

        var c = input[position + 1];

        if (c != '0' && c != '1')
        {
            return false;
        }

        value = c == '1';
        position += 2;

        return true;
    }

    private static void SkipOptionalWhitespace(string input, ref int position)
    {
        while (position < input.Length && (input[position] == ' ' || input[position] == '\t'))
        {
            position++;
        }
    }

    private static bool IsLowerAlpha(char c)
    {
        return c >= 'a' && c <= 'z';
    }

    private static bool IsKeyChar(char c)
    {
        return IsLowerAlpha(c) || char.IsAsciiDigit(c) || c == '_' || c == '-' || c == '.' || c == '*';
    }

    private static bool IsTokenChar(char c)
    {
        return char.IsAsciiLetterOrDigit(c) || "!#$%&'*+-.^_`|~".Contains(c);
    }
}
