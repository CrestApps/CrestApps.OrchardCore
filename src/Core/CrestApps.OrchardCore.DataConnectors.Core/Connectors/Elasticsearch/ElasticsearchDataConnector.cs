using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.DataConnectors.Http;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.DataConnectors.Connectors.Elasticsearch;

/// <summary>
/// How a data set reads an Elasticsearch index. The index, alias or pattern is the data set's resource.
/// </summary>
public sealed class ElasticsearchDataSetSettings
{
    /// <summary>
    /// Gets or sets a query clause in the query DSL, such as <c>{ "term": { "status": "active" } }</c>, or
    /// <see langword="null"/> for every document.
    /// </summary>
    public string Query { get; set; }

    /// <summary>
    /// Gets or sets the minutes the cluster keeps a scroll open between two pages.
    /// </summary>
    public int ScrollMinutes { get; set; } = 2;
}

/// <summary>
/// Reads Elasticsearch and OpenSearch indices through their REST API: lists indices and aliases, maps index mappings
/// to fields, and scrolls through the matching documents. Range conditions on number and date fields, and the
/// watermark of an incremental import, are applied by the cluster.
/// </summary>
internal sealed class ElasticsearchDataConnector : IDataConnector
{
    /// <summary>
    /// The source field that holds a document's identifier.
    /// </summary>
    public const string IdField = "_id";

    private readonly DataConnectorHttpClient _httpClient;

    public ElasticsearchDataConnector(DataConnectorHttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public static string Name => "Elasticsearch";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<ElasticsearchDataConnector>("Elasticsearch");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<ElasticsearchDataConnector>("Reads the documents of Elasticsearch or OpenSearch indices and aliases, with Basic, API key or bearer token authentication.");

    public static DataConnectorCapabilities Capabilities =>
        DataConnectorCapabilities.Live |
        DataConnectorCapabilities.Import |
        DataConnectorCapabilities.IncrementalImport |
        DataConnectorCapabilities.Discovery |
        DataConnectorCapabilities.ConditionPushdown;

    public async Task<DataConnectorTestResult> TestAsync(DataConnectorContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            using var document = await GetJsonAsync(context, string.Empty, cancellationToken);
            var root = document.RootElement;
            var version = JsonRecords.SelectText(root, "version.number");
            var distribution = JsonRecords.SelectText(root, "version.distribution") ?? "Elasticsearch";
            var cluster = JsonRecords.SelectText(root, "cluster_name");

            return DataConnectorTestResult.Success($"Connected to {distribution} {version} (cluster '{cluster}').");
        }
        catch (DataConnectorException exception)
        {
            return DataConnectorTestResult.Failure(exception.Message);
        }
    }

    public async Task<IReadOnlyList<DataConnectorResource>> DiscoverAsync(DataConnectorContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var resources = new List<DataConnectorResource>();

        using (var indices = await GetJsonAsync(context, "_cat/indices?format=json&h=index,docs.count&s=index", cancellationToken))
        {
            foreach (var index in Items(indices.RootElement))
            {
                var name = JsonRecords.SelectText(index, "index");

                if (!string.IsNullOrEmpty(name) && !name.StartsWith('.'))
                {
                    var count = index.TryGetProperty("docs.count", out var docs) && docs.ValueKind == JsonValueKind.String ? docs.GetString() : null;

                    resources.Add(new DataConnectorResource(name, string.IsNullOrEmpty(count) ? name : $"{name} ({count} documents)", "Index"));
                }
            }
        }

        using (var aliases = await GetJsonAsync(context, "_cat/aliases?format=json&h=alias&s=alias", cancellationToken))
        {
            foreach (var alias in Items(aliases.RootElement))
            {
                var name = JsonRecords.SelectText(alias, "alias");

                if (!string.IsNullOrEmpty(name) && !name.StartsWith('.') && !resources.Any(resource => resource.Name == name))
                {
                    resources.Add(new DataConnectorResource(name, name, "Alias"));
                }
            }
        }

        return resources;
    }

