using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Subscriptions.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Core.Indexes;

/// <summary>
/// The queryable projection of a <see cref="Subscription"/> agreement.
/// </summary>
/// <remarks>
/// Every column here exists to answer a question without loading the whole ledger: which agreement a
/// provider notification belongs to, whose subscriptions to show, what is due to bill tonight, and which
/// past-due agreements have run out of grace.
/// </remarks>
public sealed class SubscriptionRecordIndex : CatalogItemIndex
{
    /// <summary>
    /// The subscription title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// The owner of the agreement.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// The current lifecycle state.
    /// </summary>
    public SubscriptionStatus Status { get; set; }

    /// <summary>
    /// The payment provider that bills the agreement.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// The provider's own identifier, used to correlate a provider notification.
    /// </summary>
    public string ProviderSubscriptionId { get; set; }

    /// <summary>
    /// The kind of thing that was subscribed to.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// The identifier of the thing that was subscribed to.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// The checkout session the agreement came from.
    /// </summary>
    public string CheckoutSessionId { get; set; }

    /// <summary>
    /// The obligation within that checkout, which makes creation idempotent.
    /// </summary>
    public string ObligationId { get; set; }

    /// <summary>
    /// The ISO-4217 currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// The total billed each cycle, including tax.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// The UTC end of the period the customer has paid for.
    /// </summary>
    public DateTime CurrentPeriodEndUtc { get; set; }

    /// <summary>
    /// The UTC time the next cycle is due, or null when nothing more will be billed.
    /// </summary>
    public DateTime? NextBillingUtc { get; set; }

    /// <summary>
    /// The UTC time a past-due agreement stops being honored.
    /// </summary>
    public DateTime? GraceEndsUtc { get; set; }

    /// <summary>
    /// The UTC time the agreement was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// The UTC time the agreement was last changed.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>
/// Maps <see cref="Subscription"/> documents to <see cref="SubscriptionRecordIndex"/> rows.
/// </summary>
public sealed class SubscriptionRecordIndexProvider : IndexProvider<Subscription>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionRecordIndexProvider"/> class.
    /// </summary>
    public SubscriptionRecordIndexProvider()
    {
        CollectionName = SubscriptionConstants.SubscriptionCollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<Subscription> context)
    {
        context.For<SubscriptionRecordIndex>()
            .Map(subscription => new SubscriptionRecordIndex
            {
                ItemId = subscription.ItemId,
                Title = subscription.Title,
                OwnerId = subscription.OwnerId,
                Status = subscription.Status,
                ProviderKey = subscription.ProviderKey,
                ProviderSubscriptionId = subscription.ProviderSubscriptionId,
                ReferenceType = subscription.ReferenceType,
                ReferenceId = subscription.ReferenceId,
                CheckoutSessionId = subscription.CheckoutSessionId,
                ObligationId = subscription.ObligationId,
                Currency = subscription.Currency,
                TotalAmount = subscription.TotalAmount,
                CurrentPeriodEndUtc = subscription.CurrentPeriodEndUtc,
                NextBillingUtc = subscription.NextBillingUtc,
                GraceEndsUtc = subscription.GraceEndsUtc,
                CreatedUtc = subscription.CreatedUtc,
                UpdatedUtc = subscription.UpdatedUtc,
            });
    }
}
