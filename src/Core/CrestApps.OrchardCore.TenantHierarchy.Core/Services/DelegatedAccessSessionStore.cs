using CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Services;

/// <summary>
/// Stores the delegated access sessions in the database of the current parent tenant.
/// </summary>
public sealed class DelegatedAccessSessionStore
{
    private const string Collection = TenantHierarchyConstants.CollectionName;
    private const int MaxSessionsPerQuery = 500;

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegatedAccessSessionStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session of the parent tenant.</param>
    public DelegatedAccessSessionStore(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Finds a session by the hash of its identifier.
    /// </summary>
    /// <param name="sessionHash">The hash of the session identifier.</param>
    public Task<DelegatedAccessSession> FindByHashAsync(string sessionHash)
    {
        if (string.IsNullOrEmpty(sessionHash))
        {
            return Task.FromResult<DelegatedAccessSession>(null);
        }

        return _session.Query<DelegatedAccessSession, DelegatedAccessSessionIndex>(index => index.SessionHash == sessionHash, Collection)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Lists the open sessions that one parent sign-in started.
    /// </summary>
    /// <param name="parentSessionId">The parent sign-in.</param>
    public Task<IReadOnlyList<DelegatedAccessSession>> ListOpenByParentSessionAsync(string parentSessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(parentSessionId);

        return _session.Query<DelegatedAccessSession, DelegatedAccessSessionIndex>(
            index => index.ParentSessionId == parentSessionId && index.IsOpen,
            Collection)
            .Take(MaxSessionsPerQuery)
            .ListAsync();
    }

    /// <summary>
    /// Lists the open sessions of a parent user.
    /// </summary>
    /// <param name="parentUserId">The parent user.</param>
    public Task<IReadOnlyList<DelegatedAccessSession>> ListOpenByUserAsync(string parentUserId)
    {
        ArgumentException.ThrowIfNullOrEmpty(parentUserId);

        return _session.Query<DelegatedAccessSession, DelegatedAccessSessionIndex>(
            index => index.ParentUserId == parentUserId && index.IsOpen,
            Collection)
            .Take(MaxSessionsPerQuery)
            .ListAsync();
    }

    /// <summary>
    /// Lists the open sessions in one child tenant.
    /// </summary>
    /// <param name="childEntryId">The registry entry of the child tenant.</param>
    public Task<IReadOnlyList<DelegatedAccessSession>> ListOpenByChildAsync(string childEntryId)
    {
        ArgumentException.ThrowIfNullOrEmpty(childEntryId);

        return _session.Query<DelegatedAccessSession, DelegatedAccessSessionIndex>(
            index => index.ChildEntryId == childEntryId && index.IsOpen,
            Collection)
            .Take(MaxSessionsPerQuery)
            .ListAsync();
    }

    /// <summary>
    /// Saves a session.
    /// </summary>
    /// <param name="session">The session.</param>
    public Task SaveAsync(DelegatedAccessSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return _session.SaveAsync(session, checkConcurrency: false, Collection);
    }
}
