using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps each <see cref="AgentProfile"/> to one <see cref="AgentQueueMembershipIndex"/> row per queue the
/// agent is both entitled to and signed in to, so routing can select queue members with a single indexed
/// query. Queue identifiers are stored lower-cased for portable, case-insensitive matching.
/// </summary>
public sealed class AgentQueueMembershipIndexProvider : IndexProvider<AgentProfile>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentQueueMembershipIndexProvider"/> class.
    /// </summary>
    public AgentQueueMembershipIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AgentProfile> context)
    {
        context
            .For<AgentQueueMembershipIndex>()
            .Map(profile => AgentEntitlementUtilities
                .GetEntitledQueueIds(profile)
                .Select(queueId => new AgentQueueMembershipIndex
                {
                    ItemId = profile.ItemId,
                    QueueId = queueId.ToLowerInvariant(),
                    PresenceStatus = profile.PresenceStatus,
                    MaxConcurrentInteractions = profile.MaxConcurrentInteractions,
                }));
    }
}
