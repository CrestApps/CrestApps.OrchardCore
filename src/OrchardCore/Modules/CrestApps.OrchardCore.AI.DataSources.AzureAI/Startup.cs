using CrestApps.AI.Models;
using CrestApps.Infrastructure;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.AzureAI.Handlers;
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
        Azure.AISearch.ServiceCollectionExtensions.AddAzureAISearchServices(services);

        services.AddAzureAISearchIndexingSource(DataSourceConstants.IndexingTaskType, o =>
        {
            o.DisplayName = S["AI Knowledge Base Index (Azure AI Search)"];
            o.Description = S["Create an Azure AI Search index to store AI knowledge base document embeddings for vector search."];
        });

        services.Configure<AIDataSourceOptions>(options =>
        {
            options.AddFieldMapping(AzureAISearchConstants.ProviderName, IndexingConstants.ContentsIndexSource, mapping =>
            {
                mapping.DefaultKeyField = "ContentItemId";
                mapping.DefaultTitleField = "Content__ContentItem__DisplayText__keyword";
                mapping.DefaultContentField = "Content__ContentItem__FullText";
            });

            options.AddFieldMapping(AzureAISearchConstants.ProviderName, AIConstants.AIDocumentsIndexingTaskType, mapping =>
            {
                mapping.DefaultKeyField = AIConstants.ColumnNames.ChunkId;
                mapping.DefaultTitleField = AIConstants.ColumnNames.FileName;
                mapping.DefaultContentField = AIConstants.ColumnNames.Content;
            });
        });
    }
}
