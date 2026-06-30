using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="CallSession"/> documents to the <see cref="CallSessionIndex"/>.
/// </summary>
public sealed class CallSessionIndexProvider : IndexProvider<CallSession>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallSessionIndexProvider"/> class.
    /// </summary>
    public CallSessionIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<CallSession> context)
    {
        context
            .For<CallSessionIndex>()
            .Map(session => new CallSessionIndex
            {
                ItemId = session.ItemId,
                InteractionId = session.InteractionId,
                ActivityItemId = session.ActivityItemId,
                ProviderName = session.ProviderName,
                ProviderCallId = session.ProviderCallId,
                State = session.State,
                AgentId = session.AgentId,
                QueueId = session.QueueId,
                CreatedUtc = session.CreatedUtc,
                EndedUtc = session.EndedUtc,
            });
    }
}
