namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Provides common payment gateway information for payment event contexts.
/// </summary>
public class PaymentEventContextBase
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
    /// Gets the additional gateway-specific data associated with the event.
    /// </summary>
    public Dictionary<string, object> Data { get; } = new(StringComparer.OrdinalIgnoreCase);
}
