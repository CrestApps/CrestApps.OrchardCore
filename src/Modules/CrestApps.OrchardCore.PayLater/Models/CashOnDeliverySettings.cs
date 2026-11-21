namespace CrestApps.OrchardCore.PayLater.Models;

/// <summary>
/// The site settings of the Cash on delivery payment method.
/// </summary>
public sealed class CashOnDeliverySettings
{
    /// <summary>
    /// Gets or sets whether Cash on delivery is offered, on checkouts of orders that are delivered.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets what the buyer is told at checkout, for example which payments the courier takes.
    /// </summary>
    public string Instructions { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether only a signed-in buyer may pay on delivery. Off by default.
    /// </summary>
    public bool RequireSignedIn { get; set; }

    /// <summary>
    /// Gets or sets the largest order total that may be paid on delivery, or <see langword="null"/> for no limit.
    /// </summary>
    public decimal? MaxOrderAmount { get; set; }
}
