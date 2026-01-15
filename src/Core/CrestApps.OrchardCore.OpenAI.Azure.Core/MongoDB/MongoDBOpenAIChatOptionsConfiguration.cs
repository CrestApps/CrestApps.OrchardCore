using System.Text.Json;
using Azure.AI.OpenAI.Chat;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.MongoDB;
using CrestApps.OrchardCore.OpenAI.Core;
using Microsoft.AspNetCore.DataProtection;
using OpenAI.Chat;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.MongoDb;

public sealed class MongoDBOpenAIChatOptionsConfiguration : IOpenAIChatOptionsConfiguration, IAzureOpenAIDataSourceHandler
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

        // Try to get MongoDB metadata from new or legacy metadata
        var mongoMetadata = GetMongoDBMetadata(dataSource);
        if (mongoMetadata is null)
        {
            return;
        }

        var authentication = new Dictionary<string, object>();

        if (mongoMetadata.Authentication?.Type is not null &&
            string.Equals("username_and_password", mongoMetadata.Authentication.Type, StringComparison.OrdinalIgnoreCase))
        {
            authentication["type"] = "username_and_password";
            authentication["username"] = mongoMetadata.Authentication.Username;

            var password = string.Empty;

            if (!string.IsNullOrWhiteSpace(mongoMetadata.Authentication.Password))
            {
                var protector = _dataProtectionProvider.CreateProtector(AzureOpenAIConstants.MongoDataProtectionPurpose);

                password = protector.Unprotect(mongoMetadata.Authentication.Password);
            }

            authentication["password"] = password;
        }
        else
        {
            throw new NotSupportedException($"Unsupported authentication type: {mongoMetadata.Authentication?.Type}");
        }

        var mongoDbDataSource = new
        {
            type = "mongo_db",
            parameters = new Dictionary<string, object>
            {
                ["endpoint"] = mongoMetadata.EndpointName,
                ["collection_name"] = mongoMetadata.CollectionName,
                ["database_name"] = mongoMetadata.DatabaseName,
                ["index_name"] = mongoMetadata.IndexName,
                ["app_name"] = mongoMetadata.AppName,
                ["authentication"] = authentication,
                ["semantic_configuration"] = "default",
                ["query_type"] = "simple",
                ["in_scope"] = true,
            },
        };

        // Get RAG parameters from AIProfile first (new pattern), then fall back to legacy metadata on data source
        var ragParams = GetRagParameters(context, dataSource);
        mongoDbDataSource.parameters["top_n_documents"] = ragParams.TopNDocuments ?? AzureOpenAIConstants.DefaultTopNDocuments;
        mongoDbDataSource.parameters["strictness"] = ragParams.Strictness ?? AzureOpenAIConstants.DefaultStrictness;

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

    public async ValueTask ConfigureSourceAsync(ChatCompletionOptions options, AzureOpenAIDataSourceContext context)
    {
        if (string.IsNullOrEmpty(context.DataSourceId) || string.IsNullOrEmpty(context.DataSourceType))
        {
            return;
        }

        if (!string.Equals(context.DataSourceType, AzureOpenAIConstants.DataSourceTypes.MongoDB, StringComparison.Ordinal))
        {
            return;
        }

        var dataSource = await _aIDataSourceManager.FindByIdAsync(context.DataSourceId);

        if (dataSource is null)
        {
            return;
        }

        // Try to get MongoDB metadata from new or legacy metadata
        var mongoMetadata = GetMongoDBMetadata(dataSource);
        if (mongoMetadata is null)
        {
            return;
        }

#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        DataSourceAuthentication credentials = null;

        if (mongoMetadata.Authentication?.Type is not null)
        {
            if (string.Equals("username_and_password", mongoMetadata.Authentication.Type, StringComparison.OrdinalIgnoreCase))
            {
                var protector = _dataProtectionProvider.CreateProtector(AzureOpenAIConstants.MongoDataProtectionPurpose);

                string password = null;

                if (!string.IsNullOrWhiteSpace(mongoMetadata.Authentication.Password))
                {
                    password = protector.Unprotect(mongoMetadata.Authentication.Password);
                }

                credentials = DataSourceAuthentication.FromUsernameAndPassword(mongoMetadata.Authentication.Username, password);
            }
        }

        // Get RAG parameters from context (profile metadata) or fall back to legacy data source metadata
        var ragParams = GetRagParametersFromContext(context, dataSource);

        options.AddDataSource(new MongoDBChatDataSource()
        {
            EndpointName = mongoMetadata.EndpointName,
            CollectionName = mongoMetadata.CollectionName,
            AppName = mongoMetadata.AppName,
            IndexName = mongoMetadata.IndexName,
            Authentication = credentials,
            Strictness = ragParams.Strictness ?? AzureOpenAIConstants.DefaultStrictness,
            TopNDocuments = ragParams.TopNDocuments ?? AzureOpenAIConstants.DefaultTopNDocuments,
            InScope = true,
            OutputContexts = DataSourceOutputContexts.Citations,
        });
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
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

    /// <summary>
    /// Gets MongoDB metadata from the data source, trying new metadata first, then falling back to legacy.
    /// </summary>
    private static AzureMongoDBDataSourceMetadata GetMongoDBMetadata(AIDataSource dataSource)
    {
        // Try new metadata first
        var newMetadata = dataSource.As<AzureMongoDBDataSourceMetadata>();
        if (newMetadata is not null && !string.IsNullOrWhiteSpace(newMetadata.IndexName))
        {
            return newMetadata;
        }

        // Fall back to legacy metadata
#pragma warning disable CS0618 // Type or member is obsolete
        var legacyMetadata = dataSource.As<AzureAIProfileMongoDBMetadata>();
        if (legacyMetadata is not null && !string.IsNullOrWhiteSpace(legacyMetadata.IndexName))
        {
            return new AzureMongoDBDataSourceMetadata
            {
                IndexName = legacyMetadata.IndexName,
                EndpointName = legacyMetadata.EndpointName,
                AppName = legacyMetadata.AppName,
                CollectionName = legacyMetadata.CollectionName,
                DatabaseName = legacyMetadata.DatabaseName,
                Authentication = legacyMetadata.Authentication,
            };
        }
#pragma warning restore CS0618 // Type or member is obsolete

        return null;
    }

    /// <summary>
    /// Gets RAG parameters from context (profile) first, then falls back to legacy data source metadata.
    /// </summary>
    private static (int? Strictness, int? TopNDocuments) GetRagParameters(CompletionServiceConfigureContext context, AIDataSource dataSource)
    {
        // Try to get from AIProfile metadata (new pattern)
        if (context.AdditionalProperties is not null &&
            context.AdditionalProperties.TryGetValue("RagMetadata", out var ragMeta) &&
            ragMeta is AzureRagChatMetadata ragMetadata)
        {
            return (ragMetadata.Strictness, ragMetadata.TopNDocuments);
        }

        // Fall back to legacy data source metadata
#pragma warning disable CS0618 // Type or member is obsolete
        if (dataSource.TryGet<AzureAIProfileMongoDBMetadata>(out var legacyMetadata))
        {
            return (legacyMetadata.Strictness, legacyMetadata.TopNDocuments);
        }
#pragma warning restore CS0618 // Type or member is obsolete

        return (null, null);
    }

    /// <summary>
    /// Gets RAG parameters from context or falls back to data source.
    /// </summary>
    private static (int? Strictness, int? TopNDocuments) GetRagParametersFromContext(AzureOpenAIDataSourceContext context, AIDataSource dataSource)
    {
        // Try to get from context (set by completion handler from AIProfile)
        if (context.RagMetadata is not null)
        {
            return (context.RagMetadata.Strictness, context.RagMetadata.TopNDocuments);
        }

        // Fall back to legacy data source metadata
#pragma warning disable CS0618 // Type or member is obsolete
        if (dataSource.TryGet<AzureAIProfileMongoDBMetadata>(out var legacyMetadata))
        {
            return (legacyMetadata.Strictness, legacyMetadata.TopNDocuments);
        }
#pragma warning restore CS0618 // Type or member is obsolete

        return (null, null);
    }
}
