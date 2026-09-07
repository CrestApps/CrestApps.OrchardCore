using CrestApps.Core.Models;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Payments.Models;

namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// A durable record of an agreement to bill a customer on a recurring cycle.
/// </summary>
/// <remarks>
/// Until this existed, "a subscription" was whatever a completed checkout session happened to contain. That
/// made every ordinary question expensive or impossible: who is subscribed right now, whose payment failed
/// last night, what does this customer owe next month, and what should happen when they cancel. A checkout
/// session records how something was bought once; this records what the customer is entitled to from now on,
/// and it outlives the checkout that created it.
/// </remarks>
public sealed class Subscription : CatalogItem
{
    /// <summary>
    /// Gets or sets the YesSql document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the display title, usually the plan the customer bought.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who owns the subscription.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets whether the owner is an authenticated user or a guest.
    /// </summary>
    public CustomerOwnerKind OwnerKind { get; set; }

    /// <summary>
    /// Gets or sets the guest's name, when the subscription was bought without an account.
    /// </summary>
    public string GuestContactName { get; set; }

    /// <summary>
    /// Gets or sets the guest's email address, which is the only way to reach a guest subscriber.
    /// </summary>
    public string GuestContactEmail { get; set; }

    /// <summary>
    /// Gets or sets the kind of thing that was subscribed to, for example a subscription plan content type.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the thing that was subscribed to.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets the version identifier of the thing that was subscribed to, so a later edit of the plan
    /// does not silently change what an existing subscriber agreed to.
    /// </summary>
    public string ReferenceVersionId { get; set; }

    /// <summary>
    /// Gets or sets the checkout session the subscription was created from.
    /// </summary>
    public string CheckoutSessionId { get; set; }

    /// <summary>
    /// Gets or sets the obligation within that checkout this subscription settles.
    /// </summary>
    public string ObligationId { get; set; }

    /// <summary>
    /// Gets or sets the key of the payment provider that bills the agreement.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets the provider's own identifier for the agreement, for example a Stripe subscription id.
    /// It is how a provider notification is correlated back to this record.
    /// </summary>
    public string ProviderSubscriptionId { get; set; }

    /// <summary>
    /// Gets or sets the provider's customer identifier, so a later change can be made against the same
    /// customer rather than creating a second one.
    /// </summary>
    public string ProviderCustomerId { get; set; }

    /// <summary>
    /// Gets or sets whether the agreement lives in the provider's live or test mode.
    /// </summary>
    public GatewayMode GatewayMode { get; set; }

    /// <summary>
    /// Gets or sets the current lifecycle state.
    /// </summary>
    public SubscriptionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency the agreement bills in.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the amount billed each cycle before tax.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the tax billed each cycle.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets the number of <see cref="DurationType"/> units in one billing cycle.
    /// </summary>
    public int BillingDuration { get; set; }

    /// <summary>
    /// Gets or sets the unit of time <see cref="BillingDuration"/> is expressed in.
    /// </summary>
    public DurationType DurationType { get; set; }

    /// <summary>
    /// Gets or sets the number of cycles the customer agreed to, when the agreement is capped.
    /// </summary>
    public int? BillingCycleLimit { get; set; }

    /// <summary>
    /// Gets or sets how many cycles have been billed so far.
    /// </summary>
    public int CyclesBilled { get; set; }

    /// <summary>
    /// Gets or sets the UTC start of the current billing period.
    /// </summary>
    public DateTime CurrentPeriodStartUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC end of the period the customer has paid for. Access is owed until this moment
    /// even after a cancellation, which is why it is kept separately from the status.
    /// </summary>
    public DateTime CurrentPeriodEndUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the next cycle should be billed. It is what the renewal sweep queries.
    /// </summary>
    public DateTime? NextBillingUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC end of a trial, when the agreement started with one.
    /// </summary>
    public DateTime? TrialEndsUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the most recent renewal payment failed. Dunning is measured from here.
    /// </summary>
    public DateTime? PastDueSinceUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the grace period for an unpaid renewal ends. Past it the subscription
    /// expires rather than staying past due forever.
    /// </summary>
    public DateTime? GraceEndsUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the agreement stops when the paid period ends. It is kept
    /// separate from a canceled status because a customer who cancels mid-cycle keeps access they paid for.
    /// </summary>
    public bool CancelAtPeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the agreement was canceled.
    /// </summary>
    public DateTime? CanceledUtc { get; set; }

    /// <summary>
    /// Gets or sets the reason recorded for the cancellation.
    /// </summary>
    public string CancellationReason { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the agreement was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the agreement was last changed.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// Gets the lines that make up what is billed each cycle.
    /// </summary>
    public IList<SubscriptionLine> Lines { get; init; } = [];

    /// <summary>
    /// Gets the audit trail of everything that happened to the agreement.
    /// </summary>
    public IList<SubscriptionEvent> Events { get; init; } = [];

    /// <summary>
    /// Gets the entitlements this subscription grants while it is current.
    /// </summary>
    public IList<SubscriptionEntitlement> Entitlements { get; init; } = [];

    /// <summary>
    /// Gets the total billed each cycle, including tax.
    /// </summary>
    public decimal TotalAmount
        => Amount + TaxAmount;

    /// <summary>
    /// Returns whether the subscription currently entitles the owner to what it grants.
    /// </summary>
    /// <param name="utcNow">The current UTC time.</param>
    /// <remarks>
    /// A canceled subscription is still current until the period the customer paid for runs out, and a past
    /// due one is current through its grace window. Treating either as immediately revoked takes away access
    /// somebody paid for.
    /// </remarks>
    public bool IsCurrent(DateTime utcNow)
        => Status switch
        {
            SubscriptionStatus.Active or SubscriptionStatus.Trialing => true,
            SubscriptionStatus.PastDue => GraceEndsUtc is null || utcNow <= GraceEndsUtc.Value,
            SubscriptionStatus.Canceled => utcNow <= CurrentPeriodEndUtc,
            _ => false,
        };

    /// <summary>
    /// Advances a UTC date by one billing cycle.
    /// </summary>
    /// <param name="from">The date to advance from.</param>
    public DateTime Advance(DateTime from)
        => DurationType switch
        {
            DurationType.Day => from.AddDays(Math.Max(1, BillingDuration)),
            DurationType.Week => from.AddDays(Math.Max(1, BillingDuration) * 7),
            DurationType.Month => from.AddMonths(Math.Max(1, BillingDuration)),
            DurationType.Year => from.AddYears(Math.Max(1, BillingDuration)),
            _ => from.AddMonths(Math.Max(1, BillingDuration)),
        };
}
