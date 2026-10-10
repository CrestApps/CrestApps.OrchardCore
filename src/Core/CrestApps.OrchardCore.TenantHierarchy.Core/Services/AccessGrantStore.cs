using CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Services;

/// <summary>
/// Stores the access grants in the database of the current parent tenant.
/// </summary>
public sealed class AccessGrantStore
{
    private const string Collection = TenantHierarchyConstants.CollectionName;

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccessGrantStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session of the parent tenant.</param>
    public AccessGrantStore(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Finds a grant by its identifier.
    /// </summary>
    /// <param name="grantId">The grant identifier.</param>
    public Task<AccessGrant> FindAsync(string grantId)
    {
        if (string.IsNullOrEmpty(grantId))
        {
            return Task.FromResult<AccessGrant>(null);
        }

        return _session.Query<AccessGrant, AccessGrantIndex>(index => index.GrantId == grantId, Collection)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Lists the grants for one child tenant only, without the grants for every child tenant.
    /// </summary>
    /// <param name="childEntryId">The registry entry of the child tenant.</param>
    public Task<IReadOnlyList<AccessGrant>> ListForChildAsync(string childEntryId)
    {
        ArgumentException.ThrowIfNullOrEmpty(childEntryId);

        return _session.Query<AccessGrant, AccessGrantIndex>(index => index.ChildEntryId == childEntryId, Collection)
            .OrderBy(index => index.PrincipalType)
            .ListAsync();
    }

    /// <summary>
    /// Lists the grants for every child tenant.
    /// </summary>
    public Task<IReadOnlyList<AccessGrant>> ListParentWideAsync()
    {
        return _session.Query<AccessGrant, AccessGrantIndex>(index => index.ChildEntryId == null, Collection)
            .OrderBy(index => index.PrincipalType)
            .ListAsync();
    }

    /// <summary>
    /// Lists the grants that apply to a parent user directly or through one of the user's roles.
    /// </summary>
    /// <param name="userId">The parent user identifier.</param>
    /// <param name="roleNames">The parent roles of the user.</param>
    public Task<IReadOnlyList<AccessGrant>> ListForPrincipalAsync(string userId, IEnumerable<string> roleNames)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var userType = nameof(AccessGrantPrincipalType.User);
        var roleType = nameof(AccessGrantPrincipalType.Role);
        var roles = roleNames?.Where(role => !string.IsNullOrEmpty(role)).Distinct().ToArray() ?? [];

        if (roles.Length == 0)
        {
            return _session.Query<AccessGrant, AccessGrantIndex>(
                index => index.PrincipalType == userType && index.PrincipalId == userId,
                Collection)
                .ListAsync();
        }

        return _session.Query<AccessGrant, AccessGrantIndex>(
            index => (index.PrincipalType == userType && index.PrincipalId == userId) ||
                (index.PrincipalType == roleType && index.PrincipalId.IsIn(roles)),
            Collection)
            .ListAsync();
    }

    /// <summary>
    /// Saves a grant.
    /// </summary>
    /// <param name="grant">The grant.</param>
    public Task SaveAsync(AccessGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);

        return _session.SaveAsync(grant, checkConcurrency: false, Collection);
    }

    /// <summary>
    /// Deletes a grant.
    /// </summary>
    /// <param name="grant">The grant.</param>
    public void Delete(AccessGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);

        _session.Delete(grant, Collection);
    }

    /// <summary>
    /// Deletes every grant for one child tenant.
    /// </summary>
    /// <param name="childEntryId">The registry entry of the child tenant.</param>
    public async Task DeleteForChildAsync(string childEntryId)
    {
        foreach (var grant in await ListForChildAsync(childEntryId))
        {
            _session.Delete(grant, Collection);
        }
    }
}
