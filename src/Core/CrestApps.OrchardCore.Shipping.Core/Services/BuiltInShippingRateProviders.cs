using CrestApps.OrchardCore.Shipping.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// One amount per order, plus an optional amount per item.
/// </summary>
public sealed class FlatRateShippingProvider : IShippingRateProvider
{
    internal readonly IStringLocalizer S;

    public FlatRateShippingProvider(IStringLocalizer<FlatRateShippingProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public string Name => ShippingConstants.Providers.Flat;

    public LocalizedString DisplayName => S["Flat rate"];

    public LocalizedString Description => S["One amount per order, plus an optional amount for each item."];

    public Task<IReadOnlyList<ShippingRate>> GetRatesAsync(ShippingRateContext context, CancellationToken cancellationToken = default)
    {
        var settings = context.Method.GetOrCreate<FlatRateSettings>();

        return Task.FromResult<IReadOnlyList<ShippingRate>>([context.CreateRate(settings.Amount + (settings.PerItemAmount * context.Request.ItemCount))]);
    }
}

/// <summary>
/// An amount looked up in a table by the order's weight, subtotal or number of items.
/// </summary>
public sealed class TableRateShippingProvider : IShippingRateProvider
{
    internal readonly IStringLocalizer S;

    public TableRateShippingProvider(IStringLocalizer<TableRateShippingProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public string Name => ShippingConstants.Providers.Table;

    public LocalizedString DisplayName => S["Table rate"];

    public LocalizedString Description => S["An amount looked up by the order's weight, subtotal or number of items."];

    public Task<IReadOnlyList<ShippingRate>> GetRatesAsync(ShippingRateContext context, CancellationToken cancellationToken = default)
    {
        var settings = context.Method.GetOrCreate<TableRateSettings>();
        var amount = Lookup(settings, context.Request);

        return Task.FromResult<IReadOnlyList<ShippingRate>>(amount is { } value ? [context.CreateRate(value)] : []);
    }

    /// <summary>
    /// Gets the amount of the row the request falls in, or <see langword="null"/> when it falls in none.
    /// </summary>
    public static decimal? Lookup(TableRateSettings settings, ShippingRateRequest request)
    {
        var value = settings.Basis switch
        {
            TableRateBasis.Subtotal => request.Subtotal,
            TableRateBasis.Quantity => request.ItemCount,
            _ => request.TotalWeight,
        };

        if (settings.Maximum is { } maximum && value > maximum)
        {
            return null;
        }

        return settings.Rows?
            .Where(row => row.From <= value)
            .OrderByDescending(row => row.From)
            .Select(row => (decimal?)row.Amount)
            .FirstOrDefault();
    }
}

/// <summary>
/// Nothing to pay, optionally only from an order subtotal.
/// </summary>
public sealed class FreeShippingProvider : IShippingRateProvider
{
    internal readonly IStringLocalizer S;

    public FreeShippingProvider(IStringLocalizer<FreeShippingProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public string Name => ShippingConstants.Providers.Free;

    public LocalizedString DisplayName => S["Free shipping"];

    public LocalizedString Description => S["Nothing to pay, optionally only from an order subtotal."];

    public Task<IReadOnlyList<ShippingRate>> GetRatesAsync(ShippingRateContext context, CancellationToken cancellationToken = default)
    {
        var settings = context.Method.GetOrCreate<FreeShippingSettings>();

        return Task.FromResult<IReadOnlyList<ShippingRate>>(context.Request.Subtotal >= settings.MinimumSubtotal ? [context.CreateRate(0)] : []);
    }
}

/// <summary>
/// Delivery by the store itself to the postal codes it covers, for a fee, optionally free from an order subtotal.
/// </summary>
public sealed class LocalDeliveryShippingProvider : IShippingRateProvider
{
    internal readonly IStringLocalizer S;

    public LocalDeliveryShippingProvider(IStringLocalizer<LocalDeliveryShippingProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public string Name => ShippingConstants.Providers.LocalDelivery;

    public LocalizedString DisplayName => S["Local delivery"];

    public LocalizedString Description => S["Delivered by the store to the postal codes it covers, for a fee, optionally free from an order subtotal."];

    public Task<IReadOnlyList<ShippingRate>> GetRatesAsync(ShippingRateContext context, CancellationToken cancellationToken = default)
    {
        var settings = context.Method.GetOrCreate<LocalDeliverySettings>();

        if (!settings.Covers(context.Request.Destination?.PostalCode) || context.Request.Subtotal < settings.MinimumSubtotal)
        {
            return Task.FromResult<IReadOnlyList<ShippingRate>>([]);
        }

        var amount = settings.FreeFromSubtotal is { } free && context.Request.Subtotal >= free ? 0m : settings.Amount;

        return Task.FromResult<IReadOnlyList<ShippingRate>>([context.CreateRate(amount)]);
    }
}

/// <summary>
/// Rates the carriers quote for the order, through the store's shipping account, with an optional markup. When the
/// carriers can't be reached it offers a fallback amount, or nothing.
/// </summary>
public sealed class CarrierShippingRateProvider : IShippingRateProvider
{
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);

    private readonly ICarrierProviderResolver _carriers;
    private readonly ISiteService _siteService;
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public CarrierShippingRateProvider(
        ICarrierProviderResolver carriers,
        ISiteService siteService,
        IMemoryCache cache,
        ILogger<CarrierShippingRateProvider> logger,
        IStringLocalizer<CarrierShippingRateProvider> stringLocalizer)
    {
        _carriers = carriers;
        _siteService = siteService;
        _cache = cache;
        _logger = logger;
        S = stringLocalizer;
    }

