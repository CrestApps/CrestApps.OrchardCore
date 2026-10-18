using CrestApps.OrchardCore.Shipping.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// Prices the shipping methods set to it. Shipping comes with flat, table and free providers; a carrier module can add
/// one that asks the carrier for live rates.
/// </summary>
public interface IShippingRateProvider
{
    /// <summary>
    /// Gets the name methods refer to the provider by.
    /// </summary>
    string Name { get; }

    LocalizedString DisplayName { get; }

    LocalizedString Description { get; }

    /// <summary>
    /// Returns the rates the method offers for the request: usually one, none when the method doesn't apply (such as
    /// free shipping below its minimum), or several for a carrier with options.
    /// </summary>
    Task<IReadOnlyList<ShippingRate>> GetRatesAsync(ShippingRateContext context, CancellationToken cancellationToken = default);
}

public sealed class ShippingRateContext
{
    public ShippingRateContext(ShippingZone zone, ShippingMethod method, ShippingRateRequest request)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(request);

        Zone = zone;
        Method = method;
        Request = request;
    }

    public ShippingZone Zone { get; }

    public ShippingMethod Method { get; }

    public ShippingRateRequest Request { get; }

    /// <summary>
    /// Creates the method's rate for an amount.
    /// </summary>
    public ShippingRate CreateRate(decimal amount, string option = null, string name = null)
        => new()
        {
            RateId = string.IsNullOrEmpty(option) ? Method.MethodId : $"{Method.MethodId}:{option}",
            ZoneId = Zone.ItemId,
            MethodId = Method.MethodId,
            ProviderName = Method.ProviderName,
            Name = name ?? Method.Name,
            Description = Method.Description,
            Amount = amount,
            Currency = Request.Currency,
            TaxCategoryCode = Method.TaxCategoryCode,
        };
}
