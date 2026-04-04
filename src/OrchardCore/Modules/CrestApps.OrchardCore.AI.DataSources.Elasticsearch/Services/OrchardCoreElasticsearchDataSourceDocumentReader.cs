using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Infrastructure.Indexing.DataSources;
using CrestApps.Infrastructure.Indexing.Models;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;

internal sealed class OrchardCoreElasticsearchDataSourceDocumentReader : IDataSourceDocumentReader
{
    private const int BatchSize = 1000;

    private readonly ElasticsearchClient _elasticClient;

    public OrchardCoreElasticsearchDataSourceDocumentReader(ElasticsearchClient elasticClient)
    {
        _elasticClient = elasticClient;
    }

    public async IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadAsync(
        IIndexProfileInfo indexProfile,
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
                ? await _elasticClient.SearchAsync<JsonObject>(
                    s => s.Indices(indexProfile.IndexFullName).Size(BatchSize).Sort(sort => sort.Field("_doc")),
                    cancellationToken)
                : await _elasticClient.SearchAsync<JsonObject>(
                    s => s.Indices(indexProfile.IndexFullName).Size(BatchSize).Sort(sort => sort.Field("_doc")).SearchAfter([searchAfterValue]),
                    cancellationToken);

            if (!response.IsValidResponse || response.Hits.Count == 0)
            {
                yield break;
            }

            foreach (var hit in response.Hits)
            {
                if (hit.Id == null || hit.Source == null)
                {
                    continue;
                }

                var key = hit.Id;

                if (!string.IsNullOrWhiteSpace(keyFieldName))
                {
                    var keyNode = ResolveFieldValue(hit.Source, keyFieldName);

                    if (keyNode != null)
                    {
                        key = GetStringValue(keyNode) ?? key;
                    }
                }

                yield return new KeyValuePair<string, SourceDocument>(
                    key,
                    ExtractDocument(hit.Source, titleFieldName, contentFieldName));
            }

            var lastSort = response.Hits.Last().Sort;
            searchAfterValue = lastSort != null && lastSort.Count > 0 ? lastSort.First().ToString() : null;

            if (string.IsNullOrEmpty(searchAfterValue) || response.Hits.Count < BatchSize)
            {
                yield break;
            }
        }
    }

    public async IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadByIdsAsync(
        IIndexProfileInfo indexProfile,
        IEnumerable<string> documentIds,
        string keyFieldName,
        string titleFieldName,
        string contentFieldName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (indexProfile == null || documentIds == null)
        {
            yield break;
        }

        var ids = documentIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();

        if (ids.Count == 0)
        {
            yield break;
        }

        var response = await _elasticClient.SearchAsync<JsonObject>(
            s => s.Indices(indexProfile.IndexFullName).Query(q => q.Ids(query => query.Values(new Ids(ids)))).Size(ids.Count),
            cancellationToken);

        if (!response.IsValidResponse || response.Hits == null)
        {
            yield break;
        }

        foreach (var hit in response.Hits)
        {
            if (hit.Source == null)
            {
                continue;
            }

            var key = hit.Id;

            if (!string.IsNullOrWhiteSpace(keyFieldName))
            {
                var keyNode = ResolveFieldValue(hit.Source, keyFieldName);

                if (keyNode != null)
                {
                    key = GetStringValue(keyNode) ?? key;
                }
            }

            yield return new KeyValuePair<string, SourceDocument>(
                key,
                ExtractDocument(hit.Source, titleFieldName, contentFieldName));
        }
    }

    private static SourceDocument ExtractDocument(JsonObject source, string titleFieldName, string contentFieldName)
    {
        var title = !string.IsNullOrWhiteSpace(titleFieldName) ? GetStringValue(ResolveFieldValue(source, titleFieldName)) : null;
        var content = !string.IsNullOrWhiteSpace(contentFieldName) ? GetStringValue(ResolveFieldValue(source, contentFieldName)) : null;

        if (string.IsNullOrEmpty(content))
        {
            content = source.ToJsonString();
        }

        if (string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(content))
        {
            title = ExtractTitleFromContent(content);
        }

        var fields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in source)
        {
            fields[property.Key] = GetRawValue(property.Value);
        }

        return new SourceDocument
        {
            Title = title,
            Content = content,
            Fields = fields,
        };
    }

    private static JsonNode ResolveFieldValue(JsonObject source, string fieldPath)
    {
        if (source == null || string.IsNullOrEmpty(fieldPath))
        {
            return null;
        }

        if (source.TryGetPropertyValue(fieldPath, out var directNode))
        {
            return directNode;
        }

        if (!fieldPath.Contains('.'))
        {
            return null;
        }

        JsonNode current = source;

        foreach (var segment in fieldPath.Split('.'))
        {
            if (current is not JsonObject obj || !obj.TryGetPropertyValue(segment, out var next))
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    private static string GetStringValue(JsonNode node)
    {
        if (node == null)
        {
            return null;
        }

        return node is JsonValue jsonValue ? jsonValue.ToString() : node.ToJsonString();
    }

    private static object GetRawValue(JsonNode node)
    {
        if (node == null)
        {
            return null;
        }

        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<string>(out var stringValue))
            {
                return stringValue;
            }

            if (jsonValue.TryGetValue<long>(out var longValue))
            {
                return longValue;
            }

            if (jsonValue.TryGetValue<double>(out var doubleValue))
            {
                return doubleValue;
            }

            if (jsonValue.TryGetValue<bool>(out var boolValue))
            {
                return boolValue;
            }

            if (jsonValue.TryGetValue<DateTime>(out var dateValue))
            {
                return dateValue;
            }

            return jsonValue.ToString();
        }

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
