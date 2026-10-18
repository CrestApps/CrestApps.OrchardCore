namespace CrestApps.OrchardCore.Orders.Core.Models;

/// <summary>
/// A dispute, or chargeback, the payer opened with their bank against an order's payment, as the gateway reports it.
/// </summary>
public sealed class OrderDispute
{
    /// <summary>
    /// Gets or sets the gateway's reference of the dispute.
    /// </summary>
    public string Reference { get; set; }

    /// <summary>
    /// Gets or sets the gateway that reported it, such as <c>Stripe</c>.
    /// </summary>
    public string Gateway { get; set; }

    /// <summary>
    /// Gets or sets the disputed amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency of the amount.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets why the payer disputed the payment, in the gateway's words, such as <c>fraudulent</c>.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the dispute's status, in the gateway's words, such as <c>needs_response</c> or <c>won</c>.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets whether the dispute is over.
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// Gets or sets when it was opened.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it last changed.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }
}
