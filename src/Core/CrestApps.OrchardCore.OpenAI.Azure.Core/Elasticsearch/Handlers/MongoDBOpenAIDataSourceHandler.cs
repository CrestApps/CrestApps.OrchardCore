using Azure.AI.OpenAI.Chat;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using OpenAI.Chat;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch.Handlers;

public sealed class MongoDBOpenAIDataSourceHandler : IAzureOpenAIDataSourceHandler
{
    private readonly IAIDataSourceManager _aIDataSourceManager;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public MongoDBOpenAIDataSourceHandler(
        IAIDataSourceManager aIDataSourceManager,
        IDataProtectionProvider dataProtectionProvider)
    {
        _aIDataSourceManager = aIDataSourceManager;
        _dataProtectionProvider = dataProtectionProvider;
    }

    public bool CanHandle(string type)
        => string.Equals(type, AzureOpenAIConstants.DataSourceTypes.MongoDB, StringComparison.Ordinal);

    public async ValueTask ConfigureSourceAsync(ChatCompletionOptions options, AzureOpenAIDataSourceContext context)
    {
        var dataSource = await _aIDataSourceManager.FindByIdAsync(context.DataSourceId);

        if (dataSource is null)
        {
            return;
        }

        if (!dataSource.TryGet<AzureAIProfileMongoDBMetadata>(out var dataSourceMetadata))
        {
            return;
        }

#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        DataSourceAuthentication credentials = null;

        if (dataSourceMetadata.Authentication?.Type is not null)
        {
            if (string.Equals("username_and_password", dataSourceMetadata.Authentication.Type, StringComparison.OrdinalIgnoreCase))
            {
                var protector = _dataProtectionProvider.CreateProtector(AzureOpenAIConstants.MongoDataProtectionPurpose);

                string password = null;

                if (!string.IsNullOrWhiteSpace(dataSourceMetadata.Authentication.Password))
                {
                    password = protector.Unprotect(dataSourceMetadata.Authentication.Password);
                }

                credentials = DataSourceAuthentication.FromUsernameAndPassword(dataSourceMetadata.Authentication.Username, password);
            }
        }

        options.AddDataSource(new MongoDBChatDataSource()
        {
            EndpointName = dataSourceMetadata.EndpointName,
            CollectionName = dataSourceMetadata.CollectionName,
            AppName = dataSourceMetadata.AppName,
            IndexName = dataSourceMetadata.IndexName,
            Authentication = credentials,
            Strictness = dataSourceMetadata.Strictness ?? AzureOpenAIConstants.DefaultStrictness,
            TopNDocuments = dataSourceMetadata.TopNDocuments ?? AzureOpenAIConstants.DefaultTopNDocuments,
            InScope = true,
            OutputContexts = DataSourceOutputContexts.Citations,
        });
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }
}
