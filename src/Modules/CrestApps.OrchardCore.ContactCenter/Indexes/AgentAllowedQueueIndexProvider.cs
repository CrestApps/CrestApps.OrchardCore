using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps each <see cref="AgentProfile"/> to one <see cref="AgentAllowedQueueIndex"/> row per queue the agent is a
/// member of (its allowed queues and any queues it is signed in to), independent of presence, so a routed SMS
/// selection can enumerate a queue's members with a single indexed query. Queue identifiers are stored
/// lower-cased for portable, case-insensitive matching.
/// </summary>
public sealed class AgentAllowedQueueIndexProvider : IndexProvider<AgentProfile>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentAllowedQueueIndexProvider"/> class.
    /// </summary>
    public AgentAllowedQueueIndexProvider()
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AgentProfile> context)
    {
        context
            .For<AgentAllowedQueueIndex>()
            .Map(BuildRows);
    }

    /// <summary>
    /// Builds one index row per distinct queue the agent is a member of (its allowed queues and any queues it is
    /// signed in to), with queue identifiers lower-cased for case-insensitive matching.
    /// </summary>
    /// <param name="profile">The agent profile.</param>
    /// <returns>The index rows for the profile.</returns>
    public static IEnumerable<AgentAllowedQueueIndex> BuildRows(AgentProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return (profile.AllowedQueueIds ?? [])
            .Concat(profile.QueueIds ?? [])
            .Where(queueId => !string.IsNullOrEmpty(queueId))
            .Select(queueId => queueId.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Select(queueId => new AgentAllowedQueueIndex
            {
                ItemId = profile.ItemId,
                QueueId = queueId,
            });
    }
}
