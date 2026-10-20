using System.Threading;
using System.Threading.Tasks;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// The default <see cref="IPriceResolver"/>. It resolves a product snapshot and returns the product's list
/// price tagged with the product-owned currency. A caller may request another currency: a price the product lists in
/// it wins, otherwise a one-time price is converted by <see cref="IShopperCurrency"/>, and one that can't be is
/// rejected so a price is never charged in the wrong currency. Recurring prices are never converted, since a
/// subscription keeps charging what it started at. A future pricing engine can replace this resolver to add price
/// schedules, quantity breaks, or customer-specific pricing without changing any consumer.
/// </summary>
public sealed class DefaultPriceResolver : IPriceResolver
{
    private readonly IProductSnapshotResolver _snapshotResolver;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;
    private readonly IShopperCurrency _shopperCurrency;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultPriceResolver"/> class that applies role discounts for the
    /// signed-in buyer.
    /// </summary>
    public DefaultPriceResolver(
        IProductSnapshotResolver snapshotResolver,
        IClock clock,
        ILogger<DefaultPriceResolver> logger,
        Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor)
        : this(snapshotResolver, clock, logger)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultPriceResolver"/> class that also prices products in the
    /// shopper's currency.
    /// </summary>
    public DefaultPriceResolver(
        IProductSnapshotResolver snapshotResolver,
        IClock clock,
        ILogger<DefaultPriceResolver> logger,
        Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
        IShopperCurrency shopperCurrency)
        : this(snapshotResolver, clock, logger, httpContextAccessor)
    {
        _shopperCurrency = shopperCurrency;
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
        // the product part's single price would silently charge them something they never selected. A chosen
        // variant carries its own price instead.
        if (product.VariantId is null &&
            context.ContentItem is not null &&
            context.ContentItem.TryGet<ProductPricePart>(out var pricing) &&
            pricing.Prices is { Count: > 0 })
        {
            return await ResolveFromPricesAsync(context, product, pricing, cancellationToken);
        }

        if (string.IsNullOrEmpty(product.Currency))
        {
            _logger.LogWarning("Refusing to price product '{ContentItemId}': it has no currency and no default currency is configured for its content type.", product.ContentItemId);

            return null;
        }

        var unitPrice = product.UnitPrice;
        var currency = product.Currency;

        if (!string.IsNullOrEmpty(context.Currency) &&
            !string.Equals(context.Currency, product.Currency, StringComparison.OrdinalIgnoreCase))
        {
            if (await ConvertAsync(unitPrice, product.Currency, context.Currency, cancellationToken) is not { } converted)
            {
                _logger.LogWarning("Refusing to price product '{ContentItemId}' in '{RequestedCurrency}': it is sold in '{ProductCurrency}' and can't be converted.", product.ContentItemId, context.Currency, product.Currency);

                return null;
            }

            unitPrice = converted;
            currency = context.Currency.ToUpperInvariant();
        }

        // Volume and role discounts come off the price the product sells at.

        if (context.ContentItem is not null && context.ContentItem.TryGet<ProductPart>(out var productPart))
        {
            unitPrice = ProductVolumePricing.Apply(unitPrice, ProductVolumePricing.GetPercentOff(productPart, Math.Max(1, context.Quantity), GetRoles()));
        }

        return new PriceResult(unitPrice, currency, context.Quantity);
    }

    private async ValueTask<decimal?> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, CancellationToken cancellationToken)
        => _shopperCurrency is null
            ? null
            : await _shopperCurrency.ConvertAsync(amount, fromCurrency, toCurrency, isPrice: true, cancellationToken);

    // A price the product lists in the requested currency, standing in for the one the buyer chose: the same kind of
    // price, billed the same way.
    private static ProductPrice FindInCurrency(ProductPricePart pricing, ProductPrice chosen, string currency, DateTime now)
        => pricing.Prices
            .Where(candidate => string.Equals(candidate.Currency, currency, StringComparison.OrdinalIgnoreCase) &&
                candidate.Kind == chosen.Kind &&
                candidate.Interval == chosen.Interval &&
                candidate.BillingDuration == chosen.BillingDuration &&
                candidate.IsAvailable(now))
            .OrderByDescending(candidate => string.Equals(candidate.Name, chosen.Name, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(candidate => candidate.IsDefault)
            .FirstOrDefault();

    // The signed-in buyer's roles, for role discounts. A request without a buyer, such as a webhook, gets none, but
    // never reprices anyway: an order keeps the prices it was placed at.
    private IEnumerable<string> GetRoles()
        => _httpContextAccessor?.HttpContext?.User is { Identity.IsAuthenticated: true } user
            ? user.FindAll(System.Security.Claims.ClaimTypes.Role).Select(claim => claim.Value)
            : [];

    private async Task<PriceResult> ResolveFromPricesAsync(ProductSnapshotContext context, ISellableProduct product, ProductPricePart pricing, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var price = string.IsNullOrEmpty(context.PriceId)
            ? pricing.GetDefault(now)
            : pricing.Find(context.PriceId);

        if (price is null)
        {
            _logger.LogWarning("Refusing to price product '{ContentItemId}': it offers no price matching '{PriceId}'.", product.ContentItemId, context.PriceId?.SanitizeLogValue() ?? "(default)");

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

        decimal? convertedAmount = null;

        if (!string.IsNullOrEmpty(context.Currency) &&
            !string.Equals(context.Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            // A price the product lists in the shopper's currency is the one they pay; failing that, a one-time price
            // is converted. A custom amount is the buyer's own, in the currency they're paying in.
            if (FindInCurrency(pricing, price, context.Currency, now) is { } listed)
            {
                price = listed;
                currency = listed.Currency;
            }
            else if (price.Kind == PriceKind.Recurring ||
                price.AllowCustomAmount ||
                await ConvertAsync(price.Amount, currency, context.Currency, cancellationToken) is not { } converted)
            {
                _logger.LogWarning("Refusing to price product '{ContentItemId}' in '{RequestedCurrency}': price '{PriceId}' is sold in '{PriceCurrency}' and can't be converted.", product.ContentItemId, context.Currency, price.PriceId, currency);

                return null;
            }
            else
            {
                convertedAmount = converted;
                currency = context.Currency.ToUpperInvariant();
            }
        }

        var amount = convertedAmount ?? price.GetChargeableAmount(context.CustomAmount);

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
