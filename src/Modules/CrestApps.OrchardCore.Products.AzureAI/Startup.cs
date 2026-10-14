using CrestApps.OrchardCore.Products.AzureAI.Handlers;
using CrestApps.OrchardCore.Products.AzureAI.Services;
using CrestApps.OrchardCore.Products.Core.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.AzureAI;
using OrchardCore.AzureAI.Core;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Products.AzureAI;

/// <summary>
/// Adds the product search index type to Azure AI Search, and the search that reads it.
/// </summary>
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAzureAISearchIndexingSource(ProductSearchIndexFields.IndexingTaskType, options =>
        {
            options.DisplayName = S["Products (Azure AI Search)"];
            options.Description = S["Create an Azure AI Search index that product listings can search."];
        });

        services.AddIndexProfileHandler<ProductAzureAISearchIndexProfileHandler>();
        services.AddScoped<IDocumentIndexHandler, ProductAzureAISearchDocumentIndexHandler>();
        services.AddKeyedScoped<IProductIndexSearchService, AzureAIProductSearchService>(AzureAISearchConstants.ProviderName);
    }
}
