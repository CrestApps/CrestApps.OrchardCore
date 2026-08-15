namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The collection of confirmed payments recorded on a checkout session, keyed by provider transaction id.
/// </summary>
public sealed class PaymentsMetadata
{
    /// <summary>
    /// The confirmed payments, keyed by their <see cref="PaymentRecord.TransactionId"/>.
    /// </summary>
    public Dictionary<string, PaymentRecord> Payments { get; set; } = [];
}
