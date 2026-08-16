namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents a request to create a Stripe recurring price.
/// </summary>
public class CreatePriceRequest
{
    /// <summary>
    /// Gets or sets the Stripe product identifier that owns the price.
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// Gets or sets the lookup key used to find the price later.
    /// </summary>
    public string LookupKey { get; set; }

    /// <summary>
    /// Gets or sets the display title stored as the Stripe price nickname.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the recurring amount in major currency units.
    /// </summary>
    public double? Amount { get; set; }

    /// <summary>
    /// Gets or sets the ISO currency code for the price.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the recurring interval. Valid values are <c>month</c>, <c>year</c>, <c>week</c>, and <c>day</c>.
    /// </summary>
    public string Interval { get; set; }

    /// <summary>
    /// Gets or sets the number of intervals between billings.
    /// </summary>
    public int? IntervalCount { get; set; }
}
