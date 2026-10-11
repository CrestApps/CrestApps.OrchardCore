using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Core.Indexes;

/// <summary>
/// The queryable projection of a <see cref="PaymentAttempt"/>, the durable payment ledger.
/// </summary>
public sealed class PaymentAttemptIndex : CatalogItemIndex
{
    /// <summary>
    /// The checkout session id the attempt belongs to.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// The payment provider key.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// The obligation the attempt settles.
    /// </summary>
    public string ObligationId { get; set; }

    /// <summary>
    /// The idempotency key used with the provider.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// The provider's authoritative reference.
    /// </summary>
    public string ProviderReference { get; set; }

    /// <summary>
    /// The lifecycle state of the attempt.
    /// </summary>
    public PaymentAttemptState State { get; set; }

    /// <summary>
    /// The UTC time the attempt was last updated.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// The moment the attempt was created. Reporting buckets money by when it was taken, so this is the
    /// column every revenue report ranges over.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// The currency the attempt was made in. Amounts in different currencies are never summed together.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// The amount the provider actually confirmed, excluding tax. It is deliberately the confirmed amount
    /// rather than the expected one: a report of money taken must not count money that was only asked for.
    /// </summary>
    public decimal ConfirmedAmount { get; set; }

    /// <summary>
    /// The tax the provider actually confirmed.
    /// </summary>
    public decimal ConfirmedTaxAmount { get; set; }

    /// <summary>
    /// The reference type of the checkout the attempt belongs to, denormalized so a report can select the
    /// attempts for one kind of purchase without loading every session.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// The reference id of the checkout the attempt belongs to.
    /// </summary>
    public string ReferenceId { get; set; }
}
