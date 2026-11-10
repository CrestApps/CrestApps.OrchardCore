using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Commerce.Stores;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Products.Core.Feeds;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// Gives product feed items their weight and size from the Shipping part, and what shipping one unit costs at the least
/// to the store's own country. Only the rates Shipping works out itself (flat, table and free) count: a carrier's live
/// rates would mean a call to the carrier for every item.
/// </summary>
internal sealed class ShippingProductFeedContributor : IProductFeedItemContributor
{
    private static readonly HashSet<string> _localProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        ShippingConstants.Providers.Flat,
        ShippingConstants.Providers.Table,
        ShippingConstants.Providers.Free,
    };

    private readonly ISiteService _siteService;
    private readonly IShippingRateService _rateService;
    private readonly IEnumerable<IShippingRateProvider> _providers;
    private readonly IShopperCurrency _shopperCurrency;

    private ShippingSettings _settings;
    private ShippingZone _zone;
    private Address _destination;
    private string _storeCurrency;
    private bool _loaded;

    public ShippingProductFeedContributor(
        ISiteService siteService,
        IShippingRateService rateService,
        IEnumerable<IShippingRateProvider> providers,
        IShopperCurrency shopperCurrency)
    {
        _siteService = siteService;
        _rateService = rateService;
        _providers = providers;
        _shopperCurrency = shopperCurrency;
    }

    public async Task ContributeAsync(ProductFeedItemContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A feed is built in one scope, so the store's settings and zone are read once for all its items.
        await LoadAsync(cancellationToken);

        var item = context.Item;
        var part = context.Product.TryGet<ShippingPart>(out var shippingPart) ? shippingPart : null;
        var variantId = context.Variant?.VariantId;

        if (part is not null)
        {
            if (part.HasWeight(variantId))
            {
                item.Weight = part.GetWeight(variantId);
                item.WeightUnit = _settings.WeightUnit == WeightUnit.Pound ? "lb" : "kg";
            }

            if (part.GetDimensions(variantId) is { } dimensions)
            {
                item.Length = dimensions.Length;
                item.Width = dimensions.Width;
                item.Height = dimensions.Height;
                item.DimensionsUnit = ShippingDimensions.GetUnit(_settings.WeightUnit);
            }
        }

        if (context.ProductType is ProductType.Digital or ProductType.Service || _zone is null || string.IsNullOrEmpty(item.Currency))
        {
            return;
        }

        item.ShippingPrice = await GetCheapestRateAsync(context, part, cancellationToken);
    }

    private async Task<decimal?> GetCheapestRateAsync(ProductFeedItemContext context, ShippingPart part, CancellationToken cancellationToken)
    {
        var item = context.Item;
        var request = new ShippingRateRequest
        {
            Destination = _destination,
            Currency = item.Currency,
            Lines =
            [
                new ShippingRequestLine
                {
                    ContentItemId = context.Product.ContentItemId,
                    VariantId = context.Variant?.VariantId,
                    Title = item.Title,
                    Quantity = 1,
                    UnitPrice = item.SalePrice ?? item.Price,
                    Weight = part?.GetWeight(context.Variant?.VariantId) ?? 0m,
                    ShippingClass = part?.ShippingClass,
                },
            ],
        };

        decimal? cheapest = null;

        foreach (var method in _zone.Methods.Where(method => method.Enabled && _localProviders.Contains(method.ProviderName ?? string.Empty)))
        {
            // A method prices only orders in its currency; one without a currency is in the store's.
            var methodCurrency = string.IsNullOrEmpty(method.Currency) ? _storeCurrency : method.Currency;

            if (!string.Equals(methodCurrency, item.Currency, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var provider = _providers.FirstOrDefault(provider => string.Equals(provider.Name, method.ProviderName, StringComparison.OrdinalIgnoreCase));

            if (provider is null)
            {
                continue;
            }

            var surcharge = DefaultShippingRateService.GetSurcharge(method, request);

            foreach (var rate in await provider.GetRatesAsync(new ShippingRateContext(_zone, method, request), cancellationToken))
            {
                var amount = Money.Round(Math.Max(0, rate.Amount + surcharge), item.Currency);

                cheapest = cheapest is null ? amount : Math.Min(cheapest.Value, amount);
            }
        }

        return cheapest;
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        _settings = await _siteService.GetSettingsAsync<ShippingSettings>();

        var country = _settings.ShipFrom?.Country;

        if (string.IsNullOrWhiteSpace(country))
        {
            country = (await _siteService.GetStoreProfileAsync()).Address?.Country;
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            return;
        }

        _destination = new Address { Country = country.Trim().ToUpperInvariant() };
        _zone = await _rateService.FindZoneAsync(_destination, cancellationToken);
        _storeCurrency = await _shopperCurrency.GetStoreCurrencyAsync(cancellationToken);
    }
}
