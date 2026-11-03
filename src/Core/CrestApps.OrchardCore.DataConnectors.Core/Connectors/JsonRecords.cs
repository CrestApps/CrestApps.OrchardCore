using System.Globalization;
using System.Text.Json;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.DataConnectors.Connectors;

/// <summary>
/// Reads records out of JSON documents: finds them at a path, and flattens each into source fields named by their
/// dotted path, such as <c>address.city</c>.
/// </summary>
internal static class JsonRecords
{
    private const int _maxDepth = 4;

    /// <summary>
    /// Returns the element at a dotted path, such as <c>data.items</c>. An empty path is the root.
    /// </summary>
    public static JsonElement? Select(JsonElement root, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return root;
        }

        var current = root;

        foreach (var segment in path.Trim().TrimStart('$').Trim('.').Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = segment;
            int? index = null;
            var bracket = segment.IndexOf('[');

            if (bracket > 0 && segment.EndsWith(']') && int.TryParse(segment[(bracket + 1)..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var position))
            {
                name = segment[..bracket];
                index = position;
            }

            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current))
            {
                return null;
            }

            if (index is { } at)
            {
                if (current.ValueKind != JsonValueKind.Array || at >= current.GetArrayLength())
                {
                    return null;
                }

                current = current[at];
            }
        }

        return current;
    }

    /// <summary>
    /// Returns the records at a path: the items of an array, or the object itself.
    /// </summary>
    public static IReadOnlyList<JsonElement> SelectRecords(JsonElement root, string path)
    {
        var element = Select(root, path);

        return element?.ValueKind switch
        {
            JsonValueKind.Array => [.. element.Value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object)],
            JsonValueKind.Object => [element.Value],
            _ => [],
        };
    }

    /// <summary>
    /// Returns the text of a scalar at a path, such as a cursor or a next-page link, or <see langword="null"/>.
    /// </summary>
    public static string SelectText(JsonElement root, string path)
    {
        var element = Select(root, path);

        return element?.ValueKind switch
        {
            JsonValueKind.String => element.Value.GetString(),
            JsonValueKind.Number => element.Value.GetRawText(),
            _ => null,
        };
    }

    /// <summary>
    /// Flattens a record into source fields: nested objects become dotted names, arrays stay as JSON text.
    /// </summary>
    public static Dictionary<string, object> Flatten(JsonElement record)
    {
        var values = new Dictionary<string, object>(StringComparer.Ordinal);

        Flatten(record, null, values, 0);

        return values;
    }

    /// <summary>
    /// Returns a JSON scalar as a value: text, a whole number, a decimal number or a boolean.
    /// </summary>
    public static object ToValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var whole)
                ? whole
                : element.TryGetDecimal(out var number) ? number : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText(),
        };
    }

    private static void Flatten(JsonElement element, string prefix, Dictionary<string, object> values, int depth)
    {
        if (element.ValueKind == JsonValueKind.Object && (depth < _maxDepth || prefix is null))
        {
            foreach (var property in element.EnumerateObject())
            {
                var name = prefix is null ? property.Name : $"{prefix}.{property.Name}";

                if (property.Value.ValueKind == JsonValueKind.Object && depth + 1 < _maxDepth)
                {
                    Flatten(property.Value, name, values, depth + 1);
                }
                else
                {
                    values[name] = ToValue(property.Value);
                }
            }

            return;
        }

        if (prefix is not null)
        {
            values[prefix] = ToValue(element);
        }
    }
}

/// <summary>
/// Infers the fields of a source from a sample of its rows.
/// </summary>
internal static class FieldInference
{
    /// <summary>
    /// Returns a field per name, typed by the values the sample holds. With <paramref name="parseText"/>, text that
    /// reads as a number, a boolean or a date gives that type, as in a CSV file.
    /// </summary>
    public static IReadOnlyList<DataField> Infer(IReadOnlyList<string> names, IEnumerable<object[]> rows, bool parseText)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(rows);

        var types = new DataFieldType?[names.Count];
        var mixed = new bool[names.Count];

        foreach (var row in rows)
        {
            for (var index = 0; index < names.Count && index < row.Length; index++)
            {
                if (mixed[index] || DataValues.IsEmpty(row[index]))
                {
                    continue;
                }

                var type = TypeOf(row[index], parseText);

                if (types[index] is null)
                {
                    types[index] = type;
                }
                else if (types[index] != type)
                {
                    var combined = Combine(types[index].Value, type);

                    if (combined is null)
                    {
                        mixed[index] = true;
                        types[index] = DataFieldType.Text;
                    }
                    else
                    {
                        types[index] = combined;
                    }
                }
            }
        }

        return [.. names.Select((name, index) => new DataField(name, name, types[index] ?? DataFieldType.Text))];
    }

    /// <summary>
    /// Returns the fields of flattened JSON records, in the order their names first appear.
    /// </summary>
    public static IReadOnlyList<DataField> Infer(IReadOnlyList<Dictionary<string, object>> records)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var record in records)
        {
            foreach (var name in record.Keys)
            {
                if (seen.Add(name))
                {
                    names.Add(name);
                }
            }
        }

        var rows = records.Select(record => names.Select(name => record.GetValueOrDefault(name)).ToArray());

        return Infer(names, rows, parseText: false);
    }

    private static DataFieldType TypeOf(object value, bool parseText)
    {
        if (value is string text)
        {
            return parseText ? ParseType(text) : LooksLikeDate(text);
        }

        return DataValues.InferType(value);
    }

    private static DataFieldType ParseType(string text)
    {
        var trimmed = text.Trim();

        if (long.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _) && !HasLeadingZero(trimmed))
        {
            return DataFieldType.Integer;
        }

        if (decimal.TryParse(trimmed, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _) && !HasLeadingZero(trimmed))
        {
            return DataFieldType.Decimal;
        }

        if (bool.TryParse(trimmed, out _))
        {
            return DataFieldType.Boolean;
        }

        return LooksLikeDate(trimmed);
    }

    // A leading zero ("00501") is an identifier, such as a postal code, not a number.
    private static bool HasLeadingZero(string text)
    {
        var digits = text.TrimStart('-', '+');

        return digits.Length > 1 && digits[0] == '0' && digits[1] != '.';
    }

    private static DataFieldType LooksLikeDate(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.Length < 10 || !char.IsAsciiDigit(trimmed[0]) || trimmed[4] != '-' || trimmed[7] != '-')
        {
            return DataFieldType.Text;
        }

        if (DataValues.Coerce(trimmed, DataFieldType.DateTime) is not DateTime)
        {
            return DataFieldType.Text;
        }

        return trimmed.Length == 10 ? DataFieldType.Date : DataFieldType.DateTime;
    }

    private static DataFieldType? Combine(DataFieldType left, DataFieldType right)
    {
        if (DataValues.IsNumeric(left) && DataValues.IsNumeric(right))
        {
            return DataFieldType.Decimal;
        }

        if (DataValues.IsTemporal(left) && DataValues.IsTemporal(right))
        {
            return DataFieldType.DateTime;
        }

        return null;
    }
}
