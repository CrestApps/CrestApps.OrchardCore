using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IAgentProfileStore"/>.
/// </summary>
public sealed class AgentProfileStore : DocumentCatalog<AgentProfile, AgentProfileIndex>, IAgentProfileStore
{
    /// <inheritdoc/>
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentProfileStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public AgentProfileStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<AgentProfile> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return await Session.Query<AgentProfile, AgentProfileIndex>(
            index => index.UserId == userId,
            collection: ContactCenterConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<AgentProfile>> ListAvailableForQueueAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        var normalizedQueueId = queueId.ToLowerInvariant();
        var available = await Session.Query<AgentProfile, AgentQueueMembershipIndex>(
            index => index.QueueId == normalizedQueueId && index.PresenceStatus == AgentPresenceStatus.Available,
            collection: ContactCenterConstants.CollectionName)
            .ListAsync(cancellationToken);

        return available.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<AgentProfile>> ListByPresenceAsync(
        AgentPresenceStatus presenceStatus,
        CancellationToken cancellationToken = default)
    {
        return (await Session.Query<AgentProfile, AgentProfileIndex>(
            index => index.PresenceStatus == presenceStatus,
            collection: ContactCenterConstants.CollectionName)
            .ListAsync(cancellationToken)).ToArray();
    }
}
