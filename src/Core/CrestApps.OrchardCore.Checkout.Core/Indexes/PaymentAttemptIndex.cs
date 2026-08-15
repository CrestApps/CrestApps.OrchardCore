using CrestApps.OrchardCore.Checkout.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Checkout.Core.Indexes;

/// <summary>
/// The queryable projection of a <see cref="PaymentAttempt"/>, the durable payment ledger.
/// </summary>
public sealed class PaymentAttemptIndex : MapIndex
{
    /// <summary>
    /// The attempt id.
    /// </summary>
    public string AttemptId { get; set; }

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
}
