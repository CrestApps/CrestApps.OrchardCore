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

        var mongoMetadata = dataSource.As<AzureMongoDBDataSourceMetadata>();
        if (mongoMetadata is null || string.IsNullOrWhiteSpace(mongoMetadata.IndexName))
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

        // Get RAG parameters from AIProfile metadata
        var ragParams = GetRagParameters(context);
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

        var mongoMetadata = dataSource.As<AzureMongoDBDataSourceMetadata>();
        if (mongoMetadata is null || string.IsNullOrWhiteSpace(mongoMetadata.IndexName))
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

        // Get RAG parameters from the profile
        var ragMetadata = dataSource.As<AzureRagChatMetadata>();

        options.AddDataSource(new MongoDBChatDataSource()
        {
            EndpointName = mongoMetadata.EndpointName,
            CollectionName = mongoMetadata.CollectionName,
            AppName = mongoMetadata.AppName,
            IndexName = mongoMetadata.IndexName,
            Authentication = credentials,
            Strictness = ragMetadata?.Strictness ?? AzureOpenAIConstants.DefaultStrictness,
            TopNDocuments = ragMetadata?.TopNDocuments ?? AzureOpenAIConstants.DefaultTopNDocuments,
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
    /// Gets RAG parameters from AIProfile metadata.
    /// </summary>
    private static (int? Strictness, int? TopNDocuments) GetRagParameters(CompletionServiceConfigureContext context)
    {
        if (context.AdditionalProperties is not null &&
            context.AdditionalProperties.TryGetValue("RagMetadata", out var ragMeta) &&
            ragMeta is AzureRagChatMetadata ragMetadata)
        {
            return (ragMetadata.Strictness, ragMetadata.TopNDocuments);
        }

        return (null, null);
    }
}
