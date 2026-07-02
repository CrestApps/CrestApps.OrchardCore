using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="AgentSession"/> documents to the <see cref="AgentSessionIndex"/>.
/// </summary>
public sealed class AgentSessionIndexProvider : IndexProvider<AgentSession>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentSessionIndexProvider"/> class.
    /// </summary>
    public AgentSessionIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AgentSession> context)
    {
        context
            .For<AgentSessionIndex>()
            .Map(session => new AgentSessionIndex
            {
                ItemId = session.ItemId,
                UserId = session.UserId,
                IsOnline = session.IsOnline,
                LastHeartbeatUtc = session.LastHeartbeatUtc,
            });
    }
}
