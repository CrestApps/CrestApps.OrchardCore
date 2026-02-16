using System.Runtime.CompilerServices;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;

/// <summary>
/// Reads documents from an Azure AI Search source index.
/// </summary>
internal sealed class DataSourceAzureAISearchDocumentReader : IDataSourceDocumentReader
{
    private const int BatchSize = 1000;

    private readonly SearchIndexClient _searchIndexClient;

    public DataSourceAzureAISearchDocumentReader(SearchIndexClient searchIndexClient)
    {
        _searchIndexClient = searchIndexClient;
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

        var searchClient = _searchIndexClient.GetSearchClient(indexProfile.IndexFullName);

        var searchOptions = new SearchOptions
        {
            Size = BatchSize,
            Select = { "*" },
        };

        var response = await searchClient.SearchAsync<SearchDocument>(
            searchText: "*",
            searchOptions,
            cancellationToken);

        await foreach (var searchResult in response.Value.GetResultsAsync())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var doc = searchResult.Document;

            // Use the configured key field, or fall back to the first field (typically the document key).
            string documentKey = null;

            if (!string.IsNullOrEmpty(keyFieldName) && doc.TryGetValue(keyFieldName, out var keyValue))
            {
                documentKey = keyValue?.ToString();
            }

            if (string.IsNullOrEmpty(documentKey))
            {
                var firstKey = doc.Keys.FirstOrDefault();

                if (firstKey != null && doc.TryGetValue(firstKey, out var firstKeyValue))
                {
                    documentKey = firstKeyValue?.ToString() ?? string.Empty;
                }
            }

            if (!string.IsNullOrEmpty(documentKey))
            {
                yield return new KeyValuePair<string, SourceDocument>(
                    documentKey, ExtractDocument(doc, titleFieldName, contentFieldName));
            }
        }
    }

    private static SourceDocument ExtractDocument(SearchDocument doc, string titleFieldName, string contentFieldName)
    {
        string title = null;
        string content;

        if (!string.IsNullOrEmpty(titleFieldName) && doc.TryGetValue(titleFieldName, out var titleValue))
        {
            title = titleValue?.ToString();
        }

        if (!string.IsNullOrEmpty(contentFieldName) && doc.TryGetValue(contentFieldName, out var contentValue))
        {
            content = contentValue?.ToString();
        }
        else
        {
            // Fallback: serialize the full document as content.
            content = System.Text.Json.JsonSerializer.Serialize(doc);
        }

        if (string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(content))
        {
            title = ExtractTitleFromContent(content);
        }

        // Populate all source fields for filter field propagation.
        var fields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in doc)
        {
            fields[kvp.Key] = kvp.Value;
        }

        return new SourceDocument
        {
            Title = title,
            Content = content,
            Fields = fields,
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
