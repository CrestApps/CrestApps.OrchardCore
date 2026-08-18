using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// The default <see cref="IPriceResolver"/>. It resolves a product snapshot and returns the product's list
/// price tagged with the product-owned currency. A caller may request a currency, but the price is never
/// converted; a requested currency that differs from the product's currency is rejected so a price is
/// never charged in the wrong currency. A future pricing engine can replace this resolver to add price
/// schedules, quantity breaks, or customer-specific pricing without changing any consumer.
/// </summary>
public sealed class DefaultPriceResolver : IPriceResolver
{
    private readonly IProductSnapshotResolver _snapshotResolver;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultPriceResolver"/> class.
    /// </summary>
    /// <param name="snapshotResolver">The resolver that projects a content item into a sellable snapshot.</param>
    /// <param name="logger">The logger.</param>
    public DefaultPriceResolver(
        IProductSnapshotResolver snapshotResolver,
        ILogger<DefaultPriceResolver> logger)
    {
        _snapshotResolver = snapshotResolver;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PriceResult> ResolveAsync(ProductSnapshotContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var product = await _snapshotResolver.ResolveAsync(context, cancellationToken);

        if (product is null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(product.Currency))
        {
            _logger.LogWarning("Refusing to price product '{ContentItemId}': it has no currency and no default currency is configured for its content type.", product.ContentItemId);

            return null;
        }

        if (!string.IsNullOrEmpty(context.Currency) &&
            !string.Equals(context.Currency, product.Currency, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Refusing to price product '{ContentItemId}' in '{RequestedCurrency}': it is sold in '{ProductCurrency}' and no conversion is applied.", product.ContentItemId, context.Currency, product.Currency);

            return null;
        }

        return new PriceResult(product.UnitPrice, product.Currency, context.Quantity);
    }
}
