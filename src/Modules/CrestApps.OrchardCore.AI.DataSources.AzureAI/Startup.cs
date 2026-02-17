using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.AzureAI.Handlers;
using CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;
using OrchardCore.Search.AzureAI;
using OrchardCore.Search.AzureAI.Core;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI;

public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddIndexProfileHandler<DataSourceAzureAISearchIndexProfileHandler>();
        services.AddScoped<IDocumentIndexHandler, DataSourceAzureAISearchDocumentIndexHandler>();
        services.AddKeyedScoped<IDataSourceVectorSearchService, DataSourceAzureAISearchVectorSearchService>(
            AzureAISearchConstants.ProviderName);
        services.AddKeyedScoped<IDataSourceDocumentReader, DataSourceAzureAISearchDocumentReader>(
            AzureAISearchConstants.ProviderName);
        services.AddKeyedSingleton<IODataFilterTranslator, AzureAIODataFilterTranslator>(
            AzureAISearchConstants.ProviderName);

        services.AddAzureAISearchIndexingSource(DataSourceConstants.IndexingTaskType, o =>
        {
            o.DisplayName = S["AI Knowledge Base Index (Azure AI Search)"];
            o.Description = S["Create an Azure AI Search index to store AI knowledge base document embeddings for vector search."];
        });
    }
}
