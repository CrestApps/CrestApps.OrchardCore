using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// A durable, per-obligation record of a single interaction with a payment provider. It is persisted in
/// the tenant database (never only in a distributed cache) and is the source of truth used to reconcile
/// what actually happened at the provider against what the checkout believes. Persisting it before the
/// provider call, and updating it with the provider's returned reference immediately after, is what
/// guarantees no orphaned charges when a multi-obligation checkout partially fails or a node crashes.
/// </summary>
public sealed class PaymentAttempt : Entity
{
    /// <summary>
    /// The unique identifier of the attempt (26-character generated id).
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// The checkout session this attempt belongs to.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// The key of the payment provider handling the attempt (for example the Stripe processor key).
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// The obligation this attempt settles. For a multi-interval checkout each recurring group and the
    /// one-time amount get their own obligation id, so a partial failure is always attributable.
    /// </summary>
    public string ObligationId { get; set; }

    /// <summary>
    /// The idempotency key sent to the provider so a retried attempt never double-charges.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// The amount the attempt is expected to charge, before tax, in the invoice currency.
    /// </summary>
    public double ExpectedAmount { get; set; }

    /// <summary>
    /// The tax expected to be charged with this attempt, in the invoice currency.
    /// </summary>
    public double ExpectedTaxAmount { get; set; }

    /// <summary>
    /// The ISO-4217 currency code of the attempt.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// The current lifecycle state of the attempt.
    /// </summary>
    public PaymentAttemptState State { get; set; }

    /// <summary>
    /// The provider's authoritative reference for this attempt (for example a PaymentIntent or remote
    /// subscription id). Stored as soon as the provider returns it so the resource is never lost even if a
    /// later step in the same checkout fails.
    /// </summary>
    public string ProviderReference { get; set; }

    /// <summary>
    /// The checkout transaction id recorded in the session's payment metadata once the attempt succeeds.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// The provider mode the attempt ran in (for example test or live).
    /// </summary>
    public GatewayMode GatewayMode { get; set; }

    /// <summary>
    /// The amount the provider confirmed it actually charged, before tax, in the invoice currency. Set
    /// only when the provider's authoritative verification succeeds, so the confirmed payment can always
    /// be rebuilt from the durable ledger without trusting session metadata.
    /// </summary>
    public double ConfirmedAmount { get; set; }

    /// <summary>
    /// The tax the provider confirmed it actually collected with this attempt, in the invoice currency.
    /// </summary>
    public double ConfirmedTaxAmount { get; set; }

    /// <summary>
    /// The immutable tax determination captured when the attempt was confirmed, persisted so historical
    /// tax is never recalculated with current rules.
    /// </summary>
    public TaxSnapshot TaxSnapshot { get; set; }

    /// <summary>
    /// The reason the attempt failed, when <see cref="State"/> is <see cref="PaymentAttemptState.Failed"/>.
    /// </summary>
    public string FailureReason { get; set; }

    /// <summary>
    /// The UTC time the attempt was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// The UTC time the attempt was last updated.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }
}
