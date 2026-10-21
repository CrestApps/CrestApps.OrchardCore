using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Indexing;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Sends a listing's search to the product search index it names, or to the database.
/// </summary>
/// <remarks>
/// The database search answers when the listing names no index, when the index or its provider is gone, and when
/// the search server fails, so a listing keeps working while a server is down.
/// </remarks>
public sealed class ProductSearchRouter
{
    private readonly SqlProductSearchService _sqlSearch;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public ProductSearchRouter(
        SqlProductSearchService sqlSearch,
        IServiceProvider serviceProvider,
        ILogger<ProductSearchRouter> logger)
    {
        _sqlSearch = sqlSearch;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<ProductSearchResult> SearchAsync(ProductSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!string.IsNullOrEmpty(query.IndexProfileName))
        {
            // The index profile store exists only while Orchard Core Indexing is enabled.
            var store = _serviceProvider.GetService<IIndexProfileStore>();
            var indexProfile = store is null ? null : await store.FindByNameAsync(query.IndexProfileName);

            if (indexProfile is not null &&
                ProductSearchIndexFields.IsProductSearchIndex(indexProfile) &&
                _serviceProvider.GetKeyedService<IProductIndexSearchService>(indexProfile.ProviderName) is { } indexSearch)
            {
                try
                {
                    return await indexSearch.SearchAsync(indexProfile, query, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "The product search index '{IndexName}' could not be searched; the database search answered instead.", indexProfile.Name);
                }
            }
            else
            {
                _logger.LogWarning("The product search index '{IndexName}' was not found or its provider is not enabled; the database search answered instead.", query.IndexProfileName);
            }
        }

        return await _sqlSearch.SearchAsync(query, cancellationToken);
    }
}
