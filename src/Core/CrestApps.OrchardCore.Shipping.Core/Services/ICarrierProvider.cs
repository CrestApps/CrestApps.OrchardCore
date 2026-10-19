using CrestApps.OrchardCore.Addresses.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// A carrier provider: a shipping account that quotes the carriers' rates for a parcel and buys their labels, such as
/// EasyPost. Each provider is a feature with its own tab on Settings › Commerce › Shipping, and the store chooses the
/// default one there; shipping methods priced by the carriers and a shipment's Buy label use it, through
/// <see cref="ICarrierProviderResolver"/>.
/// </summary>
public interface ICarrierProvider
{
    /// <summary>
    /// Gets the provider's technical name, which the default provider setting holds.
    /// </summary>
    string Name { get; }

    LocalizedString DisplayName { get; }

    /// <summary>
    /// Returns whether the store enabled the provider and gave it what it needs, such as its API key.
    /// </summary>
    Task<bool> IsEnabledAsync();

    /// <summary>
    /// Gets the carriers' rates for sending the parcel. A failed quote has an error and no rates.
    /// </summary>
    Task<CarrierQuote> QuoteAsync(CarrierShipmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Buys the label of one of a quote's rates.
    /// </summary>
    Task<CarrierLabel> BuyLabelAsync(string quoteId, string rateId, CancellationToken cancellationToken = default);
}

/// <summary>
/// A parcel to send: from where, to where, and how big and heavy.
/// </summary>
public sealed class CarrierShipmentRequest
{
    public Address From { get; init; }

    public Address To { get; init; }

    public CarrierParcel Parcel { get; init; }

    /// <summary>
    /// Gets what the parcel is known by in the store, such as the order number, for the carrier's records.
    /// </summary>
    public string Reference { get; init; }
}

/// <summary>
/// A parcel's weight in ounces and size in inches, the units carriers quote in.
/// </summary>
public sealed class CarrierParcel
{
    public decimal WeightOunces { get; init; }

    public decimal? LengthInches { get; init; }

    public decimal? WidthInches { get; init; }

    public decimal? HeightInches { get; init; }

    /// <summary>
    /// Converts a weight in the store's unit to ounces.
    /// </summary>
    public static decimal ToOunces(decimal weight, Models.WeightUnit unit)
        => Math.Round(unit == Models.WeightUnit.Pound ? weight * 16m : weight * 35.27396m, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Converts a length in the store's unit (centimeters for a store weighing in kilograms, inches otherwise) to inches.
    /// </summary>
    public static decimal? ToInches(decimal? length, Models.WeightUnit unit)
        => length is not > 0 ? null : Math.Round(unit == Models.WeightUnit.Pound ? length.Value : length.Value / 2.54m, 2, MidpointRounding.AwayFromZero);
}

public sealed class CarrierQuote
{
    /// <summary>
    /// Gets the account's id for the quote, which a label is bought from.
    /// </summary>
    public string QuoteId { get; init; }

    public IReadOnlyList<CarrierRate> Rates { get; init; } = [];

    public string Error { get; init; }

    public bool Succeeded
        => string.IsNullOrEmpty(Error);
}

public sealed class CarrierRate
{
    public string RateId { get; init; }

    public string Carrier { get; init; }

    public string Service { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; }

    /// <summary>
    /// Gets how many days delivery takes, or <see langword="null"/> when the carrier doesn't say.
    /// </summary>
    public int? DeliveryDays { get; init; }
}

public sealed class CarrierLabel
{
    public string Error { get; init; }

    public bool Succeeded
        => string.IsNullOrEmpty(Error);

    public string Carrier { get; init; }

    public string Service { get; init; }

    public string TrackingNumber { get; init; }

    /// <summary>
    /// Gets where the label is printed from.
    /// </summary>
    public string LabelUrl { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; }

    /// <summary>
    /// Gets the link the carrier's tracking page is at, or <see langword="null"/>.
    /// </summary>
    public string TrackingUrl { get; init; }
}
