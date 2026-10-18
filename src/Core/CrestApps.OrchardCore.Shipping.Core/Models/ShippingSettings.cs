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
