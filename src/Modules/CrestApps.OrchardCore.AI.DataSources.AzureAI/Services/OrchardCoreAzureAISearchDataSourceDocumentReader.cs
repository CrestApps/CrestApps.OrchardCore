using System.Runtime.CompilerServices;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.DataSources;
using CrestApps.Core.Infrastructure.Indexing.Models;
using OrchardCore.AzureAI.Services;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;

internal sealed class OrchardCoreAzureAISearchDataSourceDocumentReader : IDataSourceDocumentReader
{
    private const int BatchSize = 1000;

    private readonly AzureAIClientFactory _clientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardCoreAzureAISearchDataSourceDocumentReader"/> class.
    /// </summary>
    /// <param name="clientFactory">The client factory.</param>
    public OrchardCoreAzureAISearchDataSourceDocumentReader(AzureAIClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    /// <summary>
    /// Performs the i async enumerable operation.
    /// </summary>
    /// <param name="indexProfile">The index profile.</param>
    /// <param name="keyFieldName">The key field name.</param>
    /// <param name="titleFieldName">The title field name.</param>
    /// <param name="contentFieldName">The content field name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
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

        var searchClient = _clientFactory.CreateSearchClient(indexProfile.IndexFullName);
        var response = await searchClient.SearchAsync<SearchDocument>(
            "*",
            new SearchOptions
            {
                Size = BatchSize,
                Select = { "*" },
            },
            cancellationToken);

        await foreach (var searchResult in response.Value.GetResultsAsync())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            var document = searchResult.Document;
            var documentKey = ResolveKey(document, keyFieldName);

            if (!string.IsNullOrEmpty(documentKey))
            {
                yield return new KeyValuePair<string, SourceDocument>(
                    documentKey,
                    ExtractDocument(document, titleFieldName, contentFieldName));
            }
        }
    }

    /// <summary>
    /// Performs the i async enumerable operation.
    /// </summary>
    /// <param name="indexProfile">The index profile.</param>
    /// <param name="documentIds">The document ids.</param>
    /// <param name="keyFieldName">The key field name.</param>
    /// <param name="titleFieldName">The title field name.</param>
    /// <param name="contentFieldName">The content field name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
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

        var searchClient = _clientFactory.CreateSearchClient(indexProfile.IndexFullName);

        if (!string.IsNullOrWhiteSpace(keyFieldName))
        {
            var filter = string.Join(" or ", ids.Select(id => $"{keyFieldName} eq '{SanitizeODataValue(id)}'"));
            var response = await searchClient.SearchAsync<SearchDocument>(
                null,
                new SearchOptions
                {
                    Filter = filter,
                    Size = ids.Count,
                    Select = { "*" },
                },
                cancellationToken);

            await foreach (var searchResult in response.Value.GetResultsAsync())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                var document = searchResult.Document;
                var documentKey = ResolveKey(document, keyFieldName);

                if (!string.IsNullOrEmpty(documentKey))
                {
                    yield return new KeyValuePair<string, SourceDocument>(
                        documentKey,
                        ExtractDocument(document, titleFieldName, contentFieldName));
                }
            }

            yield break;
        }

        foreach (var id in ids)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            SearchDocument document = null;

            try
            {
                document = (await searchClient.GetDocumentAsync<SearchDocument>(id, cancellationToken: cancellationToken)).Value;
            }
            catch
            {
            }

            if (document != null)
            {
                yield return new KeyValuePair<string, SourceDocument>(
                    ResolveKey(document, null) ?? id,
                    ExtractDocument(document, titleFieldName, contentFieldName));
            }
        }
    }

    private static string ResolveKey(SearchDocument document, string keyFieldName)
    {
        if (!string.IsNullOrWhiteSpace(keyFieldName) &&
            document.TryGetValue(keyFieldName, out var keyValue))
        {
            return keyValue?.ToString();
        }

        var firstKey = document.Keys.FirstOrDefault();

        return firstKey != null && document.TryGetValue(firstKey, out var firstKeyValue)
            ? firstKeyValue?.ToString()
            : null;
    }

    private static SourceDocument ExtractDocument(SearchDocument document, string titleFieldName, string contentFieldName)
    {
        var title = !string.IsNullOrWhiteSpace(titleFieldName) &&
            document.TryGetValue(titleFieldName, out var titleValue)
                ? titleValue?.ToString()
                : null;
        var content = !string.IsNullOrWhiteSpace(contentFieldName) &&
            document.TryGetValue(contentFieldName, out var contentValue)
                ? contentValue?.ToString()
                : System.Text.Json.JsonSerializer.Serialize(document);

        if (string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(content))
        {
            title = ExtractTitleFromContent(content);
        }

        var fields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, value) in document)
        {
            fields[name] = value;
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

    private static string SanitizeODataValue(string value)
        => value.Replace("'", "''");
}
