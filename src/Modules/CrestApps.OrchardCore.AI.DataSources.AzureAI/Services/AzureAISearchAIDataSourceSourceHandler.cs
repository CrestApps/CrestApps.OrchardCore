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
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;

internal sealed class AzureAISearchAIDataSourceSourceHandler : IAIDataSourceSourceHandler
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<AzureAISearchAIDataSourceSourceHandler> _logger;
    private readonly IDataConnectionResolver _dataConnectionResolver;
    private readonly IAIEndpointPolicy _endpointPolicy;
    private readonly AzureAISearchHostIdentity _hostIdentity;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAISearchAIDataSourceSourceHandler"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="dataConnectionResolver">Resolves the data connection a data source may reference; <see langword="null"/> while the Data Connectors feature is off.</param>
    /// <param name="endpointPolicy">The policy that keeps the search endpoint off the site's own network; <see langword="null"/> refuses every private address.</param>
    /// <param name="hostIdentity">Where the data source may sign in as the host; <see langword="null"/> refuses it everywhere.</param>
    public AzureAISearchAIDataSourceSourceHandler(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<AzureAISearchAIDataSourceSourceHandler> logger,
        IDataConnectionResolver dataConnectionResolver = null,
        IAIEndpointPolicy endpointPolicy = null,
        AzureAISearchHostIdentity hostIdentity = null)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
        _dataConnectionResolver = dataConnectionResolver;
        _endpointPolicy = endpointPolicy;
        _hostIdentity = hostIdentity;
    }

    // The host's identity reads whatever the host can, other tenants' indexes included (review 10, B1).
    private bool MaySignInAsHost(string endpoint, string indexName, string identityClientId)
        => _hostIdentity?.IsApproved(endpoint, indexName, identityClientId) == true;

    public string SourceType => AIDataSourceSourceTypes.AzureAISearch;

    public async ValueTask ValidateAsync(
        AIDataSource dataSource,
        ValidationResultDetails result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(result);

        if (!dataSource.TryGet<AzureAISearchSourceMetadata>(out var metadata))
        {
            result.Fail(new ValidationResult("Azure AI Search source settings are required.", [nameof(AzureAISearchSourceMetadata)]));

            return;
        }

        // A data source that references a data connection reads the connection's endpoint and key.
        var connectionId = AIDataSourceConnectionPicker.GetConnectionId(dataSource);

        if (connectionId is not null)
        {
            if (string.IsNullOrWhiteSpace(metadata.IndexName))
            {
                result.Fail(new ValidationResult("Azure AI Search index name is required.", [nameof(AzureAISearchSourceMetadata.IndexName)]));
            }

            var resolution = await _dataConnectionResolver.ResolveForAsync(connectionId, AzureAISearchDataConnectionClient.ConnectorNames, cancellationToken);

            if (!resolution.Succeeded)
            {
                result.Fail(new ValidationResult(resolution.Error, [nameof(DataConnectionReference.ConnectionId)]));
            }
            else if (resolution.Context.Connection.GetOrCreate<HttpConnectionSettings>() is { } settings &&
                AzureAISearchHostIdentity.SignsInAsHost(settings) &&
                !MaySignInAsHost(settings.BaseUrl, metadata.IndexName, identityClientId: null))
            {
                result.Fail(new ValidationResult(AzureAISearchHostIdentity.NotApprovedError, [nameof(DataConnectionReference.ConnectionId)]));
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(metadata.Endpoint))
        {
            result.Fail(new ValidationResult("Azure AI Search endpoint is required.", [nameof(AzureAISearchSourceMetadata.Endpoint)]));
        }
        else if (!(_endpointPolicy?.IsAllowed(metadata.Endpoint) ?? AIEgress.IsAllowedEndpoint(metadata.Endpoint, isDevelopment: false, options: null)))
        {
            // The editor, the recipe step and the API all validate here, so none of them can point a data source at the
            // site's own network.
            result.Fail(new ValidationResult("This address is on the site's own network. Use a public address, or ask the host to allow a private network for AI connections.", [nameof(AzureAISearchSourceMetadata.Endpoint)]));
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

        if (AzureAISearchHostIdentity.SignsInAsHost(metadata) &&
            !string.IsNullOrWhiteSpace(metadata.Endpoint) &&
            !MaySignInAsHost(metadata.Endpoint, metadata.IndexName, metadata.IdentityClientId))
        {
            result.Fail(new ValidationResult(AzureAISearchHostIdentity.NotApprovedError, [nameof(AzureAISearchSourceMetadata.AuthenticationType)]));
        }
    }

    public ValueTask<string> GetReferenceTypeAsync(
        AIDataSource dataSource,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(SourceType);

    public async IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadAsync(
        AIDataSource dataSource,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var searchClient = await ResolveAsync(dataSource, cancellationToken);
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

        var searchClient = await ResolveAsync(dataSource, cancellationToken);

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
                // The document was removed from the index after it was listed, so there is nothing to read.
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

    /// <summary>
    /// Creates the client of a data source: from the data connection it references, else from its own settings.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The client.</returns>
    internal async Task<SearchClient> ResolveAsync(AIDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        if (!dataSource.TryGet<AzureAISearchSourceMetadata>(out var metadata))
        {
            throw new InvalidOperationException("Azure AI Search source metadata is missing.");
        }

        var connectionId = AIDataSourceConnectionPicker.GetConnectionId(dataSource);

        if (connectionId is not null)
        {
            var resolution = await _dataConnectionResolver.ResolveForAsync(connectionId, AzureAISearchDataConnectionClient.ConnectorNames, cancellationToken);

            if (!resolution.Succeeded)
            {
                throw new InvalidOperationException(resolution.Error);
            }

            var settings = resolution.Context.Connection.GetOrCreate<HttpConnectionSettings>();

            // Checked before every sync too: the connection, or the host's approval, may have changed since the save.
            if (AzureAISearchHostIdentity.SignsInAsHost(settings) && !MaySignInAsHost(settings.BaseUrl, metadata.IndexName, identityClientId: null))
            {
                throw new InvalidOperationException(AzureAISearchHostIdentity.NotApprovedError);
            }

            return AzureAISearchDataConnectionClient.Create(settings, resolution.Context.Secrets, metadata.IndexName);
        }

        var endpoint = new Uri(metadata.Endpoint);
        var authenticationType = metadata.GetAuthenticationType();

        if (string.Equals(authenticationType, AzureAISearchSourceMetadata.ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            var protector = _dataProtectionProvider.CreateProtector(AIDataSourceProtectionConstants.SourceSecretPurpose);
            var apiKey = DataProtectionHelper.Unprotect(protector, metadata.ApiKey, _logger, "Failed to unprotect AI data source field '{FieldName}' for data source '{DataSourceId}'.", nameof(AzureAISearchSourceMetadata.ApiKey), dataSource.ItemId);

            return new SearchClient(endpoint, metadata.IndexName, new AzureKeyCredential(apiKey));
        }

        if (!MaySignInAsHost(metadata.Endpoint, metadata.IndexName, metadata.IdentityClientId))
        {
            throw new InvalidOperationException(AzureAISearchHostIdentity.NotApprovedError);
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
            title = AIDataSourceExtensions.GetTitleFromContent(content);
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
}
