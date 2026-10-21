using OrchardCore.Entities;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;

namespace CrestApps.OrchardCore.Products.Core.Search;

/// <summary>
/// The index profile handler a search provider's product search integration starts from: it configures a product
/// search index of its provider when the profile is initialized, created or updated, and leaves every other profile
/// alone.
/// </summary>
public abstract class ProductSearchIndexProfileHandlerBase : IndexProfileHandlerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProductSearchIndexProfileHandlerBase"/> class.
    /// </summary>
    /// <param name="providerName">The name of the search provider whose product search indexes the handler configures.</param>
    protected ProductSearchIndexProfileHandlerBase(string providerName)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);

        ProviderName = providerName;
    }

    /// <summary>
    /// Gets the name of the search provider whose product search indexes the handler configures.
    /// </summary>
    protected string ProviderName { get; }

    /// <inheritdoc/>
    public override Task InitializingAsync(InitializingContext<IndexProfile> context)
        => ConfigureIfProductSearchAsync(context.Model);

    /// <inheritdoc/>
    public override Task CreatingAsync(CreatingContext<IndexProfile> context)
        => ConfigureIfProductSearchAsync(context.Model);

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<IndexProfile> context)
        => ConfigureIfProductSearchAsync(context.Model);

    /// <summary>
    /// Whether the index profile is a product search index of the handler's provider.
    /// </summary>
    /// <param name="indexProfile">The index profile.</param>
    protected bool IsProductSearch(IndexProfile indexProfile)
        => ProductSearchIndexFields.IsProductSearchIndex(indexProfile, ProviderName);

    /// <summary>
    /// Configures a product search index of the handler's provider, such as its field mappings or its key field.
    /// </summary>
    /// <param name="indexProfile">The product search index profile.</param>
    protected abstract Task ConfigureAsync(IndexProfile indexProfile);

    private Task ConfigureIfProductSearchAsync(IndexProfile indexProfile)
        => IsProductSearch(indexProfile)
            ? ConfigureAsync(indexProfile)
            : Task.CompletedTask;
}
