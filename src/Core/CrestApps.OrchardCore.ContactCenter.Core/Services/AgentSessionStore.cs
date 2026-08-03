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
    /// The maximum number of stale sessions a single cleanup pass reads.
    /// </summary>
    public const int MaxStaleSessionsPerPass = 500;

    /// <summary>
    /// Gets a value indicating that agent session updates use YesSql document-version concurrency checks so
    /// concurrent connect, heartbeat, and disconnect operations cannot lose active-session state. A losing
    /// writer observes a <see cref="ConcurrencyException"/> instead of silently overwriting a newer commit.
    /// </summary>
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentSessionStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public AgentSessionStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<AgentSession> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return await Session.Query<AgentSession, AgentSessionIndex>(
            index => index.UserId == userId,
            collection: ContactCenterStorage.CollectionName)
            .OrderByDescending(index => index.IsOnline)
            .ThenByDescending(index => index.LastHeartbeatUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<AgentSession>> ListStaleAsync(DateTime heartbeatCutoffUtc, CancellationToken cancellationToken = default)
    {
        // Read the index alone rather than the documents, and order the read explicitly. A YesSql document
        // query always groups by document identity, and no ordering over the index columns can satisfy that
        // grouping, so bounding one makes the engine materialize and sort every stale session before it can
        // honor the limit: the cost of a pass would still grow with the whole backlog rather than with the page
        // it takes. Worse, a bound on a document query is not free to add — YesSql supplies an ordering by
        // document identity when a page is asked for and none is given, so the sort appears whether or not it
        // is wanted. An index query carries no such grouping, so ordering by the heartbeat time is answered by
        // the retention index that leads with it and the limit stops the read early.
        //
        // Bounded on purpose. The caller takes a distributed lock, re-reads and deletes for every session this
        // returns, so the cost of one cleanup pass is set by the size of the page. An unbounded read makes a
        // single incident — a deployment restart that drops every connection at once — hand the pass every
        // session in the tenant, and it runs on a schedule, so the pass would still be working when the next one
        // starts. The oldest heartbeats come first, so consecutive passes drain the backlog instead of
        // re-reading the same page, and what is not expired now is expired by the next pass a minute later,
        // which is already the resolution this cleanup has.
        var candidates = await Session.QueryIndex<AgentSessionIndex>(
            index => index.IsOnline && index.LastHeartbeatUtc < heartbeatCutoffUtc,
            collection: ContactCenterStorage.CollectionName)
            .OrderBy(index => index.LastHeartbeatUtc)
            .Take(MaxStaleSessionsPerPass)
            .ListAsync(cancellationToken);

        var userIds = candidates
            .Select(candidate => candidate.UserId)
            .Where(userId => !string.IsNullOrEmpty(userId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (userIds.Length == 0)
        {
            return [];
        }

        return await ListByUserIdsAsync(userIds, cancellationToken);
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
                collection: ContactCenterStorage.CollectionName)
                .ListAsync(cancellationToken));
        }

        return sessions;
    }
}
