using System.Globalization;
using System.Text;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// Serializes structured field values (RFC 8941 section 4.1), as needed to build the <c>Signature-Input</c> and
/// <c>Signature</c> headers and the <c>@signature-params</c> line of an HTTP message signature.
/// </summary>
public static class StructuredFieldSerializer
{
    /// <summary>
    /// Serializes a dictionary.
    /// </summary>
    /// <param name="members">The members, in order.</param>
    /// <returns>The serialized dictionary.</returns>
    public static string SerializeDictionary(IEnumerable<KeyValuePair<string, StructuredFieldMember>> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        var builder = new StringBuilder();

        foreach (var member in members)
        {
            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(SerializeKey(member.Key));

            if (!member.Value.IsInnerList && member.Value.Item.Value is true)
            {
                builder.Append(SerializeParameters(member.Value.Parameters));
            }
            else
            {
                builder.Append('=').Append(SerializeMember(member.Value));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Serializes the value of a dictionary member: an item or an inner list, with its parameters.
    /// </summary>
    /// <param name="member">The member value.</param>
    /// <returns>The serialized member value.</returns>
    public static string SerializeMember(StructuredFieldMember member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return member.IsInnerList
            ? SerializeInnerList(member.InnerList, member.Parameters)
            : SerializeItem(member.Item);
    }

    /// <summary>
    /// Serializes an inner list with its parameters, such as <c>("@method" "@path");keyid="k"</c>.
    /// </summary>
    /// <param name="items">The items of the list.</param>
    /// <param name="parameters">The parameters of the list, or <see langword="null"/> for none.</param>
    /// <returns>The serialized inner list.</returns>
    public static string SerializeInnerList(IEnumerable<StructuredFieldItem> items, IReadOnlyDictionary<string, object> parameters)
    {
        ArgumentNullException.ThrowIfNull(items);

        var builder = new StringBuilder("(");
        var first = true;

        foreach (var item in items)
        {
            if (!first)
            {
                builder.Append(' ');
            }

            builder.Append(SerializeItem(item));
            first = false;
        }

        return builder.Append(')').Append(SerializeParameters(parameters)).ToString();
    }

    /// <summary>
    /// Serializes an item with its parameters.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The serialized item.</returns>
    public static string SerializeItem(StructuredFieldItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return SerializeBareItem(item.Value, item.IsToken) + SerializeParameters(item.Parameters);
    }

    /// <summary>
    /// Serializes parameters, such as <c>;keyid="k";created=1618884473</c>.
    /// </summary>
    /// <param name="parameters">The parameters, in order, or <see langword="null"/> for none.</param>
    /// <returns>The serialized parameters, or an empty string when there are none.</returns>
    public static string SerializeParameters(IReadOnlyDictionary<string, object> parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        foreach (var parameter in parameters)
        {
            builder.Append(';').Append(SerializeKey(parameter.Key));

            if (parameter.Value is not true)
            {
                builder.Append('=').Append(SerializeBareItem(parameter.Value, false));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Serializes a bare item.
    /// </summary>
    /// <param name="value">The value: a <see cref="string"/>, <see cref="StructuredFieldToken"/>, <see cref="byte"/> array, integer, <see cref="decimal"/> or <see cref="bool"/>.</param>
    /// <param name="isToken">Whether a string value is a token.</param>
    /// <returns>The serialized bare item.</returns>
    public static string SerializeBareItem(object value, bool isToken)
    {
        return value switch
        {
            StructuredFieldToken token => SerializeToken(token.Value),
            string text when isToken => SerializeToken(text),
            string text => SerializeString(text),
            byte[] bytes => SerializeByteSequence(bytes),
            bool boolean => boolean ? "?1" : "?0",
            decimal number => SerializeDecimal(number),
            long number => SerializeInteger(number),
            int number => SerializeInteger(number),
            _ => throw new ArgumentException($"The value type '{value?.GetType().Name ?? "null"}' is not a structured field bare item.", nameof(value)),
        };
    }

    /// <summary>
    /// Serializes an sf-string, escaping <c>"</c> and <c>\</c>.
    /// </summary>
    /// <param name="value">The string, made of printable ASCII characters.</param>
    /// <returns>The quoted string.</returns>
    public static string SerializeString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new StringBuilder("\"");

        foreach (var c in value)
        {
            if (c < 0x20 || c > 0x7E)
            {
                throw new ArgumentException("A structured field string holds only printable ASCII characters.", nameof(value));
            }

            if (c == '"' || c == '\\')
            {
                builder.Append('\\');
            }

            builder.Append(c);
        }

        return builder.Append('"').ToString();
    }

    /// <summary>
    /// Serializes a byte sequence as <c>:&lt;base64&gt;:</c>.
    /// </summary>
    /// <param name="value">The bytes.</param>
    /// <returns>The serialized byte sequence.</returns>
    public static string SerializeByteSequence(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return ":" + Convert.ToBase64String(value) + ":";
    }

    private static string SerializeInteger(long value)
    {
        if (value > 999_999_999_999_999 || value < -999_999_999_999_999)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "A structured field integer has at most 15 digits.");
        }

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string SerializeDecimal(decimal value)
    {
        var rounded = Math.Round(value, 3, MidpointRounding.ToEven);

        if (Math.Abs(decimal.Truncate(rounded)) > 999_999_999_999m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "A structured field decimal has at most 12 integer digits.");
        }

        var text = rounded.ToString("0.0##", CultureInfo.InvariantCulture);

        return text;
    }

    private static string SerializeToken(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value;
    }

    private static string SerializeKey(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return key;
    }
}