    public string Name => ShippingConstants.Providers.Carrier;

    public LocalizedString DisplayName => S["Carrier rates"];

    public LocalizedString Description => S["The carriers' own rates for each order, through your shipping account, with an optional markup."];

    public async Task<IReadOnlyList<ShippingRate>> GetRatesAsync(ShippingRateContext context, CancellationToken cancellationToken = default)
    {
        var settings = context.Method.GetOrCreate<CarrierRateSettings>();
        var shipping = await _siteService.GetSettingsAsync<ShippingSettings>();
        var carrier = await _carriers.GetDefaultAsync();

        if (carrier is null || shipping.ShipFrom is null || context.Request.Destination is null)
        {
            return Fallback(context, settings);
        }

        var request = new CarrierShipmentRequest
        {
            From = shipping.ShipFrom,
            To = context.Request.Destination,
            Parcel = new CarrierParcel
            {
                WeightOunces = Math.Max(0.1m, CarrierParcel.ToOunces(context.Request.TotalWeight + shipping.PackagingWeight, shipping.WeightUnit)),
                LengthInches = CarrierParcel.ToInches(shipping.DefaultParcelLength, shipping.WeightUnit),
                WidthInches = CarrierParcel.ToInches(shipping.DefaultParcelWidth, shipping.WeightUnit),
                HeightInches = CarrierParcel.ToInches(shipping.DefaultParcelHeight, shipping.WeightUnit),
            },
        };

        // The same parcel to the same place is quoted once every few minutes, not on every checkout step.
        var key = $"carrier-quote:{carrier.Name}:{request.To.Country}:{request.To.PostalCode}:{request.To.Region}:{request.Parcel.WeightOunces}";
        var quote = await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheDuration;

            return await carrier.QuoteAsync(request, cancellationToken);
        });

        if (quote is null || !quote.Succeeded)
        {
            _cache.Remove(key);

            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("The carriers couldn't quote shipping method '{MethodId}': {Error}", context.Method.MethodId, quote?.Error);
            }

            return Fallback(context, settings);
        }

        var carriers = (settings.Carriers ?? []).Where(name => !string.IsNullOrWhiteSpace(name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rates = quote.Rates
            .Where(rate => carriers.Count == 0 || carriers.Contains(rate.Carrier))
            .Where(rate => string.IsNullOrEmpty(rate.Currency) || string.IsNullOrEmpty(context.Request.Currency) || string.Equals(rate.Currency, context.Request.Currency, StringComparison.OrdinalIgnoreCase))
            .OrderBy(rate => rate.Amount);

        var offered = (settings.MaxRates > 0 ? rates.Take(settings.MaxRates) : rates)
            .Select(rate =>
            {
                var amount = Math.Round((rate.Amount * (1 + (settings.MarkupPercent / 100m))) + settings.MarkupAmount, 2, MidpointRounding.AwayFromZero);
                var shippingRate = context.CreateRate(amount, $"{rate.Carrier}-{rate.Service}".ToLowerInvariant(), $"{rate.Carrier} {DescribeService(rate.Carrier, rate.Service)}");

                return rate.DeliveryDays is { } days
                    ? new ShippingRate
                    {
                        RateId = shippingRate.RateId,
                        ZoneId = shippingRate.ZoneId,
                        MethodId = shippingRate.MethodId,
                        ProviderName = shippingRate.ProviderName,
                        Name = shippingRate.Name,
                        Description = days == 1 ? S["1 business day"].Value : S["{0} business days", days].Value,
                        Amount = shippingRate.Amount,
                        Currency = shippingRate.Currency,
                        TaxCategoryCode = shippingRate.TaxCategoryCode,
                    }
                    : shippingRate;
            })
            .ToArray();

        return offered.Length > 0 ? offered : Fallback(context, settings);
    }

    private static IReadOnlyList<ShippingRate> Fallback(ShippingRateContext context, CarrierRateSettings settings)
        => settings.FallbackAmount is { } amount ? [context.CreateRate(amount)] : [];

    /// <summary>
    /// Gets a service's name as buyers read it. Carriers name services in their own code, such as "GroundAdvantage",
    /// "2ndDayAir" or "FEDEX_2_DAY_AM", which reads "Ground Advantage", "2nd Day Air" and "2 Day AM" (the carrier's own
    /// name, shown beside it, isn't repeated).
    /// </summary>
    public static string DescribeService(string carrier, string service)
    {
        if (string.IsNullOrWhiteSpace(service))
        {
            return service;
        }

        var text = System.Text.RegularExpressions.Regex.Replace(service.Replace('_', ' '), "(?<=[a-z])(?=[A-Z0-9])|(?<=[0-9])(?=[A-Z][a-z])", " ").Trim();

        // A code in capitals reads in title case, with short words such as AM kept as they are.
        if (!text.Any(char.IsLower))
        {
            text = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Length > 2 && word.All(char.IsLetter) ? char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant() : word));
        }

        if (!string.IsNullOrWhiteSpace(carrier) && text.StartsWith(carrier.Trim() + " ", StringComparison.OrdinalIgnoreCase))
        {
            text = text[(carrier.Trim().Length + 1)..];
        }

        return text.Trim();
    }
}
