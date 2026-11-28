using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.AI.Agent.Queries;

/// <summary>
/// Turns the rows a query returned into a bounded tool result: one page of rows, long values shortened, the whole
/// page kept under a size limit, and values stored under a name that says they are a secret left out.
/// </summary>
internal static class QueryResultRows
{
    /// <summary>
    /// The longest text value a row keeps.
    /// </summary>
    public const int MaxValueLength = 2000;

    /// <summary>
    /// The size of the rows of one result, in characters of JSON, after which no further row is added.
    /// </summary>
    public const int MaxResultLength = 40000;

    /// <summary>
    /// What a value stored under a secret name is replaced with.
    /// </summary>
    public const string Redacted = "[redacted]";

    private const int MaxArrayLength = 50;

    private const int MaxDepth = 12;

    // A name that holds one of these says the value is a credential. The check ignores case, '_' and '-'.
    private static readonly string[] _secretNameParts =
    [
        "password",
        "passwd",
        "secret",
        "token",
        "apikey",
        "privatekey",
        "accesskey",
        "signingkey",
        "encryptionkey",
        "hashkey",
        "securitystamp",
        "connectionstring",
        "credential",
    ];

    private static readonly string[] _secretNames =
    [
        "pin",
        "pwd",
        "salt",
        "hash",
    ];

    /// <summary>
    /// Reads the <c>parameters</c> argument of a tool that runs a query, the way the query pages of the admin and the
    /// query API read their parameters.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <param name="parameters">The parameters; empty when the argument is not given.</param>
    /// <returns><see langword="false"/> when the argument is given but is not a JSON object.</returns>
    public static bool TryReadParameters(AIFunctionArguments arguments, out Dictionary<string, object> parameters)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        parameters = [];

        if (!AgentToolArguments.TryReadJsonObject(arguments, "parameters", out var given))
        {
            return false;
        }

        if (given is not null)
        {
            parameters = JConvert.DeserializeObject<Dictionary<string, object>>(given.ToJsonString()) ?? [];
        }

        return true;
    }

    /// <summary>
    /// Builds one page of the rows a query returned.
    /// </summary>
    /// <param name="items">The rows, documents or content items the query returned.</param>
    /// <param name="page">The page, starting at 1.</param>
    /// <param name="pageSize">The number of rows on a page.</param>
    /// <returns>The page.</returns>
    public static QueryResultPage CreatePage(IEnumerable<object> items, int page, int pageSize)
    {
        var all = items as IReadOnlyCollection<object> ?? items?.ToList() ?? [];
        var result = new QueryResultPage
        {
            Total = all.Count,
        };

        var length = 0;

        foreach (var item in all.Skip((page - 1) * pageSize).Take(pageSize))
        {
            var row = Describe(item, result);
            var rowLength = row.ToJsonString().Length;

            // The first row is always returned, so a page is never empty because of one large row.
            if (result.Items.Count > 0 && length + rowLength > MaxResultLength)
            {
                result.Truncated = true;

                break;
            }

            result.Items.Add(row);
            length += rowLength;
        }

        return result;
    }

    /// <summary>
    /// Whether a column or property name says its value is a credential.
    /// </summary>
    /// <param name="name">The name of the column or property.</param>
    /// <returns><see langword="true"/> when the value must not be returned.</returns>
    public static bool IsSecretName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        var normalized = name.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);

        return Array.Exists(_secretNames, secret => string.Equals(normalized, secret, StringComparison.OrdinalIgnoreCase)) ||
            Array.Exists(_secretNameParts, part => normalized.Contains(part, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Cuts a text to a length, never between the two halves of a character.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="length">The most characters to keep.</param>
    /// <returns>The text, cut when it is longer.</returns>
    public static string Cut(string text, int length)
    {
        if (text is null || text.Length <= length)
        {
            return text;
        }

        return char.IsHighSurrogate(text[length - 1])
            ? text.Substring(0, length - 1)
            : text.Substring(0, length);
    }

    private static JsonNode Describe(object item, QueryResultPage result)
    {
        return item switch
        {
            // A content item is summarized: its parts and fields are read with the content tools.
            ContentItem contentItem => new JsonObject
            {
                ["contentItemId"] = contentItem.ContentItemId,
                ["contentItemVersionId"] = contentItem.ContentItemVersionId,
                ["contentType"] = contentItem.ContentType,
                ["displayText"] = contentItem.DisplayText,
                ["published"] = contentItem.Published,
                ["latest"] = contentItem.Latest,
            },
            JsonObject row => Clean(row, result, 0),
            JsonNode node => new JsonObject
            {
                ["value"] = Clean(node, result, 0),
            },
            IDictionary<string, object> row => Clean(ToJsonObject(row), result, 0),
            _ => new JsonObject
            {
                ["value"] = Shorten(Convert.ToString(item, CultureInfo.InvariantCulture), result),
            },
        };
    }

    private static JsonObject ToJsonObject(IDictionary<string, object> row)
    {
        var jsonObject = new JsonObject();

        foreach (var (name, value) in row)
        {
            jsonObject[name] = value switch
            {
                null => null,
                JsonNode node => node.DeepClone(),
                string text => text,
                bool flag => flag,
                int number => number,
                long number => number,
                double number => number,
                decimal number => number,
                _ => Convert.ToString(value, CultureInfo.InvariantCulture),
            };
        }

        return jsonObject;
    }

    private static JsonNode Clean(JsonNode node, QueryResultPage result, int depth)
    {
        if (node is null)
        {
            return null;
        }

        if (depth > MaxDepth)
        {
            result.ValuesShortened = true;

            return "[nested too deep]";
        }

        if (node is JsonObject jsonObject)
        {
            var cleaned = new JsonObject();

            foreach (var (name, value) in jsonObject)
            {
                if (value is not null && IsSecretName(name))
                {
                    result.ValuesRedacted = true;
                    cleaned[name] = Redacted;
                }
                else
                {
                    cleaned[name] = Clean(value, result, depth + 1);
                }
            }

            return cleaned;
        }

        if (node is JsonArray jsonArray)
        {
            var cleaned = new JsonArray();

            foreach (var value in jsonArray.Take(MaxArrayLength))
            {
                cleaned.Add(Clean(value, result, depth + 1));
            }

            if (jsonArray.Count > MaxArrayLength)
            {
                result.ValuesShortened = true;
            }

            return cleaned;
        }

        if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text))
        {
            return CleanText(text, result, depth);
        }

        return node.DeepClone();
    }

    // A text value can hold a JSON document (the Content column of the Document table, for one). Its secrets are left
    // out before the text is shortened.
    private static JsonNode CleanText(string text, QueryResultPage result, int depth)
    {
        var trimmed = text.AsSpan().TrimStart();

        if (trimmed.Length > 1 && (trimmed[0] == '{' || trimmed[0] == '['))
        {
            try
            {
                var cleaned = Clean(JsonNode.Parse(text), result, depth + 1);

                if (cleaned is not null)
                {
                    return Shorten(cleaned.ToJsonString(), result);
                }
            }
            catch (JsonException)
            {
                // The text only looks like JSON; it is returned as text.
            }
        }

        return Shorten(text, result);
    }

    private static string Shorten(string text, QueryResultPage result)
    {
        if (text is null || text.Length <= MaxValueLength)
        {
            return text;
        }

        result.ValuesShortened = true;

        return $"{Cut(text, MaxValueLength)}... [shortened, {text.Length} characters in all]";
    }
}
