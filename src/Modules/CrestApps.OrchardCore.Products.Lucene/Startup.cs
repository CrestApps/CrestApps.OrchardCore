using CrestApps.OrchardCore.Products.Core.Search;
using CrestApps.OrchardCore.Products.Lucene.Handlers;
using CrestApps.OrchardCore.Products.Lucene.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Indexing.Core;
using OrchardCore.Lucene;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Products.Lucene;

/// <summary>
/// Adds the product search index type to Lucene, and the search that reads it.
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
        services.AddLuceneIndexingSource(ProductSearchIndexFields.IndexingTaskType, options =>
        {
            options.DisplayName = S["Products (Lucene)"];
            options.Description = S["Create a Lucene index that product listings can search."];
        });

        services.AddIndexProfileHandler<ProductLuceneIndexProfileHandler>();
        services.AddKeyedScoped<IProductIndexSearchService, LuceneProductSearchService>(LuceneConstants.ProviderName);
    }
}
