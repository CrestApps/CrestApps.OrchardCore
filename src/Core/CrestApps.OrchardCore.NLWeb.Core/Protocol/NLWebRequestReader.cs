using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.NLWeb.Models;

namespace CrestApps.OrchardCore.NLWeb.Protocol;

/// <summary>
/// Reads NLWeb requests of every protocol version into one <see cref="NLWebAskRequest"/>: the JSON body of version
/// 0.5 and later (<c>query</c>, <c>context</c>, <c>prefer</c> or <c>return_response</c>, <c>meta</c>), the flat JSON
/// body and the query string of the original version.
/// </summary>
public static class NLWebRequestReader
{
    /// <summary>
    /// The longest query text accepted.
    /// </summary>
    public const int MaxQueryLength = 2000;

    /// <summary>
    /// The most earlier queries kept from the context.
    /// </summary>
    public const int MaxPreviousQueries = 20;

    private static readonly HashSet<string> _knownQueryMembers = new(StringComparer.OrdinalIgnoreCase)
    {
        "text",
        "query",
        "site",
        "itemType",
        "item_type",
        "type",
        "decontextualized_query",
        "decontextualizedQuery",
    };

    /// <summary>
    /// Reads a JSON request body.
    /// </summary>
    /// <param name="body">The JSON body.</param>
    /// <param name="request">The request, when the body is valid.</param>
    /// <param name="error">The reason the body is not valid.</param>
    /// <returns><see langword="true"/> when the body is a valid request.</returns>
    public static bool TryRead(JsonObject body, out NLWebAskRequest request, out string error)
    {
        request = null;

        if (body is null)
        {
            error = "The request body must be a JSON object.";

            return false;
        }

        var result = new NLWebAskRequest();

        ReadQuery(body["query"], result.Query);

        // The original protocol sent every field at the top level of the body.
        result.Query.Site ??= ReadFirstString(body["site"]);
        result.Query.ItemType ??= ReadString(body["itemType"]) ?? ReadString(body["item_type"]);
        result.Query.DecontextualizedText ??= ReadString(body["decontextualized_query"]);

        if (body["context"] is JsonObject context)
        {
            ReadContext(context, result.Context);
        }

        AddStrings(result.Context.Previous, body["prev"], splitCommas: true);

        var prefer = body["prefer"] as JsonObject ?? body["return_response"] as JsonObject;

        if (prefer is not null)
        {
            ReadPreferences(prefer, result.Prefer);
        }

        result.Prefer.Streaming ??= ReadBoolean(body["streaming"]);
        result.Prefer.Mode ??= ReadString(body["mode"]) ?? ReadString(body["generate_mode"]);
        result.Prefer.MaxResults ??= ReadInteger(body["num_results"]);

        if (body["meta"] is JsonObject meta)
        {
            ReadMeta(meta, result.Meta);
        }

        result.Meta.QueryId ??= ReadString(body["query_id"]);

        return Validate(result, out request, out error);
    }

    /// <summary>
    /// Reads the query string of an original protocol request, such as
    /// <c>/ask?query=...&amp;site=...&amp;prev=...&amp;mode=list</c>.
    /// </summary>
    /// <param name="values">The query string values.</param>
    /// <param name="request">The request, when the values are valid.</param>
    /// <param name="error">The reason the values are not valid.</param>
    /// <returns><see langword="true"/> when the values are a valid request.</returns>
    public static bool TryReadQueryString(IEnumerable<KeyValuePair<string, string>> values, out NLWebAskRequest request, out string error)
    {
        ArgumentNullException.ThrowIfNull(values);

        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            lookup.TryAdd(value.Key, value.Value);
        }

        var result = new NLWebAskRequest
        {
            IsLegacy = true,
        };

        result.Query.Text = lookup.GetValueOrDefault("query");
        result.Query.Site = NullIfEmpty(lookup.GetValueOrDefault("site"));
        result.Query.ItemType = NullIfEmpty(lookup.GetValueOrDefault("item_type"));
        result.Query.DecontextualizedText = NullIfEmpty(lookup.GetValueOrDefault("decontextualized_query"));

        if (lookup.TryGetValue("prev", out var previous))
        {
            AddStrings(result.Context.Previous, JsonValue.Create(previous), splitCommas: true);
        }

        result.Prefer.Mode = NullIfEmpty(lookup.GetValueOrDefault("mode")) ?? NullIfEmpty(lookup.GetValueOrDefault("generate_mode"));
        result.Prefer.Streaming = ParseBoolean(lookup.GetValueOrDefault("streaming"));

        if (int.TryParse(lookup.GetValueOrDefault("num_results"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxResults))
        {
            result.Prefer.MaxResults = maxResults;
        }

        result.Meta.QueryId = NullIfEmpty(lookup.GetValueOrDefault("query_id"));
        result.Meta.ConversationId = NullIfEmpty(lookup.GetValueOrDefault("conversation_id"));

        return Validate(result, out request, out error);
    }

