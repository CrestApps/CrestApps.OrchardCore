using CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Services;

/// <summary>
/// Stores the user links in their own collection of the database of the current child tenant.
/// </summary>
public sealed class UserLinkStore
{
    private const string Collection = TenantHierarchyConstants.CollectionName;

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserLinkStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session of the child tenant.</param>
    public UserLinkStore(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Finds the current link of a parent user.
    /// </summary>
    /// <param name="parentTenantId">The parent tenant identifier.</param>
    /// <param name="parentUserId">The parent user identifier.</param>
    public Task<UserLink> FindCurrentAsync(string parentTenantId, string parentUserId)
    {
        ArgumentException.ThrowIfNullOrEmpty(parentTenantId);
        ArgumentException.ThrowIfNullOrEmpty(parentUserId);

        return _session.Query<UserLink, UserLinkIndex>(
            index => index.ParentTenantId == parentTenantId && index.ParentUserId == parentUserId && index.IsCurrent,
            Collection)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Finds a link by its local user. Old links count too, so a local user that was ever linked stays protected.
    /// </summary>
    /// <param name="childUserId">The local user identifier.</param>
    public Task<UserLink> FindByChildUserIdAsync(string childUserId)
    {
        if (string.IsNullOrEmpty(childUserId))
        {
            return Task.FromResult<UserLink>(null);
        }

        return _session.Query<UserLink, UserLinkIndex>(index => index.ChildUserId == childUserId, Collection)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Saves a link.
    /// </summary>
    /// <param name="link">The link.</param>
    public Task SaveAsync(UserLink link)
    {
        ArgumentNullException.ThrowIfNull(link);

        return _session.SaveAsync(link, checkConcurrency: false, Collection);
    }
}
