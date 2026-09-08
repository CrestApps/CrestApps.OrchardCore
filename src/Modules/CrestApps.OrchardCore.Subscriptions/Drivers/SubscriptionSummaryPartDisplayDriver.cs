using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using YesSql;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Provides the display shape for the subscription summary content part.
/// </summary>
public sealed class SubscriptionSummaryPartDisplayDriver : ContentPartDisplayDriver<SubscriptionSummaryPart>
{
    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionSummaryPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="session">The YesSql session used to query subscription indexes.</param>
    /// <param name="clock">The clock used to evaluate active subscriptions.</param>
    public SubscriptionSummaryPartDisplayDriver(
        ISession session,
        IClock clock)
    {
        _session = session;
        _clock = clock;
    }

    /// <summary>
    /// Builds the subscription summary display by querying subscription counts and revenue.
    /// </summary>
    /// <param name="part">The subscription summary content part.</param>
    /// <param name="context">The content part display context.</param>
    /// <returns>The display result for the subscription summary part.</returns>
    public override IDisplayResult Display(SubscriptionSummaryPart part, BuildPartDisplayContext context)
    {
        return Initialize<SubscriptionSummaryViewModel>(GetDisplayShapeType(context), async model =>
        {
            // Every figure is read from the durable agreement and the payment ledger. A checkout session
            // records what somebody was asked to pay, so counting sessions as subscriptions and their totals
            // as revenue would report every abandoned checkout as a paying customer.
            model.TotalSubscriptions = await _session.QueryIndex<SubscriptionRecordIndex>().CountAsync();
            model.PendingSubscriptions = await _session.QueryIndex<SubscriptionRecordIndex>(index => index.Status == SubscriptionStatus.Incomplete).CountAsync();
            model.CompletedSubscriptions = await _session.QueryIndex<SubscriptionRecordIndex>(index => index.Status != SubscriptionStatus.Incomplete).CountAsync();

            model.ActiveSubscriptions = await _session.QueryIndex<SubscriptionRecordIndex>(index =>
                index.Status == SubscriptionStatus.Active ||
                index.Status == SubscriptionStatus.Trialing ||
                index.Status == SubscriptionStatus.PastDue).CountAsync();

            var confirmed = await _session.QueryIndex<PaymentAttemptIndex>(index =>
                index.ReferenceType == SubscriptionCheckout.ReferenceType &&
                index.State == PaymentAttemptState.Succeeded).ListAsync();

            model.TotalRevenue = confirmed.Sum(attempt => attempt.ConfirmedAmount);
        }).Location("Detail", "Content");
    }
}
