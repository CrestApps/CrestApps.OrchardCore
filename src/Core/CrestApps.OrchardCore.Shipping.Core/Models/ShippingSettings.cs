namespace CrestApps.OrchardCore.Shipping.Models;

/// <summary>
/// Settings › Commerce › Shipping.
/// </summary>
public sealed class ShippingSettings
{
    /// <summary>
    /// Gets or sets the unit product weights and weight tables are in.
    /// </summary>
    public WeightUnit WeightUnit { get; set; } = WeightUnit.Kilogram;

    /// <summary>
    /// Gets or sets the shipping classes products can be in, such as <em>Bulky</em>, which methods can charge extra for.
    /// </summary>
    public List<string> ShippingClasses { get; set; } = [];

    /// <summary>
    /// Gets or sets the carriers shipments are sent with, each with the link its parcels are tracked at.
    /// </summary>
    public List<ShippingCarrier> Carriers { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the buyer gets an email, with the tracking link, when a shipment is marked shipped.
    /// </summary>
    public bool SendShippedEmails { get; set; } = true;

    /// <summary>
    /// Gets or sets the store's locations buyers can pick their order up from, instead of having it shipped.
    /// </summary>
    public List<PickupLocation> PickupLocations { get; set; } = [];

    /// <summary>
    /// Gets or sets where parcels are sent from, which carriers quote and print labels for.
    /// </summary>
    public CrestApps.OrchardCore.Addresses.Models.Address ShipFrom { get; set; }

    /// <summary>
    /// Gets or sets the size of the box most orders go in, in centimeters for a store weighing in kilograms and inches
    /// otherwise, which carriers quote with.
    /// </summary>
    public decimal? DefaultParcelLength { get; set; }

    public decimal? DefaultParcelWidth { get; set; }

    public decimal? DefaultParcelHeight { get; set; }

    /// <summary>
    /// Gets or sets the weight of the box and packing, added to the items' weight, in the store's weight unit.
    /// </summary>
    public decimal PackagingWeight { get; set; }
}

/// <summary>
/// A place buyers pick orders up from, such as a shop, with what to know when they come and what it costs.
/// </summary>
public sealed class PickupLocation
{
    public string LocationId { get; set; }

    public string Name { get; set; }

    public string AddressLine1 { get; set; }

    public string City { get; set; }

    public string Region { get; set; }

    public string PostalCode { get; set; }

    /// <summary>
    /// Gets or sets the two-letter country code.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Gets or sets what the buyer is told, such as opening hours or "ready in 2 hours".
    /// </summary>
    public string Instructions { get; set; }

    public decimal Fee { get; set; }

    /// <summary>
    /// Gets or sets the inventory location the order's units come from, or <see langword="null"/> to let inventory
    /// choose.
    /// </summary>
    public string StockLocationId { get; set; }

    /// <summary>
    /// Gets the location as an address, which taxes and the order's pages read.
    /// </summary>
    public CrestApps.OrchardCore.Addresses.Models.Address ToAddress()
        => new()
        {
            Name = Name,
            AddressLine1 = AddressLine1,
            City = City,
            Region = Region,
            PostalCode = PostalCode,
            Country = Country,
        };

    /// <summary>
    /// Gets the location's address on one line.
    /// </summary>
    public string Describe()
        => string.Join(", ", new[] { AddressLine1, City, string.Join(" ", new[] { Region, PostalCode }.Where(value => !string.IsNullOrWhiteSpace(value))) }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
}

/// <summary>
/// A carrier, such as UPS, and the link a tracking number is followed at.
/// </summary>
public sealed class ShippingCarrier
{
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the tracking link, with <c>{0}</c> where the tracking number goes.
    /// </summary>
    public string TrackingUrlTemplate { get; set; }

    /// <summary>
    /// Gets the tracking link for a number, or <see langword="null"/> without a template.
    /// </summary>
    public string GetTrackingUrl(string trackingNumber)
        => string.IsNullOrWhiteSpace(TrackingUrlTemplate) || string.IsNullOrWhiteSpace(trackingNumber)
        ? null
        : TrackingUrlTemplate.Replace("{0}", Uri.EscapeDataString(trackingNumber.Trim()), StringComparison.Ordinal);
}

public enum WeightUnit
{
    Kilogram = 0,
    Pound = 1,
}
