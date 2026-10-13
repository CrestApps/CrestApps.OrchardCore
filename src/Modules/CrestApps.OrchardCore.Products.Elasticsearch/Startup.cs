using CrestApps.OrchardCore.Products.Core.Search;
using CrestApps.OrchardCore.Products.Elasticsearch.Handlers;
using CrestApps.OrchardCore.Products.Elasticsearch.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Elasticsearch;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Products.Elasticsearch;

/// <summary>
/// Adds the product search index type to Elasticsearch, and the search that reads it.
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
        services.AddElasticsearchIndexingSource(ProductSearchIndexFields.IndexingTaskType, options =>
        {
            options.DisplayName = S["Products (Elasticsearch)"];
            options.Description = S["Create an Elasticsearch index that product listings can search."];
        });

        services.AddIndexProfileHandler<ProductElasticsearchIndexProfileHandler>();
        services.AddKeyedScoped<IProductIndexSearchService, ElasticsearchProductSearchService>(ElasticsearchConstants.ProviderName);
    }
}