    public async Task<IReadOnlyList<DataField>> DescribeAsync(DataConnectorReadContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var mapping = await GetMappingAsync(context, cancellationToken);
        var fields = new List<DataField>
        {
            new(IdField, "Document id", DataFieldType.Text) { IsIdentifier = true },
        };

        fields.AddRange(mapping.Select(entry => new DataField(entry.Key, entry.Key, entry.Value.Type)));

        return fields;
    }

    public async IAsyncEnumerable<DataBatch> ReadAsync(DataConnectorReadContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = context.DataSet.GetOrCreate<ElasticsearchDataSetSettings>();
        var mapping = await GetMappingAsync(context, cancellationToken);
        var names = context.Fields.Count > 0
            ? context.Fields.ToArray()
            : [IdField, .. mapping.Keys];
        var fields = names.Select(name => new DataField(name, name, DataFieldType.Text)).ToArray();
        var batchSize = Math.Clamp(context.BatchSize > 0 ? context.BatchSize : DataSourceQuery.DefaultBatchSize, 1, 10_000);
        var scroll = $"{Math.Clamp(settings.ScrollMinutes, 1, 60)}m";
        var body = BuildSearch(context, settings, mapping, names, batchSize);
        var index = Uri.EscapeDataString(RequireIndex(context));
        var remaining = context.MaxRows > 0 ? context.MaxRows : long.MaxValue;
        string scrollId = null;
        var any = false;

        try
        {
            using var first = await PostJsonAsync(context, $"{index}/_search?scroll={scroll}", body, cancellationToken);
            var page = ReadHits(first.RootElement, names, ref remaining);
            scrollId = JsonRecords.SelectText(first.RootElement, "_scroll_id");

            while (page.Count > 0)
            {
                any = true;

                yield return new DataBatch(fields, page);

                if (remaining <= 0 || string.IsNullOrEmpty(scrollId))
                {
                    break;
                }

                var next = new JsonObject { ["scroll"] = scroll, ["scroll_id"] = scrollId }.ToJsonString();

                using var document = await PostJsonAsync(context, "_search/scroll", next, cancellationToken);
                page = ReadHits(document.RootElement, names, ref remaining);
                scrollId = JsonRecords.SelectText(document.RootElement, "_scroll_id") ?? scrollId;
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(scrollId))
            {
                await ClearScrollAsync(context, scrollId);
            }
        }

        if (!any)
        {
            yield return new DataBatch(fields, []);
        }
    }

    /// <summary>
    /// Builds the body of the first search: the data set's query, the conditions the cluster can apply, the watermark,
    /// the source fields, and an order that scrolls fastest.
    /// </summary>
    internal static string BuildSearch(
        DataConnectorReadContext context,
        ElasticsearchDataSetSettings settings,
        IReadOnlyDictionary<string, ElasticsearchField> mapping,
        IReadOnlyList<string> names,
        int batchSize)
    {
        JsonNode query;

        try
        {
            query = string.IsNullOrWhiteSpace(settings.Query) ? new JsonObject { ["match_all"] = new JsonObject() } : JsonNode.Parse(settings.Query);
        }
        catch (JsonException exception)
        {
            throw new DataConnectorException($"The data set's query isn't valid JSON: {exception.Message}", exception);
        }

        if (query is not JsonObject)
        {
            throw new DataConnectorException("The data set's query must be a JSON object, such as { \"match_all\": {} }.");
        }

        var filters = new JsonArray();

        foreach (var condition in context.Conditions)
        {
            if (mapping.TryGetValue(condition.Field ?? string.Empty, out var field) && ToRange(condition, field) is { } range)
            {
                filters.Add(range);
            }
        }

        if (!string.IsNullOrEmpty(context.WatermarkField) && context.Watermark is not null)
        {
            if (!mapping.TryGetValue(context.WatermarkField, out var watermarkField) || !watermarkField.IsRangeable)
            {
                throw new DataConnectorException($"The incremental field '{context.WatermarkField}' must be a number or date field of the index.");
            }

            filters.Add(Range(context.WatermarkField, "gt", context.Watermark));
        }

        var search = new JsonObject
        {
            ["size"] = batchSize,
            ["sort"] = new JsonArray("_doc"),
            ["query"] = new JsonObject
            {
                ["bool"] = new JsonObject
                {
                    ["must"] = new JsonArray(query),
                    ["filter"] = filters,
                },
            },
        };

        var sourceFields = names.Where(name => name != IdField).ToArray();

        search["_source"] = sourceFields.Length == 0
            ? false
            : new JsonArray([.. sourceFields.Select(name => (JsonNode)JsonValue.Create(name))]);

        return search.ToJsonString();
    }

