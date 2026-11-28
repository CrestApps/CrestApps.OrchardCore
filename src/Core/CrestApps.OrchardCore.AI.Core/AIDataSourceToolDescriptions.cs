using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Describes an AI data source for the AI tools, from an explicit list of fields, and builds the links to its admin
/// pages. API keys, passwords and connection strings are reported only as stored or not. The list and search tools live
/// in the Data Sources module and the save and delete tools in the AI Agent module, so both use this one copy.
/// </summary>
public static class AIDataSourceToolDescriptions
{
    /// <summary>
    /// Gets the path of a data source's editor.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="id">The data source's ID.</param>
    /// <returns>The path, or <see langword="null"/> when there is no request to build it from.</returns>
    public static string GetEditPath(IServiceProvider services, string id)
        => AIModuleTools.GetAdminPath(services, AIConstants.Feature.DataSources, "DataSources", "Edit", new RouteValueDictionary { ["id"] = id });

    /// <summary>
    /// Gets the path of the page that creates a data source of a source type.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="sourceType">The source type.</param>
    /// <returns>The path, or <see langword="null"/> when there is no request to build it from.</returns>
    public static string GetCreatePath(IServiceProvider services, string sourceType)
        => AIModuleTools.GetAdminPath(services, AIConstants.Feature.DataSources, "DataSources", "Create", new RouteValueDictionary { ["sourceType"] = sourceType });

    /// <summary>
    /// Describes a data source.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="dataSource">The data source.</param>
    /// <returns>The description.</returns>
    public static async Task<object> DescribeAsync(IServiceProvider services, AIDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(dataSource);

        var localClock = services.GetService<ILocalClock>();
        var azure = dataSource.TryGet<AzureAISearchSourceMetadata>(out var azureMetadata) ? azureMetadata : null;
        var elasticsearch = dataSource.TryGet<ElasticsearchSourceMetadata>(out var elasticsearchMetadata) ? elasticsearchMetadata : null;
        var postgreSql = dataSource.TryGet<PostgreSQLSourceMetadata>(out var postgreSqlMetadata) ? postgreSqlMetadata : null;

        return new
        {
            id = dataSource.ItemId,
            displayText = dataSource.DisplayText,
            source = dataSource.GetSourceType(),
            sourceIndexProfileName = dataSource.SourceIndexProfileName,
            aiKnowledgeBaseIndexProfileName = dataSource.AIKnowledgeBaseIndexProfileName,
            keyFieldName = dataSource.KeyFieldName,
            titleFieldName = dataSource.TitleFieldName,
            contentFieldName = dataSource.ContentFieldName,
            isConfigurationLocked = dataSource.IsConfigurationLocked(),
            dataConnectionId = AIDataSourceConnectionPicker.GetConnectionId(dataSource),
            azureAISearch = azure is null
                ? null
                : new
                {
                    endpoint = azure.Endpoint,
                    authenticationType = azure.GetAuthenticationType(),
                    indexName = azure.IndexName,
                    identityClientId = azure.IdentityClientId,
                    hasApiKey = !string.IsNullOrWhiteSpace(azure.ApiKey),
                },
            elasticsearch = elasticsearch is null
                ? null
                : new
                {
                    environmentType = elasticsearch.GetEnvironmentType(),
                    url = elasticsearch.Url,
                    cloudId = elasticsearch.CloudId,
                    authenticationType = elasticsearch.GetAuthenticationType(),
                    indexName = elasticsearch.IndexName,
                    username = elasticsearch.Username,
                    apiKeyId = elasticsearch.ApiKeyId,
                    hasPassword = !string.IsNullOrWhiteSpace(elasticsearch.Password),
                    hasApiKey = !string.IsNullOrWhiteSpace(elasticsearch.ApiKey),
                    hasBase64ApiKey = !string.IsNullOrWhiteSpace(elasticsearch.Base64ApiKey),
                    hasCertificateFingerprint = !string.IsNullOrWhiteSpace(elasticsearch.CertificateFingerprint),
                },
            postgreSQL = postgreSql is null
                ? null
                : new
                {
                    tableName = postgreSql.TableName,
                    hasConnectionString = !string.IsNullOrWhiteSpace(postgreSql.ConnectionString),
                },
            createdAt = await AIModuleTools.ToLocalAsync(localClock, dataSource.CreatedUtc),
            modifiedAt = await AIModuleTools.ToLocalAsync(localClock, dataSource.ModifiedUtc),
            author = dataSource.Author,
            editUrl = GetEditPath(services, dataSource.ItemId),
        };
    }
}
