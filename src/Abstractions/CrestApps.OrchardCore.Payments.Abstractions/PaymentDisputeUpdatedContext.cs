namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Provides details about a change to a dispute or chargeback at a payment gateway: evidence under review, or the
/// dispute won or lost.
/// </summary>
public sealed class PaymentDisputeUpdatedContext : PaymentEventContextBase
{
    /// <summary>
    /// Gets or sets the provider transaction id of the disputed payment.
    /// </summary>
    public string OriginalTransactionId { get; set; }

    /// <summary>
    /// Gets or sets the gateway's reference of the dispute.
    /// </summary>
    public string DisputeReference { get; set; }

    /// <summary>
    /// Gets or sets the disputed amount.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency of the disputed amount.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets why the payer disputed the payment, in the gateway's words.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the dispute's status, in the gateway's words.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets whether the dispute is over, won or lost.
    /// </summary>
    public bool IsClosed { get; set; }
}
