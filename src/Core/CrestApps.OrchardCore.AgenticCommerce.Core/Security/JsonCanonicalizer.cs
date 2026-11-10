using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// Serializes JSON in the JSON Canonicalization Scheme (JCS, RFC 8785): no whitespace, object members sorted by
/// their UTF-16 code units, ECMAScript number serialization and the minimal JSON string escaping.
/// </summary>
public static class JsonCanonicalizer
{
    /// <summary>
    /// Returns the canonical (RFC 8785) text of a JSON node.
    /// </summary>
    /// <param name="node">The node to canonicalize. A <see langword="null"/> node is the JSON literal <c>null</c>.</param>
    /// <returns>The canonical JSON text.</returns>
    /// <exception cref="ArgumentException">The node holds a number that is not finite or a string with a lone surrogate.</exception>
    public static string Canonicalize(JsonNode node)
    {
        var builder = new StringBuilder();
        Write(builder, node);

        return builder.ToString();
    }

    /// <summary>
    /// Returns the UTF-8 bytes of the canonical (RFC 8785) text of a JSON node, the form that is signed or hashed.
    /// </summary>
    /// <param name="node">The node to canonicalize. A <see langword="null"/> node is the JSON literal <c>null</c>.</param>
    /// <returns>The UTF-8 encoded canonical JSON.</returns>
    public static byte[] CanonicalizeToUtf8(JsonNode node)
    {
        return Encoding.UTF8.GetBytes(Canonicalize(node));
    }

    internal static string FormatNumber(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentException("JSON canonicalization does not support NaN or Infinity.", nameof(value));
        }

        if (value == 0)
        {
            return "0";
        }

        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var negative = text[0] == '-';

        if (negative)
        {
            text = text.Substring(1);
        }

        var exponent = 0;
        var mantissa = text;
        var exponentIndex = text.IndexOfAny(['E', 'e']);

        if (exponentIndex >= 0)
        {
            exponent = int.Parse(text.AsSpan(exponentIndex + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            mantissa = text.Substring(0, exponentIndex);
        }

        var dotIndex = mantissa.IndexOf('.');
        var digits = dotIndex >= 0
            ? mantissa.Remove(dotIndex, 1)
            : mantissa;
        var pointPosition = dotIndex >= 0
            ? dotIndex
            : mantissa.Length;

        var leadingZeros = 0;

        while (leadingZeros < digits.Length - 1 && digits[leadingZeros] == '0')
        {
            leadingZeros++;
        }

        digits = digits.Substring(leadingZeros).TrimEnd('0');
        pointPosition -= leadingZeros;

        if (digits.Length == 0)
        {
            return "0";
        }

        var k = digits.Length;
        var n = pointPosition + exponent;
        var builder = new StringBuilder();

        if (negative)
        {
            builder.Append('-');
        }

        if (k <= n && n <= 21)
        {
            builder.Append(digits).Append('0', n - k);
        }
        else if (0 < n && n <= 21)
        {
            builder.Append(digits, 0, n).Append('.').Append(digits, n, k - n);
        }
        else if (-6 < n && n <= 0)
        {
            builder.Append("0.").Append('0', -n).Append(digits);
        }
        else
        {
            var e = n - 1;
            builder.Append(digits[0]);

            if (k > 1)
            {
                builder.Append('.').Append(digits, 1, k - 1);
            }

            builder.Append('e').Append(e < 0 ? '-' : '+').Append(Math.Abs(e).ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static void Write(StringBuilder builder, JsonNode node)
    {
        switch (node)
        {
            case null:
                builder.Append("null");
                break;

            case JsonObject obj:
                WriteObject(builder, obj);
                break;

            case JsonArray array:
                builder.Append('[');

                for (var i = 0; i < array.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    Write(builder, array[i]);
                }

                builder.Append(']');
                break;

            case JsonValue value:
                WriteValue(builder, value);
                break;
        }
    }

    private static void WriteObject(StringBuilder builder, JsonObject obj)
    {
        var names = new List<string>(obj.Count);

        foreach (var property in obj)
        {
            names.Add(property.Key);
        }

        names.Sort(StringComparer.Ordinal);
        builder.Append('{');

        for (var i = 0; i < names.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            WriteString(builder, names[i]);
            builder.Append(':');
            Write(builder, obj[names[i]]);
        }

        builder.Append('}');
    }

    private static void WriteValue(StringBuilder builder, JsonValue value)
    {
        switch (value.GetValueKind())
        {
            case JsonValueKind.String:
                var text = value.TryGetValue<string>(out var stringValue)
                    ? stringValue
                    : JsonSerializer.Deserialize<string>(value.ToJsonString());
                WriteString(builder, text);
                break;

            case JsonValueKind.Number:
                var number = double.Parse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture);
                builder.Append(FormatNumber(number));
                break;

            case JsonValueKind.True:
                builder.Append("true");
                break;

            case JsonValueKind.False:
                builder.Append("false");
                break;

            default:
                builder.Append("null");
                break;
        }
    }

    private static void WriteString(StringBuilder builder, string value)
    {
        builder.Append('"');

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;

                case '\\':
                    builder.Append("\\\\");
                    break;

                case '\b':
                    builder.Append("\\b");
                    break;

                case '\f':
                    builder.Append("\\f");
                    break;

                case '\n':
                    builder.Append("\\n");
                    break;

                case '\r':
                    builder.Append("\\r");
                    break;

                case '\t':
                    builder.Append("\\t");
                    break;

                default:
                    if (c < 0x20)
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else if (char.IsHighSurrogate(c))
                    {
                        if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                        {
                            throw new ArgumentException("JSON canonicalization does not support lone surrogates.", nameof(value));
                        }

                        builder.Append(c).Append(value[i + 1]);
                        i++;
                    }
                    else if (char.IsLowSurrogate(c))
                    {
                        throw new ArgumentException("JSON canonicalization does not support lone surrogates.", nameof(value));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        builder.Append('"');
    }
}
