using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class SubscriptionSummaryPartDisplayDriver : ContentPartDisplayDriver<SubscriptionSummaryPart>
{
    private readonly ISession _session;
    private readonly IClock _clock;

    public SubscriptionSummaryPartDisplayDriver(
        ISession session,
        IClock clock)
    {
        _session = session;
        _clock = clock;
    }

    public override IDisplayResult Display(SubscriptionSummaryPart part, BuildPartDisplayContext context)
    {
        return Initialize<SubscriptionSummaryViewModel>(GetDisplayShapeType(context), async model =>
        {
            var now = _clock.UtcNow;

            model.TotalSubscriptions = await _session.QueryIndex<SubscriptionSessionIndex>().CountAsync();
            model.PendingSubscriptions = await _session.QueryIndex<SubscriptionSessionIndex>(x => x.Status == SubscriptionSessionStatus.Pending).CountAsync();
            model.CompletedSubscriptions = await _session.QueryIndex<SubscriptionSessionIndex>(x => x.Status == SubscriptionSessionStatus.Completed).CountAsync();

            // A subscription is considered active when it has no expiration or the expiration is still in the future.
            model.ActiveSubscriptions = await _session.QueryIndex<SubscriptionIndex>(x => x.ExpiresAt == null || x.ExpiresAt > now).CountAsync();

            var succeededTransactions = await _session.QueryIndex<SubscriptionTransactionIndex>(x => x.Status == PaymentStatus.Succeeded).ListAsync();
            model.TotalRevenue = succeededTransactions.Sum(x => x.Amount);
        }).Location("Detail", "Content");
    }
}
