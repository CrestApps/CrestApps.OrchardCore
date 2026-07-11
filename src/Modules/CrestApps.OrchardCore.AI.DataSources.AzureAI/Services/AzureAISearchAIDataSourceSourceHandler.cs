using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using CrestApps.Core;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;

internal sealed class AzureAISearchAIDataSourceSourceHandler : IAIDataSourceSourceHandler
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<AzureAISearchAIDataSourceSourceHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAISearchAIDataSourceSourceHandler"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="logger">The logger.</param>
    public AzureAISearchAIDataSourceSourceHandler(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<AzureAISearchAIDataSourceSourceHandler> logger)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    public string SourceType => AIDataSourceSourceTypes.AzureAISearch;

    public ValueTask ValidateAsync(
        AIDataSource dataSource,
        ValidationResultDetails result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(result);

        if (!dataSource.TryGet<AzureAISearchSourceMetadata>(out var metadata))
        {
            result.Fail(new ValidationResult("Azure AI Search source settings are required.", [nameof(AzureAISearchSourceMetadata)]));

            return ValueTask.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(metadata.Endpoint))
        {
            result.Fail(new ValidationResult("Azure AI Search endpoint is required.", [nameof(AzureAISearchSourceMetadata.Endpoint)]));
        }

        if (string.IsNullOrWhiteSpace(metadata.IndexName))
        {
            result.Fail(new ValidationResult("Azure AI Search index name is required.", [nameof(AzureAISearchSourceMetadata.IndexName)]));
        }

        if (string.Equals(metadata.GetAuthenticationType(), AzureAISearchSourceMetadata.ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(metadata.ApiKey))
        {
            result.Fail(new ValidationResult("Azure AI Search API key is required.", [nameof(AzureAISearchSourceMetadata.ApiKey)]));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<string> GetReferenceTypeAsync(
        AIDataSource dataSource,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(SourceType);

    public async IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadAsync(
        AIDataSource dataSource,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var searchClient = Resolve(dataSource);
        var response = await searchClient.SearchAsync<SearchDocument>(
            "*",
            new SearchOptions
            {
                Size = 1000,
                Select = { "*" },
            },
            cancellationToken);

        await foreach (var result in response.Value.GetResultsAsync())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            yield return CreateDocumentPair(dataSource, result.Document);
        }
    }

    public async IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadByIdsAsync(
        AIDataSource dataSource,
        IEnumerable<string> documentIds,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var ids = documentIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];

        if (ids.Length == 0)
        {
            yield break;
        }

        var searchClient = Resolve(dataSource);

        if (!string.IsNullOrWhiteSpace(dataSource.KeyFieldName))
        {
            var filter = string.Join(" or ", ids.Select(id => $"{dataSource.KeyFieldName} eq '{id.Replace("'", "''", StringComparison.Ordinal)}'"));
            var response = await searchClient.SearchAsync<SearchDocument>(
                null,
                new SearchOptions
                {
                    Filter = filter,
                    Size = ids.Length,
                    Select = { "*" },
                },
                cancellationToken);

            await foreach (var result in response.Value.GetResultsAsync())
            {
                yield return CreateDocumentPair(dataSource, result.Document);
            }

            yield break;
        }

        foreach (var id in ids)
        {
            SearchDocument document = null;

            try
            {
                document = (await searchClient.GetDocumentAsync<SearchDocument>(id, cancellationToken: cancellationToken)).Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
            }

            if (document != null)
            {
                yield return CreateDocumentPair(dataSource, document);
            }
        }
    }

    private static KeyValuePair<string, SourceDocument> CreateDocumentPair(
        AIDataSource dataSource,
        SearchDocument document)
    {
        var key = document.Keys.FirstOrDefault() is { } firstKey && document.TryGetValue(firstKey, out var firstKeyValue)
            ? firstKeyValue?.ToString()
            : null;

        if (!string.IsNullOrWhiteSpace(dataSource.KeyFieldName) &&
            document.TryGetValue(dataSource.KeyFieldName, out var configuredKeyValue))
        {
            key = configuredKeyValue?.ToString() ?? key;
        }

        return new KeyValuePair<string, SourceDocument>(
            key,
            ExtractDocument(document, dataSource.TitleFieldName, dataSource.ContentFieldName));
    }

    private SearchClient Resolve(AIDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        if (!dataSource.TryGet<AzureAISearchSourceMetadata>(out var metadata))
        {
            throw new InvalidOperationException("Azure AI Search source metadata is missing.");
        }

        var endpoint = new Uri(metadata.Endpoint);
        var authenticationType = metadata.GetAuthenticationType();

        if (string.Equals(authenticationType, AzureAISearchSourceMetadata.ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            var protector = _dataProtectionProvider.CreateProtector(AIDataSourceProtectionConstants.SourceSecretPurpose);
            var apiKey = DataProtectionHelper.Unprotect(protector, metadata.ApiKey, _logger, "Failed to unprotect AI data source field '{FieldName}' for data source '{DataSourceId}'.", nameof(AzureAISearchSourceMetadata.ApiKey), dataSource.ItemId);

            return new SearchClient(endpoint, metadata.IndexName, new AzureKeyCredential(apiKey));
        }

        TokenCredential credential;

        if (string.Equals(authenticationType, AzureAISearchSourceMetadata.ManagedIdentityAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            credential = CreateManagedIdentityCredential(metadata.IdentityClientId);
        }
        else
        {
            credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = string.IsNullOrWhiteSpace(metadata.IdentityClientId) ? null : metadata.IdentityClientId,
            });
        }

        return new SearchClient(endpoint, metadata.IndexName, credential);
    }

    private static ManagedIdentityCredential CreateManagedIdentityCredential(string identityClientId)
    {
#pragma warning disable CS0618 // The currently referenced Azure.Identity package only exposes obsolete managed-identity constructors.
        if (string.IsNullOrWhiteSpace(identityClientId))
        {
            return new ManagedIdentityCredential();
        }

        return new ManagedIdentityCredential(identityClientId);
#pragma warning restore CS0618
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
}
