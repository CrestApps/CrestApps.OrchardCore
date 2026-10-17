using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Reports.DataSources;

namespace CrestApps.OrchardCore.Reports.Queries;

/// <summary>
/// Turns the items an Orchard Core query returns into report rows. A query has no declared columns, so its fields are
/// found in its results: every item is written as JSON, nested objects are flattened into dotted names (for example
/// <c>TitlePart.Title</c>), lists of plain values are joined with commas, and each field's type is inferred from the
/// values found.
/// </summary>
public static class QueryResultSchema
{
    /// <summary>
    /// The deepest level of nested objects that is flattened.
    /// </summary>
    public const int MaxDepth = 4;

    /// <summary>
    /// The most fields read from one query.
    /// </summary>
    public const int MaxFields = 300;

    /// <summary>
    /// Flattens one query result item.
    /// </summary>
    /// <param name="item">The item: a JSON node, a content item, a dictionary, or any serializable object.</param>
    /// <returns>The values by dotted field name, in the order they appear.</returns>
    public static IReadOnlyList<KeyValuePair<string, JsonNode>> Flatten(object item)
    {
        var values = new List<KeyValuePair<string, JsonNode>>();
        JsonNode node;

        try
        {
            node = item as JsonNode ?? JsonSerializer.SerializeToNode(item);
        }
        catch (Exception exception) when (exception is NotSupportedException or JsonException or InvalidOperationException)
        {
            return values;
        }

        if (node is JsonObject obj)
        {
            Flatten(obj, null, 1, values);
        }
        else if (node is not null)
        {
            values.Add(new KeyValuePair<string, JsonNode>("Value", node));
        }

        return values;
    }

    /// <summary>
    /// Infers the fields of a query from sample rows.
    /// </summary>
    /// <param name="rows">The flattened rows.</param>
    /// <returns>The fields, in the order they first appear.</returns>
    public static IList<ReportFieldDescriptor> InferFields(IEnumerable<IReadOnlyList<KeyValuePair<string, JsonNode>>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var order = new List<string>();
        var types = new Dictionary<string, ReportDataType?>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            foreach (var (name, value) in row)
            {
                if (!types.TryGetValue(name, out var current))
                {
                    if (order.Count >= MaxFields)
                    {
                        continue;
                    }

                    order.Add(name);
                    current = null;
                }

                var valueType = InferType(value);

                types[name] = valueType is null ? current : Merge(current, valueType.Value);
            }
        }

        return order
            .Select(name => new ReportFieldDescriptor(name, name, types[name] ?? ReportDataType.Text, Group(name))
            {
                IsIdentifier = name.EndsWith("Id", StringComparison.Ordinal) || name.EndsWith("ID", StringComparison.Ordinal),
            })
            .ToList();
    }

    /// <summary>
    /// Converts a flattened JSON value to the CLR type of a field. Date-time text without a time zone is read as UTC.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="dataType">The field type.</param>
    /// <returns>The converted value, or <see langword="null"/>.</returns>
    public static object ToValue(JsonNode value, ReportDataType dataType)
    {
        if (value is null)
        {
            return null;
        }

        var converted = ReportDataValues.Coerce(value, dataType);

        if (converted is DateTime date)
        {
            return dataType == ReportDataType.Date
                ? DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified)
                : date.Kind == DateTimeKind.Local ? date.ToUniversalTime() : DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }

        return converted;
    }

    private static void Flatten(JsonObject obj, string prefix, int depth, List<KeyValuePair<string, JsonNode>> values)
    {
        foreach (var (name, child) in obj)
        {
            var key = prefix is null ? name : prefix + "." + name;

            switch (child)
            {
                case JsonObject nested when depth < MaxDepth:
                    Flatten(nested, key, depth + 1, values);

                    break;

                case JsonObject:
                    break;

                case JsonArray array:
                    var parts = array
                        .OfType<JsonValue>()
                        .Select(item => ReportDataValues.ToText(item))
                        .Where(text => !string.IsNullOrEmpty(text))
                        .ToArray();

                    if (parts.Length > 0 || array.Count == 0)
                    {
                        values.Add(new KeyValuePair<string, JsonNode>(key, JsonValue.Create(string.Join(", ", parts))));
                    }

                    break;

                default:
                    values.Add(new KeyValuePair<string, JsonNode>(key, child));

                    break;
            }
        }
    }

    private static ReportDataType? InferType(JsonNode value)
    {
        if (value is not JsonValue jsonValue)
        {
            return null;
        }

        var element = ReportDataValues.ToElement(jsonValue);

        switch (element.ValueKind)
        {
            case JsonValueKind.True:
            case JsonValueKind.False:
                return ReportDataType.Boolean;

            case JsonValueKind.Number:
                return element.TryGetInt64(out _) ? ReportDataType.Integer : ReportDataType.Decimal;

            case JsonValueKind.String:
                var text = element.GetString();

                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                if (LooksLikeDate(text, out var hasTime))
                {
                    return hasTime ? ReportDataType.DateTime : ReportDataType.Date;
                }

                return ReportDataType.Text;

            default:
                return null;
        }
    }

    private static bool LooksLikeDate(string text, out bool hasTime)
    {
        hasTime = false;

        if (text.Length < 10 || text.Length > 40 || !char.IsDigit(text[0]) || text[4] != '-' || text[7] != '-')
        {
            return false;
        }

        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out _))
        {
            return false;
        }

        hasTime = text.Length > 10;

        return true;
    }

    private static ReportDataType Merge(ReportDataType? current, ReportDataType next)
    {
        if (current is null || current == next)
        {
            return next;
        }

        if (ReportDataValues.IsNumeric(current.Value) && ReportDataValues.IsNumeric(next))
        {
            return ReportDataType.Decimal;
        }

        if (ReportDataValues.IsTemporal(current.Value) && ReportDataValues.IsTemporal(next))
        {
            return ReportDataType.DateTime;
        }

        return ReportDataType.Text;
    }

    private static string Group(string name)
    {
        var dot = name.LastIndexOf('.');

        return dot > 0 ? name[..dot] : null;
    }
}
