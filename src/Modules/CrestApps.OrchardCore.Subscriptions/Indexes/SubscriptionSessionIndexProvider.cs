using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Indexes;

public class SubscriptionSessionIndexProvider : IndexProvider<SubscriptionSession>
{
    public override void Describe(DescribeContext<SubscriptionSession> context)
    {
        context.For<SubscriptionSessionIndex>()
            .Map(session =>
            {
                return new SubscriptionSessionIndex()
                {
                    SessionId = session.SessionId,
                    CompletedUtc = session.CompletedUtc,
                    ModifiedUtc = session.ModifiedUtc,
                    CreatedUtc = session.CreatedUtc,
                    Status = session.Status.ToString(),
                    OwnerId = session.OwnerId,
                    ContentItemId = session.ContentItemId,
                    ContentItemVersionId = session.ContentItemVersionId,
                };
            });
    }
}
