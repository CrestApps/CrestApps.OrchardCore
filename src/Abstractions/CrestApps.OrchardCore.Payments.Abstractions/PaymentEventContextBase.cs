namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Provides common payment gateway information for payment event contexts.
/// </summary>
public abstract class PaymentEventContextBase
{
    /// <summary>
    /// Gets or sets the identifier of the payment gateway that raised the event.
    /// </summary>
    public string GatewayId { get; set; }

    /// <summary>
    /// Gets or sets the environment mode used by the payment gateway that raised the event.
    /// </summary>
    public GatewayMode GatewayMode { get; set; }

    /// <summary>
    /// Gets or sets the gateway's identifier of the event, the same on every delivery of it, so a handler whose effect
    /// lives outside the database, such as a counter in a cache, acts on it once. <see langword="null"/> when the gateway
    /// gives none.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets the additional gateway-specific data associated with the event.
    /// </summary>
    public Dictionary<string, object> Data { get; } = new(StringComparer.OrdinalIgnoreCase);
}
