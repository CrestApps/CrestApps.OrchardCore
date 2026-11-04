using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// A money operation that failed and that an operator must finish by hand, such as a refund the checkout could not
/// issue or a gateway subscription that could not be canceled. It is kept in the tenant database and listed under
/// <b>Commerce › Payment issues</b> until someone resolves it, so it is not lost in the log.
/// </summary>
public sealed class PaymentIssue : CatalogItem
{
    /// <summary>
    /// Gets or sets the YesSql document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets what failed, one of <see cref="PaymentIssueKinds"/> or a kind of another module.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets what the operator has to do, in plain words.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the technical reason it failed, such as the provider's error.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets the kind of record it is about, such as <c>CheckoutSession</c> or <c>Subscription</c>.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the ID of the record it is about.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets the payment provider involved, if any.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets the provider's reference involved, such as the payment or subscription at the gateway.
    /// </summary>
    public string ProviderReference { get; set; }

    /// <summary>
    /// Gets or sets the amount at stake, if known.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency of <see cref="Amount"/>.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets whether it is still open.
    /// </summary>
    public PaymentIssueStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when it was recorded, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when an operator resolved it, in UTC.
    /// </summary>
    public DateTime? ResolvedUtc { get; set; }

    /// <summary>
    /// Gets or sets the user name of the operator who resolved it.
    /// </summary>
    public string ResolvedBy { get; set; }

    /// <summary>
    /// Gets or sets what the operator wrote when resolving it, such as the reference of a manual refund.
    /// </summary>
    public string ResolutionNote { get; set; }
}