    /// <summary>
    /// Maps an index mapping's properties to source fields, by dotted path.
    /// </summary>
    internal static Dictionary<string, ElasticsearchField> ParseMapping(JsonElement root)
    {
        var fields = new Dictionary<string, ElasticsearchField>(StringComparer.Ordinal);

        if (root.ValueKind != JsonValueKind.Object)
        {
            return fields;
        }

        // The answer has one entry per index the name matched; their fields are merged.
        foreach (var index in root.EnumerateObject())
        {
            if (index.Value.TryGetProperty("mappings", out var mappings))
            {
                // Before Elasticsearch 7 the properties sat under a document type.
                var properties = mappings.TryGetProperty("properties", out var direct)
                    ? direct
                    : mappings.ValueKind == JsonValueKind.Object && mappings.EnumerateObject().FirstOrDefault().Value is { ValueKind: JsonValueKind.Object } typed && typed.TryGetProperty("properties", out var legacy)
                        ? legacy
                        : default;

                AddProperties(properties, null, fields);
            }
        }

        return fields;
    }

    private static void AddProperties(JsonElement properties, string prefix, Dictionary<string, ElasticsearchField> fields)
    {
        if (properties.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in properties.EnumerateObject())
        {
            var name = prefix is null ? property.Name : $"{prefix}.{property.Name}";
            var type = property.Value.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;

            if (property.Value.TryGetProperty("properties", out var children) && type is null or "object")
            {
                AddProperties(children, name, fields);

                continue;
            }

            if (!fields.ContainsKey(name))
            {
                fields[name] = new ElasticsearchField(type, ToFieldType(type));
            }
        }
    }

    private static JsonElement[] Items(JsonElement element)
        => element.ValueKind == JsonValueKind.Array ? [.. element.EnumerateArray()] : [];

    private static DataFieldType ToFieldType(string type)
        => type switch
        {
            "long" or "integer" or "short" or "byte" or "unsigned_long" => DataFieldType.Integer,
            "double" or "float" or "half_float" or "scaled_float" => DataFieldType.Decimal,
            "boolean" => DataFieldType.Boolean,
            "date" or "date_nanos" => DataFieldType.DateTime,
            _ => DataFieldType.Text,
        };

    private static JsonObject ToRange(DataCondition condition, ElasticsearchField field)
    {
        if (!field.IsRangeable || condition.Values is not { Count: > 0 } values || values[0] is null)
        {
            return null;
        }

        return condition.Operator switch
        {
            DataFilterOperator.GreaterThan => Range(condition.Field, "gt", values[0]),
            DataFilterOperator.GreaterThanOrEqual => Range(condition.Field, "gte", values[0]),
            DataFilterOperator.LessThan => Range(condition.Field, "lt", values[0]),
            DataFilterOperator.LessThanOrEqual => Range(condition.Field, "lte", values[0]),
            DataFilterOperator.Between when values.Count > 1 && values[1] is not null => Range(condition.Field, "gte", values[0], "lte", values[1]),
            _ => null,
        };
    }

    private static JsonObject Range(string field, string op, object value, string secondOp = null, object secondValue = null)
    {
        var bounds = new JsonObject { [op] = ToJson(value) };

        if (secondOp is not null)
        {
            bounds[secondOp] = ToJson(secondValue);
        }

        return new JsonObject { ["range"] = new JsonObject { [field] = bounds } };
    }

