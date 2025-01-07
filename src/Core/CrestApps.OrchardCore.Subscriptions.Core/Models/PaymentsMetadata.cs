namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// This class is stored in the session and keeps track of all payment from subscriptions and other payments.
/// </summary>
public sealed class PaymentsMetadata
{
    /// <summary>
    /// The key is the transactionId of each payment.
    /// </summary>
    public Dictionary<string, PaymentInfo> Payments { get; set; } = [];
}
