using System.Text.Json;
using Azure.AI.OpenAI.Chat;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.MongoDB;
using CrestApps.OrchardCore.OpenAI.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Search.Elasticsearch;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.MongoDb;

public sealed class MongoDBOpenAIChatOptionsConfiguration : IOpenAIChatOptionsConfiguration, IAzureOpenAIDataSourceHandler
{
    private readonly IAIDataSourceManager _aIDataSourceManager;
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    public MongoDBOpenAIChatOptionsConfiguration(
        IAIDataSourceManager aIDataSourceManager,
        IIndexProfileStore indexProfileStore,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<MongoDBOpenAIChatOptionsConfiguration> logger)
    {
        _aIDataSourceManager = aIDataSourceManager;
        _indexProfileStore = indexProfileStore;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    public async Task InitializeConfigurationAsync(CompletionServiceConfigureContext context)
    {
        if (!CanHandle(context))
        {
            return;
        }

        if (context.AdditionalProperties is null || !context.AdditionalProperties.TryGetValue("DataSource", out _))
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

        if (!dataSource.TryGet<AzureAIDataSourceIndexMetadata>(out var indexMetadata) || string.IsNullOrWhiteSpace(indexMetadata.IndexName))
        {
            return;
        }

        if (!dataSource.TryGet<AzureMongoDBDataSourceMetadata>(out var mongoMetadata))
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
                ["index_name"] = indexMetadata.IndexName,
                ["app_name"] = mongoMetadata.AppName,
                ["authentication"] = authentication,
                ["semantic_configuration"] = "default",
                ["query_type"] = "simple",
            },
        };

        // Get RAG parameters from AIProfile metadata
        var ragParams = indexProfile.As<AzureRagChatMetadata>();

        mongoDbDataSource.parameters["top_n_documents"] = ragParams.TopNDocuments ?? AzureOpenAIConstants.DefaultTopNDocuments;
        mongoDbDataSource.parameters["strictness"] = ragParams.Strictness ?? AzureOpenAIConstants.DefaultStrictness;
        mongoDbDataSource.parameters["in_scope"] = ragParams.IsInScope;

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

        if (!dataSource.TryGet<AzureAIDataSourceIndexMetadata>(out var indexMetadata) || string.IsNullOrWhiteSpace(indexMetadata.IndexName))
        {
            return;
        }

        if (!dataSource.TryGet<AzureMongoDBDataSourceMetadata>(out var mongoMetadata))
        {
            return;
        }

        var indexProfile = await _indexProfileStore.FindByIndexNameAndProviderAsync(indexMetadata.IndexName, ElasticsearchConstants.ProviderName);

        if (indexProfile is null)
        {
            _logger.LogWarning("Index named '{IndexName}' set as Elasticsearch data-source but not found in Elasticsearch document manager.", indexMetadata.IndexName);
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

        if (!string.IsNullOrWhiteSpace(context.Filter))
        {
            _logger.LogWarning("MongoDB data source does not support filter parameter. The provided filter '{Filter}' will be ignored.", context.Filter);
        }

        options.AddDataSource(new MongoDBChatDataSource()
        {
            EndpointName = mongoMetadata.EndpointName,
            CollectionName = mongoMetadata.CollectionName,
            AppName = mongoMetadata.AppName,
            Authentication = credentials,
            Strictness = context.Strictness ?? AzureOpenAIConstants.DefaultStrictness,
            TopNDocuments = context.TopNDocuments ?? AzureOpenAIConstants.DefaultTopNDocuments,
            InScope = context.IsInScope ?? true,
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
}