    private static bool Validate(NLWebAskRequest result, out NLWebAskRequest request, out string error)
    {
        request = null;
        result.Query.Text = result.Query.Text?.Trim();

        if (string.IsNullOrEmpty(result.Query.Text))
        {
            error = "The query text is required.";

            return false;
        }

        if (result.Query.Text.Length > MaxQueryLength ||
            result.Query.DecontextualizedText?.Length > MaxQueryLength ||
            result.Context.Text?.Length > MaxQueryLength * 4)
        {
            error = $"The query must not be longer than {MaxQueryLength} characters.";

            return false;
        }

        // Keep the most recent queries when the conversation is long.
        while (result.Context.Previous.Count > MaxPreviousQueries)
        {
            result.Context.Previous.RemoveAt(0);
        }

        for (var i = result.Context.Previous.Count - 1; i >= 0; i--)
        {
            if (result.Context.Previous[i].Length > MaxQueryLength)
            {
                result.Context.Previous.RemoveAt(i);
            }
        }

        result.Prefer.Mode = result.Prefer.Mode?.Trim().ToLowerInvariant();
        error = null;
        request = result;

        return true;
    }

    private static void ReadQuery(JsonNode node, NLWebQuery query)
    {
        if (node is JsonValue)
        {
            query.Text = ReadString(node);

            return;
        }

        if (node is not JsonObject obj)
        {
            return;
        }

        query.Text = ReadString(obj["text"]) ?? ReadString(obj["query"]);
        query.Site = ReadFirstString(obj["site"]);
        query.ItemType = ReadString(obj["itemType"]) ?? ReadString(obj["item_type"]) ?? ReadString(obj["type"]);
        query.DecontextualizedText = ReadString(obj["decontextualized_query"]) ?? ReadString(obj["decontextualizedQuery"]);

        foreach (var member in obj)
        {
            if (!_knownQueryMembers.Contains(member.Key) && member.Value is not null)
            {
                query.Filters[member.Key] = member.Value.DeepClone();
            }
        }
    }

    private static void ReadContext(JsonObject context, NLWebContext result)
    {
        result.Type = ReadString(context["@type"]);
        result.Text = ReadString(context["text"]);
        AddStrings(result.Previous, context["prev"], splitCommas: false);
        AddStrings(result.Memory, context["memory"], splitCommas: false);
    }

    private static void ReadPreferences(JsonObject prefer, NLWebPreferences result)
    {
        result.Streaming = ReadBoolean(prefer["streaming"]);
        result.ResponseFormat = ReadString(prefer["response_format"]) ?? ReadString(prefer["format"]);
        result.Mode = ReadString(prefer["mode"]);
        result.Language = ReadString(prefer["accept-language"]) ?? ReadString(prefer["lang"]);
        result.UserAgent = ReadString(prefer["user-agent"]) ?? ReadString(prefer["client_type"]);
        result.MaxResults = ReadInteger(prefer["num_results"]) ?? ReadInteger(prefer["max_results"]);
    }

    private static void ReadMeta(JsonObject meta, NLWebMeta result)
    {
        result.Version = ReadString(meta["version"]) ?? ReadString(meta["api_version"]);
        result.QueryId = ReadString(meta["query_id"]);

        if (meta["session_context"] is JsonObject sessionContext)
        {
            result.SessionContext = (JsonObject)sessionContext.DeepClone();
            result.ConversationId = ReadString(sessionContext["conversation_id"]);
        }

        result.ConversationId ??= ReadString(meta["conversation_id"]);
    }

    private static string ReadString(JsonNode node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<string>(out var text))
        {
            return NullIfEmpty(text);
        }

        return NullIfEmpty(value.ToJsonString().Trim('"'));
    }

    private static string ReadFirstString(JsonNode node)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                var text = ReadString(item);

                if (text is not null)
                {
                    return text;
                }
            }

            return null;
        }

        return ReadString(node);
    }

    private static bool? ReadBoolean(JsonNode node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<bool>(out var flag))
        {
            return flag;
        }

        return ParseBoolean(ReadString(node));
    }

    private static int? ReadInteger(JsonNode node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<int>(out var number))
        {
            return number;
        }

        return int.TryParse(ReadString(node), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static bool? ParseBoolean(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "yes" => true,
            "false" or "0" or "no" => false,
            _ => null,
        };
    }

    private static void AddStrings(IList<string> target, JsonNode node, bool splitCommas)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                var text = ReadString(item);

                if (text is not null)
                {
                    target.Add(text);
                }
            }

            return;
        }

        var value = ReadString(node);

        if (value is null)
        {
            return;
        }

        if (!splitCommas)
        {
            target.Add(value);

            return;
        }

        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            target.Add(part);
        }
    }

    private static string NullIfEmpty(string text)
        => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
