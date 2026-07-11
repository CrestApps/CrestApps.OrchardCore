using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.Core.Models;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;

internal sealed class ElasticsearchAIDataSourceSourceHandler : IAIDataSourceSourceHandler
{
    private const int BatchSize = 1000;

    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<ElasticsearchAIDataSourceSourceHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElasticsearchAIDataSourceSourceHandler"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="logger">The logger.</param>
    public ElasticsearchAIDataSourceSourceHandler(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<ElasticsearchAIDataSourceSourceHandler> logger)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    public string SourceType => AIDataSourceSourceTypes.Elasticsearch;

    public ValueTask ValidateAsync(
        AIDataSource dataSource,
        ValidationResultDetails result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(result);

        if (!dataSource.TryGet<ElasticsearchSourceMetadata>(out var metadata))
        {
            result.Fail(new ValidationResult("Elasticsearch source settings are required.", [nameof(ElasticsearchSourceMetadata)]));

            return ValueTask.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(metadata.Url))
        {
            result.Fail(new ValidationResult("Elasticsearch URL is required.", [nameof(ElasticsearchSourceMetadata.Url)]));
        }

        var environmentType = metadata.GetEnvironmentType();
        var authenticationType = metadata.GetAuthenticationType();

        if (string.Equals(environmentType, ElasticsearchSourceMetadata.SelfManagedEnvironmentType, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(metadata.Url))
        {
            result.Fail(new ValidationResult("Elasticsearch URL is required for self-managed environments.", [nameof(ElasticsearchSourceMetadata.Url)]));
        }

        if (string.Equals(environmentType, ElasticsearchSourceMetadata.CloudHostedEnvironmentType, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(metadata.CloudId))
        {
            result.Fail(new ValidationResult("Elastic Cloud ID is required for cloud-hosted environments.", [nameof(ElasticsearchSourceMetadata.CloudId)]));
        }

        if (string.IsNullOrWhiteSpace(metadata.IndexName))
        {
            result.Fail(new ValidationResult("Elasticsearch index name is required.", [nameof(ElasticsearchSourceMetadata.IndexName)]));
        }

        var hasUsername = !string.IsNullOrWhiteSpace(metadata.Username);
        var hasPassword = !string.IsNullOrWhiteSpace(metadata.Password);

        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.BasicAuthenticationType, StringComparison.OrdinalIgnoreCase) &&
            hasUsername != hasPassword)
        {
            result.Fail(new ValidationResult("Elasticsearch basic authentication requires both username and password.", [nameof(ElasticsearchSourceMetadata.Username), nameof(ElasticsearchSourceMetadata.Password)]));
        }

        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(metadata.ApiKey))
        {
            result.Fail(new ValidationResult("Elasticsearch API key authentication requires an API key.", [nameof(ElasticsearchSourceMetadata.ApiKey)]));
        }

        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.Base64ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(metadata.Base64ApiKey))
        {
            result.Fail(new ValidationResult("Elasticsearch base64 API key authentication requires a base64-encoded API key.", [nameof(ElasticsearchSourceMetadata.Base64ApiKey)]));
        }

        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.KeyIdAndKeyAuthenticationType, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(metadata.ApiKeyId) || string.IsNullOrWhiteSpace(metadata.ApiKey)))
        {
            result.Fail(new ValidationResult("Elasticsearch key ID and key authentication requires both an API key ID and API key.", [nameof(ElasticsearchSourceMetadata.ApiKeyId), nameof(ElasticsearchSourceMetadata.ApiKey)]));
        }

        if (string.Equals(environmentType, ElasticsearchSourceMetadata.CloudHostedEnvironmentType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(authenticationType, ElasticsearchSourceMetadata.NoneAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            result.Fail(new ValidationResult("Elastic Cloud connections require an authentication type and matching credentials.", [nameof(ElasticsearchSourceMetadata.AuthenticationType)]));
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
        var (client, metadata) = Resolve(dataSource);
        string searchAfterValue = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var response = searchAfterValue == null
                ? await client.SearchAsync<JsonObject>(
                    search => search.Indices(metadata.IndexName).Size(BatchSize).Sort(sort => sort.Field("_doc")),
                    cancellationToken)
                : await client.SearchAsync<JsonObject>(
                    search => search.Indices(metadata.IndexName).Size(BatchSize).Sort(sort => sort.Field("_doc")).SearchAfter([searchAfterValue]),
                    cancellationToken);

            if (!response.IsValidResponse || response.Hits.Count == 0)
            {
                yield break;
            }

            foreach (var hit in response.Hits)
            {
                if (hit.Source == null)
                {
                    continue;
                }

                yield return CreateDocumentPair(dataSource, hit.Id, hit.Source);
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
        AIDataSource dataSource,
        IEnumerable<string> documentIds,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var ids = documentIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];

        if (ids.Length == 0)
        {
            yield break;
        }

        var (client, metadata) = Resolve(dataSource);
        var response = await client.SearchAsync<JsonObject>(
            search => search.Indices(metadata.IndexName).Query(query => query.Ids(idsQuery => idsQuery.Values(new Ids(ids)))).Size(ids.Length),
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

            yield return CreateDocumentPair(dataSource, hit.Id, hit.Source);
        }
    }

    private static KeyValuePair<string, SourceDocument> CreateDocumentPair(
        AIDataSource dataSource,
        string nativeId,
        JsonObject source)
    {
        var key = nativeId;

        if (!string.IsNullOrWhiteSpace(dataSource.KeyFieldName))
        {
            var keyNode = ResolveFieldValue(source, dataSource.KeyFieldName);

            if (keyNode != null)
            {
                key = GetStringValue(keyNode) ?? key;
            }
        }

        return new KeyValuePair<string, SourceDocument>(
            key,
            ExtractDocument(source, dataSource.TitleFieldName, dataSource.ContentFieldName));
    }

    private (ElasticsearchClient Client, ElasticsearchSourceMetadata Metadata) Resolve(AIDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        if (!dataSource.TryGet<ElasticsearchSourceMetadata>(out var metadata))
        {
            throw new InvalidOperationException("Elasticsearch source metadata is missing.");
        }

        var environmentType = metadata.GetEnvironmentType();
        var authenticationType = metadata.GetAuthenticationType();
        var protector = _dataProtectionProvider.CreateProtector(AIDataSourceProtectionConstants.SourceSecretPurpose);
        var authorizationHeader = CreateAuthorizationHeader(dataSource, metadata, authenticationType, protector);
        ElasticsearchClientSettings settings;

        if (string.Equals(environmentType, ElasticsearchSourceMetadata.CloudHostedEnvironmentType, StringComparison.OrdinalIgnoreCase))
        {
            settings = new ElasticsearchClientSettings(
                new CloudNodePool(metadata.CloudId?.Trim(), authorizationHeader ?? throw new InvalidOperationException("Elastic Cloud connections require an authentication type and matching credentials.")));
        }
        else
        {
            settings = new ElasticsearchClientSettings(new Uri(metadata.Url));
        }

        if (authorizationHeader != null)
        {
            settings = settings.Authentication(authorizationHeader);
        }

        if (!string.IsNullOrWhiteSpace(metadata.CertificateFingerprint))
        {
            settings = settings.CertificateFingerprint(metadata.CertificateFingerprint);
        }

        return (new ElasticsearchClient(settings), metadata);
    }

    private AuthorizationHeader CreateAuthorizationHeader(
        AIDataSource dataSource,
        ElasticsearchSourceMetadata metadata,
        string authenticationType,
        IDataProtector protector)
    {
        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.NoneAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.BasicAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            var password = DataProtectionHelper.Unprotect(protector, metadata.Password, _logger, "Failed to unprotect AI data source field '{FieldName}' for data source '{DataSourceId}'.", nameof(ElasticsearchSourceMetadata.Password), dataSource.ItemId);

            return new BasicAuthentication(metadata.Username, password);
        }

        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            var apiKey = DataProtectionHelper.Unprotect(protector, metadata.ApiKey, _logger, "Failed to unprotect AI data source field '{FieldName}' for data source '{DataSourceId}'.", nameof(ElasticsearchSourceMetadata.ApiKey), dataSource.ItemId);

            return new ApiKey(apiKey);
        }

        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.Base64ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            var base64ApiKey = DataProtectionHelper.Unprotect(protector, metadata.Base64ApiKey, _logger, "Failed to unprotect AI data source field '{FieldName}' for data source '{DataSourceId}'.", nameof(ElasticsearchSourceMetadata.Base64ApiKey), dataSource.ItemId);

            return new Base64ApiKey(base64ApiKey);
        }

        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.KeyIdAndKeyAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            var apiKey = DataProtectionHelper.Unprotect(protector, metadata.ApiKey, _logger, "Failed to unprotect AI data source field '{FieldName}' for data source '{DataSourceId}'.", nameof(ElasticsearchSourceMetadata.ApiKey), dataSource.ItemId);

            return new Base64ApiKey(metadata.ApiKeyId, apiKey);
        }

        throw new InvalidOperationException($"Unsupported Elasticsearch authentication type '{authenticationType}'.");
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

        if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue))
        {
            return stringValue;
        }

        return node is JsonValue value ? value.ToString() : node.ToJsonString();
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
