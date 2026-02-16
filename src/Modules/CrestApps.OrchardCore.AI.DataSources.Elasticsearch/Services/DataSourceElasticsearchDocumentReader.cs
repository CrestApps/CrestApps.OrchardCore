using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Elastic.Clients.Elasticsearch;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;

/// <summary>
/// Reads documents from an Elasticsearch source index.
/// </summary>
internal sealed class DataSourceElasticsearchDocumentReader : IDataSourceDocumentReader
{
    private const int BatchSize = 1000;

    private readonly ElasticsearchClient _elasticClient;

    public DataSourceElasticsearchDocumentReader(ElasticsearchClient elasticClient)
    {
        _elasticClient = elasticClient;
    }

    public async IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadAsync(
        IndexProfile indexProfile,
        string keyFieldName,
        string titleFieldName,
        string contentFieldName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (indexProfile == null)
        {
            yield break;
        }

        string searchAfterValue = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var response = searchAfterValue == null
                ? await _elasticClient.SearchAsync<JsonObject>(s => s
                    .Indices(indexProfile.IndexFullName)
                    .Size(BatchSize)
                    .Sort(sort => sort.Field("_doc"))
                , cancellationToken)
                : await _elasticClient.SearchAsync<JsonObject>(s => s
                    .Indices(indexProfile.IndexFullName)
                    .Size(BatchSize)
                    .Sort(sort => sort.Field("_doc"))
                    .SearchAfter([searchAfterValue])
                , cancellationToken);

            if (!response.IsValidResponse || response.Hits.Count == 0)
            {
                break;
            }

            foreach (var hit in response.Hits)
            {
                if (hit.Id == null || hit.Source == null)
                {
                    continue;
                }

                var key = hit.Id;

                if (!string.IsNullOrEmpty(keyFieldName) &&
                    hit.Source.TryGetPropertyValue(keyFieldName, out var keyNode) &&
                    keyNode != null)
                {
                    key = GetStringValue(keyNode) ?? key;
                }

                yield return new KeyValuePair<string, SourceDocument>(
                    key, ExtractDocument(hit.Source, titleFieldName, contentFieldName));
            }

            var lastSort = response.Hits.Last().Sort;
            searchAfterValue = lastSort != null && lastSort.Count > 0
                ? lastSort.First().ToString()
                : null;

            if (string.IsNullOrEmpty(searchAfterValue) || response.Hits.Count < BatchSize)
            {
                break;
            }
        }
    }

    private static SourceDocument ExtractDocument(JsonObject source, string titleFieldName, string contentFieldName)
    {
        string title = null;
        string content = null;

        if (!string.IsNullOrEmpty(titleFieldName) && source.TryGetPropertyValue(titleFieldName, out var titleNode))
        {
            title = GetStringValue(titleNode);
        }

        if (!string.IsNullOrEmpty(contentFieldName) && source.TryGetPropertyValue(contentFieldName, out var contentNode))
        {
            content = GetStringValue(contentNode);
        }
        else
        {
            // Fallback: use the full document JSON as content.
            content = source.ToJsonString();
        }

        if (string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(content))
        {
            title = ExtractTitleFromContent(content);
        }

        // Populate all source fields for filter field propagation.
        var fields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in source)
        {
            fields[prop.Key] = GetRawValue(prop.Value);
        }

        return new SourceDocument
        {
            Title = title,
            Content = content,
            Fields = fields,
        };
    }

    private static string GetStringValue(JsonNode node)
    {
        if (node == null)
        {
            return null;
        }

        if (node is JsonValue jsonValue)
        {
            return jsonValue.ToString();
        }

        // For arrays or objects, return the JSON representation.
        return node.ToJsonString();
    }

    private static object GetRawValue(JsonNode node)
    {
        if (node == null)
        {
            return null;
        }

        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<string>(out var stringVal))
            {
                return stringVal;
            }

            if (jsonValue.TryGetValue<long>(out var longVal))
            {
                return longVal;
            }

            if (jsonValue.TryGetValue<double>(out var doubleVal))
            {
                return doubleVal;
            }

            if (jsonValue.TryGetValue<bool>(out var boolVal))
            {
                return boolVal;
            }

            if (jsonValue.TryGetValue<DateTime>(out var dateVal))
            {
                return dateVal;
            }

            return jsonValue.ToString();
        }

        // For arrays or objects, return as string.
        return node.ToJsonString();
    }

    private static string ExtractTitleFromContent(string content)
    {
        var firstLine = content.AsSpan();
        var newlineIndex = firstLine.IndexOfAny('\r', '\n');

        if (newlineIndex > 0)
        {
            firstLine = firstLine[..newlineIndex];
        }

        if (firstLine.Length > 200)
        {
            firstLine = firstLine[..200];
        }

        return firstLine.ToString().Trim();
    }
}
