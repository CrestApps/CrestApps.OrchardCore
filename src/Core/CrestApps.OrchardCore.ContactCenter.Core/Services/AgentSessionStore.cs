using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IAgentSessionStore"/>.
/// </summary>
public sealed class AgentSessionStore : DocumentCatalog<AgentSession, AgentSessionIndex>, IAgentSessionStore
{
    private const int QueryBatchSize = 500;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentSessionStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public AgentSessionStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<AgentSession> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return await Session.Query<AgentSession, AgentSessionIndex>(
            index => index.UserId == userId,
            collection: ContactCenterConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<AgentSession>> ListStaleAsync(DateTime heartbeatCutoffUtc, CancellationToken cancellationToken = default)
    {
        var stale = await Session.Query<AgentSession, AgentSessionIndex>(
            index => index.IsOnline && index.LastHeartbeatUtc < heartbeatCutoffUtc,
            collection: ContactCenterConstants.CollectionName)
            .ListAsync(cancellationToken);

        return stale.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<AgentSession>> ListByUserIdsAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
        {
            return [];
        }

        var sessions = new List<AgentSession>();

        foreach (var userIdBatch in userIds.Chunk(QueryBatchSize))
        {
            sessions.AddRange(await Session.Query<AgentSession, AgentSessionIndex>(
                index => index.UserId.IsIn(userIdBatch),
                collection: ContactCenterConstants.CollectionName)
                .ListAsync(cancellationToken));
        }

        return sessions;
    }
}
