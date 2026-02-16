using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Elastic.Clients.Elasticsearch;

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
        string indexName,
        string titleFieldName,
        string contentFieldName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string searchAfterValue = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var response = searchAfterValue == null
                ? await _elasticClient.SearchAsync<JsonObject>(s => s
                    .Indices(indexName)
                    .Size(BatchSize)
                    .Sort(sort => sort.Field("_doc"))
                , cancellationToken)
                : await _elasticClient.SearchAsync<JsonObject>(s => s
                    .Indices(indexName)
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

                yield return new KeyValuePair<string, SourceDocument>(
                    hit.Id, ExtractDocument(hit.Source, titleFieldName, contentFieldName));
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
            title = titleNode?.ToString();
        }

        if (!string.IsNullOrEmpty(contentFieldName) && source.TryGetPropertyValue(contentFieldName, out var contentNode))
        {
            content = contentNode?.ToString();
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

        return new SourceDocument
        {
            Title = title,
            Content = content,
        };
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
