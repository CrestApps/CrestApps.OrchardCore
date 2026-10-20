using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// What to price: where the items go, what they are, and the currency to price them in.
/// </summary>
public sealed class ShippingRateRequest
{
    public Address Destination { get; init; }

    public string Currency { get; init; }

    public IReadOnlyList<ShippingRequestLine> Lines { get; init; } = [];

    /// <summary>
    /// Gets the subtotal of the items shipped.
    /// </summary>
    public decimal Subtotal
        => Lines.Sum(line => line.UnitPrice * line.Quantity);

    /// <summary>
    /// Gets the total weight of the items shipped, in the store's weight unit.
    /// </summary>
    public decimal TotalWeight
        => Lines.Sum(line => line.Weight * line.Quantity);

    /// <summary>
    /// Gets the number of items shipped.
    /// </summary>
    public int ItemCount
        => Lines.Sum(line => line.Quantity);
}

/// <summary>
/// Units of one product, or one of its variants, to ship.
/// </summary>
public sealed class ShippingRequestLine
{
    public string ContentItemId { get; init; }

    public string VariantId { get; init; }

    public string Title { get; init; }

    public int Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    /// <summary>
    /// Gets the weight of one unit, in the store's weight unit.
    /// </summary>
    public decimal Weight { get; init; }

    public string ShippingClass { get; init; }
}

/// <summary>
/// A price for shipping by one method, which the buyer can choose.
/// </summary>
public sealed class ShippingRate
{
    /// <summary>
    /// Gets the id the buyer's choice is kept by: the method's id, followed by the provider's own option when it
    /// offers several.
    /// </summary>
    public string RateId { get; init; }

    public string ZoneId { get; init; }

    public string MethodId { get; init; }

    public string ProviderName { get; init; }

    public string Name { get; init; }

    public string Description { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; }

    public string TaxCategoryCode { get; init; }

    public ShippingRate WithAmount(decimal amount)
        => WithAmount(amount, Currency);

    /// <summary>
    /// Returns the rate at another amount, in another currency, such as converted into a market's.
    /// </summary>
    public ShippingRate WithAmount(decimal amount, string currency)
        => new()
        {
            RateId = RateId,
            ZoneId = ZoneId,
            MethodId = MethodId,
            ProviderName = ProviderName,
            Name = Name,
            Description = Description,
            Amount = amount,
            Currency = currency,
            TaxCategoryCode = TaxCategoryCode,
        };
}
