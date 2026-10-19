using CrestApps.OrchardCore.Shipping.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

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
