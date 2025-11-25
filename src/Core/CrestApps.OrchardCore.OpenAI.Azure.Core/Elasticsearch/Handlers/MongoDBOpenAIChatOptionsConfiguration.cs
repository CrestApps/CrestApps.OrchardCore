using System.Text.Json;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Core;
using Microsoft.AspNetCore.DataProtection;
using OpenAI.Chat;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch.Handlers;

public sealed class MongoDBOpenAIChatOptionsConfiguration : IOpenAIChatOptionsConfiguration
{
    private readonly IAIDataSourceManager _aIDataSourceManager;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public MongoDBOpenAIChatOptionsConfiguration(
        IAIDataSourceManager aIDataSourceManager,
        IDataProtectionProvider dataProtectionProvider)
    {
        _aIDataSourceManager = aIDataSourceManager;
        _dataProtectionProvider = dataProtectionProvider;
    }

    public async Task InitializeConfigurationAsync(CompletionServiceConfigureContext context)
    {
        if (!CanHandle(context))
        {
            return;
        }

        if (context.AdditionalProperties is null || !context.AdditionalProperties.TryGetValue("DataSource", out var ds))
        {
            var dataSource = await _aIDataSourceManager.FindByIdAsync(context.CompletionContext.DataSourceId);

            if (dataSource is null)
            {
                return;
            }

            context.AdditionalProperties ??= [];
            context.AdditionalProperties["DataSource"] = dataSource;
        }
    }

    public void Configure(CompletionServiceConfigureContext context, ChatCompletionOptions chatCompletionOptions)
    {
        if (!CanHandle(context))
        {
            return;
        }

        if (context.AdditionalProperties is null || context.AdditionalProperties.Count == 0)
        {
            return;
        }

        if (!context.AdditionalProperties.TryGetValue("DataSource", out var ds) ||
            ds is not AIDataSource dataSource)
        {
            return;
        }

        if (!context.AdditionalProperties.TryGetValue("ElasticsearchIndexProfile", out var pr) ||
            pr is not IndexProfile indexProfile)
        {
            return;
        }

        if (!dataSource.TryGet<AzureAIProfileMongoDBMetadata>(out var dataSourceMetadata))
        {
            return;
        }

        var authentication = new Dictionary<string, object>();

        if (dataSourceMetadata.Authentication?.Type is not null &&
            string.Equals("username_and_password", dataSourceMetadata.Authentication.Type, StringComparison.OrdinalIgnoreCase))
        {
            authentication["type"] = "username_and_password";
            authentication["username"] = dataSourceMetadata.Authentication.Username;

            var password = string.Empty;

            if (!string.IsNullOrWhiteSpace(dataSourceMetadata.Authentication.Password))
            {
                var protector = _dataProtectionProvider.CreateProtector(AzureOpenAIConstants.MongoDataProtectionPurpose);

                password = protector.Unprotect(dataSourceMetadata.Authentication.Password);
            }

            authentication["password"] = password;
        }
        else
        {
            throw new NotSupportedException($"Unsupported authentication type: {dataSourceMetadata.Authentication?.Type}");
        }

        var mongoDbDataSource = new
        {
            type = "mongo_db",
            parameters = new Dictionary<string, object>
            {
                ["endpoint"] = dataSourceMetadata.EndpointName,
                ["collection_name"] = dataSourceMetadata.CollectionName,
                ["database_name"] = dataSourceMetadata.DatabaseName,
                ["index_name"] = dataSourceMetadata.IndexName,
                ["app_name"] = dataSourceMetadata.AppName,
                ["authentication"] = authentication,
                ["semantic_configuration"] = "default",
                ["query_type"] = "simple",
                ["in_scope"] = true,
            },
        };

#pragma warning disable SCME0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        var dataSources = new List<object>()
            {
                mongoDbDataSource,
            };

        if (chatCompletionOptions.Patch.TryGetJson("$.data_sources"u8, out var dataSourcesJson) && dataSourcesJson.Length > 0)
        {
            var sources = JsonSerializer.Deserialize<List<object>>(dataSourcesJson.Span);
            if (sources != null)
            {
                foreach (var source in sources)
                {
                    dataSources.Add(source);
                }
            }
        }

        chatCompletionOptions.Patch.Set("$.data_sources"u8, BinaryData.FromObjectAsJson(dataSources));
#pragma warning restore SCME0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }

    private static bool CanHandle(CompletionServiceConfigureContext context)
    {
        if (!string.Equals(context.ProviderName, AzureOpenAIConstants.ProviderName, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(context.CompletionContext.DataSourceType, AzureOpenAIConstants.DataSourceTypes.MongoDB, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}
