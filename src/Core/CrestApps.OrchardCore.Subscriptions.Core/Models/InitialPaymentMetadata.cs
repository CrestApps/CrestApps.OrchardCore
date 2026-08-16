using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Stores metadata for the initial payment collected before a subscription is completed.
/// </summary>
public class InitialPaymentMetadata
{
    /// <summary>
    /// Gets or sets the local transaction identifier for the initial payment.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the amount collected for the initial payment.
    /// </summary>
    public double? Amount { get; set; }

    /// <summary>
    /// Gets or sets the ISO currency code used for the initial payment.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the payment gateway identifier used for the initial payment.
    /// </summary>
    public string GatewayId { get; set; }

    /// <summary>
    /// Gets or sets the environment mode used by the payment gateway.
    /// </summary>
    public GatewayMode GatewayMode { get; set; }
}
