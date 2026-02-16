using System.Runtime.CompilerServices;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;

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
        string indexName,
        string titleFieldName,
        string contentFieldName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var searchClient = _searchIndexClient.GetSearchClient(indexName);

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

            // The first key in the document is typically the document key.
            var key = doc.Keys.FirstOrDefault();

            if (key != null && doc.TryGetValue(key, out var keyValue))
            {
                var documentKey = keyValue?.ToString() ?? string.Empty;
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
