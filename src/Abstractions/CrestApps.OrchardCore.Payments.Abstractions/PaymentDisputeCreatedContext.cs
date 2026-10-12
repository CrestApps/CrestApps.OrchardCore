namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Provides details about a dispute or chargeback opened against a settled payment at a payment gateway,
/// so features can flag the owning order for manual review. A dispute never silently reverses local state;
/// it records that an operator must reconcile the payment with the gateway.
/// </summary>
public sealed class PaymentDisputeCreatedContext : PaymentEventContextBase
{
    /// <summary>
    /// Gets or sets the provider transaction id of the disputed payment.
    /// </summary>
    public string OriginalTransactionId { get; set; }

    /// <summary>
    /// Gets or sets the provider's authoritative reference for the dispute.
    /// </summary>
    public string DisputeReference { get; set; }

    /// <summary>
    /// Gets or sets the disputed amount reported by the gateway, when available.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency of the disputed payment.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the dispute reason reported by the gateway, when available.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the gateway dispute status, when available.
    /// </summary>
    public string Status { get; set; }
}
