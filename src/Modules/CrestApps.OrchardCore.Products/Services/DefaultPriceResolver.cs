using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

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
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultPriceResolver"/> class.
    /// </summary>
    /// <param name="snapshotResolver">The resolver that projects a content item into a sellable snapshot.</param>
    /// <param name="clock">The clock used to decide whether a price may still be bought.</param>
    /// <param name="logger">The logger.</param>
    public DefaultPriceResolver(
        IProductSnapshotResolver snapshotResolver,
        IClock clock,
        ILogger<DefaultPriceResolver> logger)
    {
        _snapshotResolver = snapshotResolver;
        _clock = clock;
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

        // A product that lists its own prices is priced from the one the buyer chose. Falling through to
        // the product part's single price would silently charge them something they never selected.
        if (context.ContentItem is not null &&
            context.ContentItem.TryGet<ProductPricePart>(out var pricing) &&
            pricing.Prices is { Count: > 0 })
        {
            return ResolveFromPrices(context, product, pricing);
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

    private PriceResult ResolveFromPrices(ProductSnapshotContext context, ISellableProduct product, ProductPricePart pricing)
    {
        var now = _clock.UtcNow;
        var price = string.IsNullOrEmpty(context.PriceId)
            ? pricing.GetDefault(now)
            : pricing.Find(context.PriceId);

        if (price is null)
        {
            _logger.LogWarning("Refusing to price product '{ContentItemId}': it offers no price matching '{PriceId}'.", product.ContentItemId, context.PriceId ?? "(default)");

            return null;
        }

        // A price the buyer names has to still be on offer. Honoring a withdrawn one would let an old link
        // keep selling something the merchant stopped selling.
        if (!price.IsAvailable(now))
        {
            _logger.LogWarning("Refusing to price product '{ContentItemId}' at price '{PriceId}': it is no longer offered.", product.ContentItemId, price.PriceId);

            return null;
        }

        var currency = string.IsNullOrEmpty(price.Currency) ? product.Currency : price.Currency;

        if (string.IsNullOrEmpty(currency))
        {
            _logger.LogWarning("Refusing to price product '{ContentItemId}': price '{PriceId}' has no currency.", product.ContentItemId, price.PriceId);

            return null;
        }

        if (!string.IsNullOrEmpty(context.Currency) &&
            !string.Equals(context.Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Refusing to price product '{ContentItemId}' in '{RequestedCurrency}': price '{PriceId}' is sold in '{PriceCurrency}' and no conversion is applied.", product.ContentItemId, context.Currency, price.PriceId, currency);

            return null;
        }

        var amount = price.GetChargeableAmount(context.CustomAmount);

        if (amount is null)
        {
            _logger.LogWarning("Refusing to price product '{ContentItemId}' at price '{PriceId}': the amount asked for is outside what the price allows.", product.ContentItemId, price.PriceId);

            return null;
        }

        var quantity = price.GetChargeableQuantity(context.Quantity);

        if (quantity is null)
        {
            _logger.LogWarning("Refusing to price product '{ContentItemId}' at price '{PriceId}': the quantity asked for is more than the price allows.", product.ContentItemId, price.PriceId);

            return null;
        }

        return new PriceResult(amount.Value, currency, quantity.Value, price);
    }
}