    private static JsonValue ToJson(object value)
        => value switch
        {
            DateTime date => JsonValue.Create(date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)),
            DateTimeOffset date => JsonValue.Create(date.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)),
            long number => JsonValue.Create(number),
            int number => JsonValue.Create(number),
            decimal number => JsonValue.Create(number),
            double number => JsonValue.Create(number),
            _ => JsonValue.Create(DataValues.ToText(value)),
        };

    private static List<object[]> ReadHits(JsonElement root, IReadOnlyList<string> names, ref long remaining)
    {
        var rows = new List<object[]>();
        var hits = JsonRecords.Select(root, "hits.hits");

        if (hits is not { ValueKind: JsonValueKind.Array })
        {
            return rows;
        }

        foreach (var hit in hits.Value.EnumerateArray())
        {
            if (remaining <= 0)
            {
                break;
            }

            var source = hit.TryGetProperty("_source", out var element) && element.ValueKind == JsonValueKind.Object
                ? JsonRecords.Flatten(element)
                : [];
            var id = JsonRecords.SelectText(hit, IdField);

            rows.Add([.. names.Select(name => name == IdField ? id : source.GetValueOrDefault(name))]);
            remaining--;
        }

        return rows;
    }

    private async Task<Dictionary<string, ElasticsearchField>> GetMappingAsync(DataConnectorReadContext context, CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(context, $"{Uri.EscapeDataString(RequireIndex(context))}/_mapping", cancellationToken);

        return ParseMapping(document.RootElement);
    }

    private static string RequireIndex(DataConnectorReadContext context)
    {
        var index = context.DataSet.Resource?.Trim();

        if (string.IsNullOrEmpty(index))
        {
            throw new DataConnectorException("The data set names no index.");
        }

        return index;
    }

    private async Task<JsonDocument> GetJsonAsync(DataConnectorContext context, string path, CancellationToken cancellationToken)
    {
        var settings = context.Connection.GetOrCreate<HttpConnectionSettings>();
        var uri = DataConnectorHttpClient.Resolve(settings, path);

        using var response = await _httpClient.SendAsync(settings, context.Secrets, DataConnectorHttpClient.CacheKey(context.Connection), HttpMethod.Get, uri, null, cancellationToken);

        return await _httpClient.ReadJsonAsync(response, cancellationToken);
    }

    private async Task<JsonDocument> PostJsonAsync(DataConnectorContext context, string path, string body, CancellationToken cancellationToken)
    {
        var settings = context.Connection.GetOrCreate<HttpConnectionSettings>();
        var uri = DataConnectorHttpClient.Resolve(settings, path);

        using var response = await _httpClient.SendAsync(settings, context.Secrets, DataConnectorHttpClient.CacheKey(context.Connection), HttpMethod.Post, uri, () => new StringContent(body, Encoding.UTF8, "application/json"), cancellationToken);

        return await _httpClient.ReadJsonAsync(response, cancellationToken);
    }

    private async Task ClearScrollAsync(DataConnectorContext context, string scrollId)
    {
        try
        {
            var settings = context.Connection.GetOrCreate<HttpConnectionSettings>();
            var uri = DataConnectorHttpClient.Resolve(settings, "_search/scroll");
            var body = new JsonObject { ["scroll_id"] = scrollId }.ToJsonString();

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var response = await _httpClient.SendAsync(settings, context.Secrets, DataConnectorHttpClient.CacheKey(context.Connection), HttpMethod.Delete, uri, () => new StringContent(body, Encoding.UTF8, "application/json"), timeout.Token, ensureSuccess: false);
        }
        catch (Exception exception) when (exception is DataConnectorException or OperationCanceledException)
        {
            // An open scroll expires by itself after its keep-alive.
        }
    }
}

/// <summary>
/// A field of an index mapping.
/// </summary>
/// <param name="MappingType">The mapping type, such as <c>keyword</c> or <c>date</c>.</param>
/// <param name="Type">The data field type values are read as.</param>
internal sealed record ElasticsearchField(string MappingType, DataFieldType Type)
{
    /// <summary>
    /// Gets a value indicating whether a range query compares the field like a consumer does.
    /// </summary>
    public bool IsRangeable => Type is DataFieldType.Integer or DataFieldType.Decimal or DataFieldType.DateTime;
}
